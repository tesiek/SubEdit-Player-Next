using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal readonly record struct GeometricSubtitleShiftResult(string Text, bool HasNegativeTime, double Scale = 1.0d);

internal static partial class GeometricSubtitleShiftProcessor
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

    public static double CalculateScale(
        double initialSubtitleMilliseconds,
        double initialVideoMilliseconds,
        double finalSubtitleMilliseconds,
        double finalVideoMilliseconds)
    {
        var subtitleDelta = finalSubtitleMilliseconds - initialSubtitleMilliseconds;
        if (subtitleDelta <= 0d)
        {
            return 1.0d;
        }

        var videoDelta = finalVideoMilliseconds - initialVideoMilliseconds;
        if (videoDelta == subtitleDelta)
        {
            return 1.0d;
        }

        return videoDelta / subtitleDelta;
    }

    public static GeometricSubtitleShiftResult Shift(
        string text,
        double initialSubtitleMilliseconds,
        double initialVideoMilliseconds,
        double finalSubtitleMilliseconds,
        double finalVideoMilliseconds,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) ||
            lastLine < firstLine ||
            finalSubtitleMilliseconds <= initialSubtitleMilliseconds)
        {
            return new GeometricSubtitleShiftResult(text, false, 1.0d);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var scale = CalculateScale(
            initialSubtitleMilliseconds,
            initialVideoMilliseconds,
            finalSubtitleMilliseconds,
            finalVideoMilliseconds);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        var hasNegativeTime = false;

        double Transform(double value) =>
            initialVideoMilliseconds + ((value - initialSubtitleMilliseconds) * scale);

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            lines[lineIndex] = ShiftLine(
                lines[lineIndex],
                Transform,
                safeFramesPerSecond,
                ref hasNegativeTime);
        }

        return new GeometricSubtitleShiftResult(string.Join(lineBreak, lines), hasNegativeTime, scale);
    }

    private static string ShiftLine(
        string line,
        Func<double, double> transform,
        double framesPerSecond,
        ref bool hasNegativeTime)
    {
        var microDvd = MicroDvdPattern.Match(line);
        if (microDvd.Success)
        {
            var start = long.Parse(microDvd.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = microDvd.Groups["end"].Value;
            var shiftedStart = SubtitleTimingFormatter.RoundAwayFromZero(transform(start * 1000d / framesPerSecond) * framesPerSecond / 1000d);
            long? shiftedEnd = null;
            if (endText.Length > 0)
            {
                var end = long.Parse(endText, CultureInfo.InvariantCulture);
                shiftedEnd = SubtitleTimingFormatter.RoundAwayFromZero(transform(end * 1000d / framesPerSecond) * framesPerSecond / 1000d);
            }

            hasNegativeTime |= shiftedStart < 0 || (shiftedEnd.HasValue && shiftedEnd.Value < 0);
            return $"{{{shiftedStart.ToString(CultureInfo.InvariantCulture)}}}" +
                $"{{{(shiftedEnd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)}}}" +
                microDvd.Groups["text"].Value;
        }

        var mpl2 = Mpl2Pattern.Match(line);
        if (mpl2.Success)
        {
            var start = long.Parse(mpl2.Groups["start"].Value, CultureInfo.InvariantCulture);
            var endText = mpl2.Groups["end"].Value;
            var shiftedStart = SubtitleTimingFormatter.RoundAwayFromZero(transform(start * 100d) / 100d);
            long? shiftedEnd = null;
            if (endText.Length > 0)
            {
                var end = long.Parse(endText, CultureInfo.InvariantCulture);
                shiftedEnd = SubtitleTimingFormatter.RoundAwayFromZero(transform(end * 100d) / 100d);
            }

            hasNegativeTime |= shiftedStart < 0 || (shiftedEnd.HasValue && shiftedEnd.Value < 0);
            return $"[{shiftedStart.ToString(CultureInfo.InvariantCulture)}]" +
                $"[{(shiftedEnd?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)}]" +
                mpl2.Groups["text"].Value;
        }

        var tmPlayer = TmPlayerPattern.Match(line);
        if (tmPlayer.Success && TryReadTmPlayerMilliseconds(tmPlayer, out var tmPlayerMilliseconds))
        {
            var shifted = transform(tmPlayerMilliseconds);
            hasNegativeTime |= shifted < 0;
            var totalSeconds = SubtitleTimingFormatter.RoundAwayFromZero(shifted / 1000d);
            var isShort = tmPlayer.Groups["hours"].Value.TrimStart('+', '-').Length == 1;
            var formatted = SubtitleTimingFormatter.FormatTmPlayerSeconds(
                totalSeconds,
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
            var shiftedStart = SubtitleTimingFormatter.RoundAwayFromZero(transform(startMilliseconds));
            var shiftedEnd = SubtitleTimingFormatter.RoundAwayFromZero(transform(endMilliseconds));
            hasNegativeTime |= shiftedStart < 0 || shiftedEnd < 0;
            return $"{srt.Groups["before"].Value}" +
                $"{SubtitleTimingFormatter.FormatSrtTimestamp(shiftedStart, srt.Groups["start"].Value.Contains('.') ? '.' : ',')}" +
                $"{srt.Groups["arrow"].Value}" +
                $"{SubtitleTimingFormatter.FormatSrtTimestamp(shiftedEnd, srt.Groups["end"].Value.Contains('.') ? '.' : ',')}" +
                $"{srt.Groups["after"].Value}";
        }

        return line;
    }

    private static bool TryReadTmPlayerMilliseconds(Match match, out long milliseconds)
    {
        if (SubtitleTimingFormatter.TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out var totalSeconds))
        {
            milliseconds = totalSeconds * 1000L;
            return true;
        }

        milliseconds = 0;
        return false;
    }
}
