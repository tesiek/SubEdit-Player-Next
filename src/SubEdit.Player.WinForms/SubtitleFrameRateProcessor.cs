using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class SubtitleFrameRateProcessor
{
    [GeneratedRegex(@"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$")]
    private static partial Regex MicroDvdPattern();

    public static double CalculateScale(double currentFrameRate, double targetFrameRate)
    {
        if (!double.IsFinite(currentFrameRate) || !double.IsFinite(targetFrameRate) ||
            currentFrameRate <= 0d || targetFrameRate <= 0d)
        {
            return 1.0d;
        }

        if (currentFrameRate == targetFrameRate)
        {
            return 1.0d;
        }

        return targetFrameRate / currentFrameRate;
    }

    public static string Change(
        string text,
        double currentFrameRate,
        double targetFrameRate,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) ||
            !double.IsFinite(currentFrameRate) ||
            !double.IsFinite(targetFrameRate) ||
            currentFrameRate <= 0d ||
            targetFrameRate <= 0d ||
            currentFrameRate == targetFrameRate ||
            lastLine < firstLine)
        {
            return text;
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var ratio = CalculateScale(currentFrameRate, targetFrameRate);

        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            var match = MicroDvdPattern().Match(lines[lineIndex]);
            if (!match.Success ||
                !long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
                !TryParseOptionalFrame(match.Groups["end"].Value, out var end))
            {
                continue;
            }

            var changedStart = RoundAwayFromZero(start * ratio);
            var changedEnd = end.HasValue ? RoundAwayFromZero(end.Value * ratio).ToString(CultureInfo.InvariantCulture) : string.Empty;
            lines[lineIndex] = $"{{{changedStart.ToString(CultureInfo.InvariantCulture)}}}" +
                $"{{{changedEnd}}}" +
                match.Groups["text"].Value;
        }

        return string.Join(lineBreak, lines);
    }

    private static bool TryParseOptionalFrame(string text, out long? value)
    {
        if (text.Length == 0)
        {
            value = null;
            return true;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static long RoundAwayFromZero(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);
}
