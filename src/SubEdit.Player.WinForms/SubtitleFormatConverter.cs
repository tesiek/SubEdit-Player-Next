using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static class SubtitleFormatConverter
{
    private static readonly Regex SrtTimingPattern = new(
        @"^\s*(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})",
        RegexOptions.Compiled);
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);

    public static bool IsSrtDocument(string text) => text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace('\r', '\n')
        .Split('\n')
        .Any(line => SrtTimingPattern.IsMatch(line));

    public static string ConvertSrtToMicroDvd(string text, double framesPerSecond)
    {
        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        var convertedLines = new List<string>();

        for (var index = 0; index < lines.Length;)
        {
            while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            if (index >= lines.Length)
            {
                break;
            }

            var timingIndex = int.TryParse(lines[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? index + 1
                : index;
            if (timingIndex >= lines.Length || !TryReadSrtTiming(lines[timingIndex], out var startMilliseconds, out var endMilliseconds))
            {
                index++;
                continue;
            }

            index = timingIndex + 1;
            var cueLines = new List<string>();
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                cueLines.Add(lines[index]);
                index++;
            }

            convertedLines.Add(
                $"{{{ToRoundedInteger(startMilliseconds * safeFramesPerSecond / 1000d)}}}" +
                $"{{{ToRoundedInteger(endMilliseconds * safeFramesPerSecond / 1000d)}}}" +
                string.Join("|", cueLines));
        }

        return string.Join(lineBreak, convertedLines);
    }

    public static string Convert(
        string text,
        SubtitleTimingFormat sourceFormat,
        SubtitleTimingFormat targetFormat,
        double framesPerSecond) =>
        Convert(text, sourceFormat, targetFormat, framesPerSecond, 0, int.MaxValue);

    public static string Convert(
        string text,
        SubtitleTimingFormat sourceFormat,
        SubtitleTimingFormat targetFormat,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        if (sourceFormat == targetFormat || string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return text;
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);

        var convertedLines = new List<string>(lines.Length);
        for (var i = 0; i < firstLine; i++)
        {
            convertedLines.Add(lines[i]);
        }

        if (sourceFormat is SubtitleTimingFormat.TmPlayerLong or SubtitleTimingFormat.TmPlayerShort)
        {
            for (var lineIndex = firstLine; lineIndex <= lastLine;)
            {
                if (!TryReadCue(lines[lineIndex], sourceFormat, safeFramesPerSecond, out var startSeconds, out _, out _, out var subtitleText))
                {
                    convertedLines.Add(lines[lineIndex]);
                    lineIndex++;
                    continue;
                }

                var texts = new List<string> { subtitleText };
                var groupEndLineIndex = lineIndex;
                while (groupEndLineIndex + 1 <= lastLine &&
                       TryReadCue(lines[groupEndLineIndex + 1], sourceFormat, safeFramesPerSecond, out var nextStartSeconds, out _, out _, out var nextSubtitleText) &&
                       nextStartSeconds == startSeconds)
                {
                    texts.Add(nextSubtitleText);
                    groupEndLineIndex++;
                }

                var endSeconds = FindTmPlayerEndSeconds(lines, groupEndLineIndex, startSeconds) ?? startSeconds + 3d;

                if (targetFormat is SubtitleTimingFormat.MicroDvd or SubtitleTimingFormat.Mpl2)
                {
                    var combinedText = string.Join("|", texts);
                    convertedLines.Add(FormatCue(targetFormat, startSeconds, endSeconds, hasEnd: true, combinedText, safeFramesPerSecond));
                }
                else
                {
                    foreach (var t in texts)
                    {
                        convertedLines.Add(FormatCue(targetFormat, startSeconds, endSeconds, hasEnd: false, t, safeFramesPerSecond));
                    }
                }

                lineIndex = groupEndLineIndex + 1;
            }
        }
        else
        {
            for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
            {
                if (!TryReadCue(lines[lineIndex], sourceFormat, safeFramesPerSecond, out var startSeconds, out var endSeconds, out var hasEnd, out var subtitleText))
                {
                    convertedLines.Add(lines[lineIndex]);
                    continue;
                }

                convertedLines.Add(FormatCue(targetFormat, startSeconds, endSeconds, hasEnd, subtitleText, safeFramesPerSecond));
            }
        }

        for (var i = lastLine + 1; i < lines.Length; i++)
        {
            convertedLines.Add(lines[i]);
        }

        return string.Join(lineBreak, convertedLines);
    }

    public static IReadOnlyList<int> GetUnrecognizedLineIndices(
        string text,
        int firstLine,
        int lastLine)
    {
        var lines = SplitLines(text);
        if (lines.Length == 0 || lastLine < firstLine)
        {
            return [];
        }

        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var indices = new List<int>();

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            if (!string.IsNullOrWhiteSpace(lines[lineIndex]) &&
                !IsRecognizedSubtitleLine(lines[lineIndex]))
            {
                indices.Add(lineIndex);
            }
        }

        return indices;
    }

    public static bool HasUnsortedCues(
        string text,
        SubtitleTimingFormat sourceFormat,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        var lines = SplitLines(text);
        if (lines.Length == 0 || lastLine < firstLine)
        {
            return false;
        }

        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        double? previousStart = null;

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            if (!TryReadCue(
                    lines[lineIndex],
                    sourceFormat,
                    safeFramesPerSecond,
                    out var startSeconds,
                    out _,
                    out _,
                    out _))
            {
                continue;
            }

            if (previousStart is { } previous && startSeconds < previous)
            {
                return true;
            }

            previousStart = startSeconds;
        }

        return false;
    }

    public static string DeleteLines(string text, IReadOnlySet<int> lineIndices)
    {
        if (lineIndices.Count == 0)
        {
            return text;
        }

        var lineBreak = GetLineBreak(text);
        var lines = SplitLines(text);
        return string.Join(
            lineBreak,
            lines.Where((_, lineIndex) => !lineIndices.Contains(lineIndex)));
    }

    internal static bool IsRecognizedSubtitleLine(string line) =>
        TryReadCue(line, SubtitleTimingFormat.MicroDvd, 25d, out _, out _, out _, out _) ||
        TryReadCue(line, SubtitleTimingFormat.Mpl2, 25d, out _, out _, out _, out _) ||
        TryReadCue(line, SubtitleTimingFormat.TmPlayerLong, 25d, out _, out _, out _, out _);

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string GetLineBreak(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static bool TryReadCue(
        string line,
        SubtitleTimingFormat format,
        double framesPerSecond,
        out double startSeconds,
        out double endSeconds,
        out bool hasEnd,
        out string text)
    {
        startSeconds = 0;
        endSeconds = 0;
        hasEnd = false;
        text = string.Empty;

        switch (format)
        {
            case SubtitleTimingFormat.MicroDvd:
                {
                    var match = MicroDvdPattern.Match(line);
                    if (!match.Success || !long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startFrame) ||
                        !TryParseOptionalLong(match.Groups["end"].Value, out var endFrame, out hasEnd))
                    {
                        return false;
                    }

                    startSeconds = startFrame / framesPerSecond;
                    endSeconds = hasEnd ? endFrame / framesPerSecond : 0d;
                    text = match.Groups["text"].Value;
                    return true;
                }
            case SubtitleTimingFormat.Mpl2:
                {
                    var match = Mpl2Pattern.Match(line);
                    if (!match.Success || !long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTenth) ||
                        !TryParseOptionalLong(match.Groups["end"].Value, out var endTenth, out hasEnd))
                    {
                        return false;
                    }

                    startSeconds = startTenth / 10d;
                    endSeconds = hasEnd ? endTenth / 10d : 0d;
                    text = match.Groups["text"].Value;
                    return true;
                }
            default:
                {
                    var match = TmPlayerPattern.Match(line);
                    if (!match.Success || !TryReadTmPlayerSeconds(match, out startSeconds))
                    {
                        return false;
                    }

                    hasEnd = false;
                    text = match.Groups["text"].Value;
                    return true;
                }
        }
    }

    private static double? FindTmPlayerEndSeconds(IReadOnlyList<string> lines, int lineIndex, double startSeconds)
    {
        for (var index = lineIndex + 1; index < lines.Count; index++)
        {
            var match = TmPlayerPattern.Match(lines[index]);
            if (match.Success && TryReadTmPlayerSeconds(match, out var candidate) && candidate > startSeconds)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FormatCue(
        SubtitleTimingFormat format,
        double startSeconds,
        double endSeconds,
        bool hasEnd,
        string text,
        double framesPerSecond) => format switch
        {
            SubtitleTimingFormat.TmPlayerLong or SubtitleTimingFormat.TmPlayerShort =>
                $"{FormatTmPlayer(startSeconds, format)}{text}",
            SubtitleTimingFormat.MicroDvd =>
                hasEnd
                    ? $"{{{ToRoundedInteger(startSeconds * framesPerSecond)}}}{{{ToRoundedInteger(endSeconds * framesPerSecond)}}}{text}"
                    : $"{{{ToRoundedInteger(startSeconds * framesPerSecond)}}}{{}}{text}",
            SubtitleTimingFormat.Mpl2 =>
                hasEnd
                    ? $"[{ToRoundedInteger(startSeconds * 10d)}][{ToRoundedInteger(endSeconds * 10d)}]{text}"
                    : $"[{ToRoundedInteger(startSeconds * 10d)}][]{text}",
            _ => text
        };

    private static bool TryReadTmPlayerSeconds(Match match, out double seconds)
    {
        if (SubtitleTimingFormatter.TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out var totalSeconds))
        {
            seconds = totalSeconds;
            return true;
        }

        seconds = 0;
        return false;
    }

    private static bool TryParseOptionalLong(string text, out long value, out bool hasValue)
    {
        if (text.Length == 0)
        {
            value = 0;
            hasValue = false;
            return true;
        }

        hasValue = true;
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadSrtTiming(string line, out long startMilliseconds, out long endMilliseconds)
    {
        startMilliseconds = 0;
        endMilliseconds = 0;
        var match = SrtTimingPattern.Match(line);
        return match.Success &&
            TryParseSrtTimestamp(match.Groups["start"].Value, out startMilliseconds) &&
            TryParseSrtTimestamp(match.Groups["end"].Value, out endMilliseconds);
    }

    private static bool TryParseSrtTimestamp(string text, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(text, out milliseconds);

    private static long ToRoundedInteger(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);

    private static string FormatTmPlayer(double seconds, SubtitleTimingFormat format) =>
        SubtitleTimingFormatter.FormatTmPlayerSeconds(ToRoundedInteger(seconds), format);
}
