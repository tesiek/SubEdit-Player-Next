using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal enum SubtitleTimingFormat
{
    TmPlayerLong,
    TmPlayerShort,
    MicroDvd,
    Mpl2
}

internal static class SubtitleTimingFormatter
{
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex MicroDvdEditablePattern = new(
        @"^\{(?<start>-?\d*)}\{(?<end>-?\d*)}(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex Mpl2EditablePattern = new(
        @"^\[(?<start>-?\d*)]\[(?<end>-?\d*)](?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^\s*[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3}\s*-->\s*[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3}.*$",
        RegexOptions.Compiled);
    public static readonly Regex SrtTimingLinePartsPattern = new(
        @"^(?<before>\s*)(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<arrow>\s*-->\s*)(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<after>.*)$",
        RegexOptions.Compiled);

    public static bool IsSrtTimingLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && SrtTimingPattern.IsMatch(line);

    public static bool TryUpdateSrtTimingLine(
        string line,
        long positionMilliseconds,
        bool insertStart,
        bool preserveDuration,
        out string updatedLine)
    {
        updatedLine = line;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trailingLineEnding = string.Empty;
        if (line.EndsWith("\r\n", StringComparison.Ordinal))
        {
            trailingLineEnding = "\r\n";
            line = line[..^2];
        }
        else if (line.EndsWith('\n'))
        {
            trailingLineEnding = "\n";
            line = line[..^1];
        }
        else if (line.EndsWith('\r'))
        {
            trailingLineEnding = "\r";
            line = line[..^1];
        }

        var match = SrtTimingLinePartsPattern.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var startStr = match.Groups["start"].Value;
        var endStr = match.Groups["end"].Value;
        var separator = startStr.Contains('.') || endStr.Contains('.') ? '.' : ',';

        if (insertStart)
        {
            var newStartStr = FormatSrtTimestamp(positionMilliseconds, separator);
            var newEndStr = endStr;
            if (preserveDuration &&
                TryParseSrtTimestamp(startStr, out var currentStart) &&
                TryParseSrtTimestamp(endStr, out var currentEnd) &&
                currentEnd > currentStart)
            {
                var duration = currentEnd - currentStart;
                newEndStr = FormatSrtTimestamp(positionMilliseconds + duration, separator);
            }

            updatedLine = $"{match.Groups["before"].Value}{newStartStr}{match.Groups["arrow"].Value}{newEndStr}{match.Groups["after"].Value}{trailingLineEnding}";
            return true;
        }
        else
        {
            var newEndStr = FormatSrtTimestamp(positionMilliseconds, separator);
            updatedLine = $"{match.Groups["before"].Value}{startStr}{match.Groups["arrow"].Value}{newEndStr}{match.Groups["after"].Value}{trailingLineEnding}";
            return true;
        }
    }

    public static int FindSrtTimingLineIndex(IReadOnlyList<string> lines, int editorLineIndex) =>
        FindSrtTimingLineIndex(lines.Count, i => lines[i], editorLineIndex);

    public static int FindSrtTimingLineIndex(int lineCount, Func<int, string> getLineText, int editorLineIndex)
    {
        if (editorLineIndex < 0 || editorLineIndex >= lineCount)
        {
            return -1;
        }

        if (editorLineIndex + 1 < lineCount &&
            int.TryParse(getLineText(editorLineIndex).Trim(), out _) &&
            IsSrtTimingLine(getLineText(editorLineIndex + 1)))
        {
            return editorLineIndex + 1;
        }

        for (var index = editorLineIndex; index >= 0; index--)
        {
            var lineText = getLineText(index);
            if (string.IsNullOrWhiteSpace(lineText))
            {
                break;
            }

            if (IsSrtTimingLine(lineText))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool IsRecognizedSubtitleLine(string line) =>
        MicroDvdPattern.IsMatch(line) ||
        Mpl2Pattern.IsMatch(line) ||
        TmPlayerPattern.IsMatch(line) ||
        SrtTimingPattern.IsMatch(line);

    public static SubtitleTimingFormat? Detect(string text)
    {
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (MicroDvdPattern.IsMatch(line)) return SubtitleTimingFormat.MicroDvd;
            if (Mpl2Pattern.IsMatch(line)) return SubtitleTimingFormat.Mpl2;
            var tmPlayer = TmPlayerPattern.Match(line);
            if (tmPlayer.Success)
            {
                return tmPlayer.Groups["hours"].Value.TrimStart('+', '-').Length == 1
                    ? SubtitleTimingFormat.TmPlayerShort
                    : SubtitleTimingFormat.TmPlayerLong;
            }
        }

        return null;
    }

    public static string Insert(string line, long positionMilliseconds, double framesPerSecond,
        SubtitleTimingFormat defaultFormat, bool insertStart, out int timingPrefixLength)
    {
        var microDvd = MicroDvdEditablePattern.Match(line);
        if (microDvd.Success)
        {
            var frame = ToFrame(positionMilliseconds, framesPerSecond).ToString(CultureInfo.InvariantCulture);
            var start = insertStart ? frame : microDvd.Groups["start"].Value;
            var end = insertStart ? microDvd.Groups["end"].Value : frame;
            var prefix = $"{{{start}}}{{{end}}}";
            timingPrefixLength = prefix.Length;
            return prefix + microDvd.Groups["text"].Value;
        }

        var mpl2 = Mpl2EditablePattern.Match(line);
        if (mpl2.Success)
        {
            var tenth = ((long)Math.Round(positionMilliseconds / 100d, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
            var start = insertStart ? tenth : mpl2.Groups["start"].Value;
            var end = insertStart ? mpl2.Groups["end"].Value : tenth;
            var prefix = $"[{start}][{end}]";
            timingPrefixLength = prefix.Length;
            return prefix + mpl2.Groups["text"].Value;
        }

        var tmPlayer = TmPlayerPattern.Match(line);
        if (tmPlayer.Success)
        {
            var format = tmPlayer.Groups["hours"].Value.TrimStart('+', '-').Length == 1
                ? SubtitleTimingFormat.TmPlayerShort
                : SubtitleTimingFormat.TmPlayerLong;
            // TMPlayer stores only the show time; the original leaves it unchanged for an end-frame insertion.
            var time = insertStart
                ? positionMilliseconds
                : ParseTmPlayerMilliseconds(tmPlayer);
            var prefix = FormatTmPlayer(time, format);
            timingPrefixLength = prefix.Length;
            return prefix + tmPlayer.Groups["text"].Value;
        }

        if (TryUpdateSrtTimingLine(line, positionMilliseconds, insertStart, preserveDuration: false, out var updatedSrt))
        {
            timingPrefixLength = 0;
            return updatedSrt;
        }

        return CreateNew(line, positionMilliseconds, framesPerSecond, defaultFormat, insertStart, out timingPrefixLength);
    }

    private static string CreateNew(string text, long positionMilliseconds, double framesPerSecond,
        SubtitleTimingFormat format, bool insertStart, out int timingPrefixLength)
    {
        string prefix;
        switch (format)
        {
            case SubtitleTimingFormat.MicroDvd:
                var frame = ToFrame(positionMilliseconds, framesPerSecond);
                prefix = insertStart ? $"{{{frame}}}{{}}" : $"{{}}{{{frame}}}";
                break;
            case SubtitleTimingFormat.Mpl2:
                var tenth = (long)Math.Round(positionMilliseconds / 100d, MidpointRounding.AwayFromZero);
                prefix = insertStart ? $"[{tenth}][]" : $"[][{tenth}]";
                break;
            default:
                // TMPlayer has no hide time. As in the original, end insertion creates a zero show time.
                prefix = FormatTmPlayer(insertStart ? positionMilliseconds : 0, format);
                break;
        }

        timingPrefixLength = prefix.Length;
        return prefix + text;
    }

    public static long RoundAwayFromZero(double value) =>
        (long)Math.Round(value, MidpointRounding.AwayFromZero);

    private static long ToFrame(long milliseconds, double framesPerSecond) =>
        (long)Math.Round(milliseconds * Math.Max(1d, framesPerSecond) / 1000d, MidpointRounding.AwayFromZero);

    public static bool TryParseTmPlayerTime(string hoursStr, string minutesStr, string secondsStr, out long totalSeconds)
    {
        totalSeconds = 0;
        if (!long.TryParse(hoursStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ||
            !long.TryParse(minutesStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) ||
            !long.TryParse(secondsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ||
            Math.Abs(minutes) > 59 ||
            Math.Abs(seconds) > 59)
        {
            return false;
        }

        totalSeconds = (hours * 3600) + (minutes * 60) + seconds;
        return true;
    }

    private static long ParseTmPlayerMilliseconds(Match match)
    {
        TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out var totalSeconds);
        return totalSeconds * 1000;
    }

    public static string FormatTmPlayerSeconds(long totalSeconds, SubtitleTimingFormat format)
    {
        if (totalSeconds >= 0)
        {
            var seconds = totalSeconds % 60;
            var minutes = (totalSeconds / 60) % 60;
            var hours = totalSeconds / 3600;
            var hoursText = format == SubtitleTimingFormat.TmPlayerShort
                ? hours.ToString(CultureInfo.InvariantCulture)
                : hours.ToString("D2", CultureInfo.InvariantCulture);
            return $"{hoursText}:{minutes:D2}:{seconds:D2}:";
        }
        else
        {
            var absSeconds = Math.Abs(totalSeconds);
            var seconds = absSeconds % 60;
            var minutes = (absSeconds / 60) % 60;
            var hours = absSeconds / 3600;

            var hoursText = hours == 0 ? "00" : $"-{hours.ToString(CultureInfo.InvariantCulture)}";
            var minutesText = minutes == 0 ? "00" : $"-{minutes.ToString(CultureInfo.InvariantCulture)}";
            var secondsText = seconds == 0 ? "00" : $"-{seconds.ToString(CultureInfo.InvariantCulture)}";

            return $"{hoursText}:{minutesText}:{secondsText}:";
        }
    }

    public static string FormatTmPlayer(long milliseconds, SubtitleTimingFormat format)
    {
        var totalSeconds = RoundAwayFromZero(milliseconds / 1000d);
        return FormatTmPlayerSeconds(totalSeconds, format);
    }

    public static string FormatSrtTimestamp(long milliseconds, char separator = ',')
    {
        var isNegative = milliseconds < 0;
        var absMs = Math.Abs(milliseconds);
        var hours = absMs / 3_600_000;
        var minutes = (absMs / 60_000) % 60;
        var seconds = (absMs / 1_000) % 60;
        var fraction = absMs % 1_000;
        var sign = isNegative ? "-" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{hours:D2}:{minutes:D2}:{seconds:D2}{separator}{fraction:D3}");
    }

    public static bool TryParseSrtTimestamp(string text, out long milliseconds)
    {
        milliseconds = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var isNegative = false;
        if (trimmed.StartsWith('-'))
        {
            isNegative = true;
            trimmed = trimmed[1..];
        }
        else if (trimmed.StartsWith('+'))
        {
            trimmed = trimmed[1..];
        }

        var parts = trimmed.Replace(',', '.').Split([':', '.']);
        if (parts.Length != 4 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            !int.TryParse(parts[3].PadRight(3, '0')[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var fraction) ||
            minutes is > 59 || seconds is > 59)
        {
            return false;
        }

        var totalMs = (((hours * 60L) + minutes) * 60 + seconds) * 1000 + fraction;
        milliseconds = isNegative ? -totalMs : totalMs;
        return true;
    }
}
