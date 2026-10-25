using SubEdit.Player.Core.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class SubtitleDisplayTimeProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?[:=].*$",
        RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^(?<before>\s*)(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<arrow>\s*-->\s*)(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<after>.*)$",
        RegexOptions.Compiled);

    public static string Change(
        string text,
        SubtitleDisplayTimeSettings options,
        bool clearEndTimes,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return text;
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);

        if (SubtitleFormatConverter.IsSrtDocument(text))
        {
            ChangeSrt(lines, options, clearEndTimes, safeFramesPerSecond, minimumGapFrames, firstLine, lastLine);
        }
        else
        {
            ChangeInline(lines, options, clearEndTimes, safeFramesPerSecond, minimumGapFrames, firstLine, lastLine);
        }

        return string.Join(lineBreak, lines);
    }

    private static void ChangeInline(
        string[] lines,
        SubtitleDisplayTimeSettings options,
        bool clearEndTimes,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine)
    {
        var entries = lines
            .Select((line, lineIndex) => ParseInline(line, lineIndex, framesPerSecond))
            .ToArray();

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (entry is null || entry.LineIndex < firstLine || entry.LineIndex > lastLine)
            {
                continue;
            }

            if (entry.Format == InlineFormat.TmPlayer)
            {
                continue;
            }

            if (clearEndTimes)
            {
                entry.EndSeconds = 0d;
                entry.EndIsEmpty = true;
                lines[entry.LineIndex] = entry.Serialize(framesPerSecond);
                continue;
            }

            if (options.ChangeOnlyUndefinedEndTimes && !entry.EndIsEmpty)
            {
                continue;
            }

            entry.EndSeconds = entry.StartSeconds + CalculateDuration(entry.Text, options);
            entry.EndIsEmpty = false;
            if (options.BlockEndTime)
            {
                var next = entries.Skip(index + 1).FirstOrDefault(candidate => candidate is not null);
                if (next is not null)
                {
                    entry.EndSeconds = Math.Min(
                        entry.EndSeconds,
                        Math.Max(entry.StartSeconds, next.StartSeconds - MinimumGapSeconds(framesPerSecond, minimumGapFrames)));
                }
            }

            lines[entry.LineIndex] = entry.Serialize(framesPerSecond);
        }
    }

    private static void ChangeSrt(
        string[] lines,
        SubtitleDisplayTimeSettings options,
        bool clearEndTimes,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine)
    {
        if (clearEndTimes)
        {
            return;
        }

        var cues = ParseSrt(lines);
        for (var index = 0; index < cues.Count; index++)
        {
            var cue = cues[index];
            if (cue.BlockStart > lastLine || cue.BlockEndExclusive <= firstLine)
            {
                continue;
            }

            if (options.ChangeOnlyUndefinedEndTimes)
            {
                continue;
            }

            var endMilliseconds = cue.StartMilliseconds +
                RoundLikeOriginal(CalculateDuration(cue.Text, options) * 1000d);
            if (options.BlockEndTime && index + 1 < cues.Count)
            {
                endMilliseconds = Math.Min(
                    endMilliseconds,
                    cues[index + 1].StartMilliseconds - MinimumGapMilliseconds(framesPerSecond, minimumGapFrames));
            }

            endMilliseconds = Math.Max(cue.StartMilliseconds, endMilliseconds);
            lines[cue.TimingLine] =
                $"{cue.Before}{FormatSrtTimestamp(endMilliseconds, cue.Separator)}{cue.After}";
        }
    }

    private static InlineEntry? ParseInline(string line, int lineIndex, double framesPerSecond)
    {
        var microDvd = MicroDvdPattern.Match(line);
        if (microDvd.Success)
        {
            var start = long.Parse(microDvd.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = microDvd.Groups["end"].Value;
            return new InlineEntry(
                lineIndex,
                InlineFormat.MicroDvd,
                start / framesPerSecond,
                endText.Length == 0
                    ? 0d
                    : long.Parse(endText, CultureInfo.InvariantCulture) / framesPerSecond,
                endText.Length == 0,
                microDvd.Groups["text"].Value);
        }

        var mpl2 = Mpl2Pattern.Match(line);
        if (mpl2.Success)
        {
            var start = long.Parse(mpl2.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = mpl2.Groups["end"].Value;
            return new InlineEntry(
                lineIndex,
                InlineFormat.Mpl2,
                start / 10d,
                endText.Length == 0
                    ? 0d
                    : long.Parse(endText, CultureInfo.InvariantCulture) / 10d,
                endText.Length == 0,
                mpl2.Groups["text"].Value);
        }

        var tmPlayer = TmPlayerPattern.Match(line);
        if (tmPlayer.Success &&
            SubtitleTimingFormatter.TryParseTmPlayerTime(
                tmPlayer.Groups["hours"].Value,
                tmPlayer.Groups["minutes"].Value,
                tmPlayer.Groups["seconds"].Value,
                out var totalSeconds))
        {
            return new InlineEntry(
                lineIndex,
                InlineFormat.TmPlayer,
                totalSeconds,
                0d,
                true,
                line);
        }

        return null;
    }

    private static List<SrtCue> ParseSrt(string[] lines)
    {
        var cues = new List<SrtCue>();
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var timing = SrtTimingPattern.Match(lines[lineIndex]);
            if (!timing.Success ||
                !TryParseSrtTimestamp(timing.Groups["start"].Value, out var startMilliseconds))
            {
                continue;
            }

            var blockStart = lineIndex;
            if (lineIndex > 0 &&
                int.TryParse(lines[lineIndex - 1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                blockStart--;
            }

            var textLines = new List<string>();
            var blockEnd = lineIndex + 1;
            while (blockEnd < lines.Length && !string.IsNullOrWhiteSpace(lines[blockEnd]))
            {
                textLines.Add(lines[blockEnd]);
                blockEnd++;
            }

            var endValue = timing.Groups["end"].Value;
            var separator = endValue.Contains('.') ? '.' : ',';
            cues.Add(new SrtCue(
                blockStart,
                blockEnd,
                lineIndex,
                startMilliseconds,
                string.Join('\n', textLines),
                $"{timing.Groups["before"].Value}{timing.Groups["start"].Value}{timing.Groups["arrow"].Value}",
                timing.Groups["after"].Value,
                separator));
        }

        return cues;
    }

    private static double CalculateDuration(string text, SubtitleDisplayTimeSettings options) =>
        (options.DurationDependsOnTextLength
            ? text.Length * 0.02d * options.BaseDurationSeconds
            : options.BaseDurationSeconds) +
        options.AdditionalDurationSeconds;

    private static bool TryParseSrtTimestamp(string text, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(text, out milliseconds);

    private static string FormatSrtTimestamp(long milliseconds, char separator) =>
        SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, separator);

    private static double MinimumGapSeconds(double framesPerSecond, int minimumGapFrames) =>
        Math.Clamp(minimumGapFrames, 1, 20) / Math.Max(1d, framesPerSecond);

    private static long MinimumGapMilliseconds(double framesPerSecond, int minimumGapFrames) =>
        Math.Max(1, RoundLikeOriginal(MinimumGapSeconds(framesPerSecond, minimumGapFrames) * 1000d));

    private static long RoundLikeOriginal(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);

    private enum InlineFormat
    {
        MicroDvd,
        Mpl2,
        TmPlayer
    }

    private sealed class InlineEntry(
        int lineIndex,
        InlineFormat format,
        double startSeconds,
        double endSeconds,
        bool endIsEmpty,
        string text)
    {
        public int LineIndex { get; } = lineIndex;
        public InlineFormat Format { get; } = format;
        public double StartSeconds { get; } = startSeconds;
        public double EndSeconds { get; set; } = endSeconds;
        public bool EndIsEmpty { get; set; } = endIsEmpty;
        public string Text { get; } = text;

        public string Serialize(double framesPerSecond)
        {
            if (Format == InlineFormat.TmPlayer)
            {
                return Text;
            }

            var unitMultiplier = Format == InlineFormat.MicroDvd ? framesPerSecond : 10d;
            var start = RoundLikeOriginal(StartSeconds * unitMultiplier);
            var end = EndIsEmpty
                ? string.Empty
                : RoundLikeOriginal(EndSeconds * unitMultiplier).ToString(CultureInfo.InvariantCulture);
            return Format == InlineFormat.MicroDvd
                ? $"{{{start.ToString(CultureInfo.InvariantCulture)}}}{{{end}}}{Text}"
                : $"[{start.ToString(CultureInfo.InvariantCulture)}][{end}]{Text}";
        }
    }

    private sealed record SrtCue(
        int BlockStart,
        int BlockEndExclusive,
        int TimingLine,
        long StartMilliseconds,
        string Text,
        string Before,
        string After,
        char Separator);
}
