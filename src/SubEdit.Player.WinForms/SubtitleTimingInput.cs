using System.Globalization;

namespace SubEdit.Player.WinForms;

internal static class SubtitleTimingInput
{
    public static bool TryParseNonNegativeNumber(string text, out double value)
    {
        var normalized = text.Trim();
        var parsed =
            double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            double.TryParse(
                normalized.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        return parsed && double.IsFinite(value) && value >= 0d;
    }
}
