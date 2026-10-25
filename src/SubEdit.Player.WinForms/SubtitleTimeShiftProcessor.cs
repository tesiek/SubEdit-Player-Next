using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal readonly record struct SubtitleTimeShiftResult(string Text, bool HasNegativeTime);

internal static partial class SubtitleTimeShiftProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^(?<before>\s*)(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^(?<before>\s*)(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<arrow>\s*-->\s*)(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<after>.*)$",
        RegexOptions.Compiled);

    public static string Shift(string text, double shiftSeconds, double framesPerSecond) =>
        Shift(text, shiftSeconds, framesPerSecond, true, true, 0, int.MaxValue).Text;

    public static SubtitleTimeShiftResult Shift(
        string text,
        double shiftSeconds,
        double framesPerSecond,
        bool shiftStart,
        bool shiftEnd,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return new SubtitleTimeShiftResult(text, false);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        var hasNegativeTime = false;

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            lines[lineIndex] = ShiftLine(
                lines[lineIndex],
                shiftSeconds,
                safeFramesPerSecond,
                shiftStart,
                shiftEnd,
                ref hasNegativeTime);
        }

        return new SubtitleTimeShiftResult(string.Join(lineBreak, lines), hasNegativeTime);
    }

    private static string ShiftLine(
        string line,
        double shiftSeconds,
        double framesPerSecond,
        bool shiftStart,
        bool shiftEnd,
        ref bool hasNegativeTime)
    {
        var microDvd = MicroDvdPattern.Match(line);
        if (microDvd.Success)
        {
            var start = long.Parse(microDvd.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = microDvd.Groups["end"].Value;
            long? end = endText.Length == 0 ? null : long.Parse(endText, CultureInfo.InvariantCulture);
            var deltaFrames = SubtitleTimingFormatter.RoundAwayFromZero(shiftSeconds * framesPerSecond);
            if (shiftStart)
            {
                start += deltaFrames;
            }
            if (shiftEnd && end.HasValue)
            {
                end += deltaFrames;
            }

            hasNegativeTime |= start < 0 || (end.HasValue && end.Value < 0);
            return $"{{{start.ToString(CultureInfo.InvariantCulture)}}}" +
                $"{{{(end.HasValue ? end.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}}}" +
                microDvd.Groups["text"].Value;
        }

        var mpl2 = Mpl2Pattern.Match(line);
        if (mpl2.Success)
        {
            var start = long.Parse(mpl2.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = mpl2.Groups["end"].Value;
            long? end = endText.Length == 0 ? null : long.Parse(endText, CultureInfo.InvariantCulture);
            var deltaDeci = SubtitleTimingFormatter.RoundAwayFromZero(shiftSeconds * 10d);
            if (shiftStart)
            {
                start += deltaDeci;
            }
            if (shiftEnd && end.HasValue)
            {
                end += deltaDeci;
            }

            hasNegativeTime |= start < 0 || (end.HasValue && end.Value < 0);
            return $"[{start.ToString(CultureInfo.InvariantCulture)}]" +
                $"[{(end.HasValue ? end.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}]" +
                mpl2.Groups["text"].Value;
        }

        var tmPlayer = TmPlayerPattern.Match(line);
        if (tmPlayer.Success && TryReadTmPlayerSeconds(tmPlayer, out var tmPlayerSeconds))
        {
            var deltaSeconds = SubtitleTimingFormatter.RoundAwayFromZero(shiftSeconds);
            var finalSeconds = tmPlayerSeconds + deltaSeconds;
            hasNegativeTime |= finalSeconds < 0;
            var isShort = tmPlayer.Groups["hours"].Value.TrimStart('+', '-').Length == 1;
            var formatted = SubtitleTimingFormatter.FormatTmPlayerSeconds(
                finalSeconds,
                isShort ? SubtitleTimingFormat.TmPlayerShort : SubtitleTimingFormat.TmPlayerLong);
            var separator = tmPlayer.Groups["separator"].Value;
            var formattedTimeOnly = formatted.TrimEnd(':');
            return $"{tmPlayer.Groups["before"].Value}{formattedTimeOnly}{separator}{tmPlayer.Groups["text"].Value}";
        }

        var srt = SrtTimingPattern.Match(line);
        if (srt.Success &&
            SubtitleTimingFormatter.TryParseSrtTimestamp(srt.Groups["start"].Value, out var startMilliseconds) &&
            SubtitleTimingFormatter.TryParseSrtTimestamp(srt.Groups["end"].Value, out var endMilliseconds))
        {
            var offsetMilliseconds = SubtitleTimingFormatter.RoundAwayFromZero(shiftSeconds * 1000d);
            var shiftedStart = shiftStart ? startMilliseconds + offsetMilliseconds : startMilliseconds;
            var shiftedEnd = shiftEnd ? endMilliseconds + offsetMilliseconds : endMilliseconds;
            hasNegativeTime |= shiftedStart < 0 || shiftedEnd < 0;

            return $"{srt.Groups["before"].Value}" +
                $"{SubtitleTimingFormatter.FormatSrtTimestamp(shiftedStart, srt.Groups["start"].Value.Contains('.') ? '.' : ',')}" +
                $"{srt.Groups["arrow"].Value}" +
                $"{SubtitleTimingFormatter.FormatSrtTimestamp(shiftedEnd, srt.Groups["end"].Value.Contains('.') ? '.' : ',')}" +
                $"{srt.Groups["after"].Value}";
        }

        return line;
    }

    private static bool TryReadTmPlayerSeconds(Match match, out long totalSeconds) =>
        SubtitleTimingFormatter.TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out totalSeconds);

    private static bool TryParseSrtTimestamp(string text, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(text, out milliseconds);

    private static string FormatSrtTimestamp(long milliseconds, char separator) =>
        SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, separator);
}
