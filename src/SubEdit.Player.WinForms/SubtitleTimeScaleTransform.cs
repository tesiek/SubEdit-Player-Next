namespace SubEdit.Player.WinForms;

internal readonly record struct SubtitleTimeScaleTransform(
    double Scale,
    string Description)
{
    public static SubtitleTimeScaleTransform? Register(
        SubtitleTimeScaleTransform? current,
        double scale,
        string description,
        bool isFullDocument)
    {
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            return current;
        }

        if (scale == 1.0d)
        {
            return current;
        }

        if (!isFullDocument)
        {
            return null;
        }

        if (current is { } existing)
        {
            return new SubtitleTimeScaleTransform(
                existing.Scale * scale,
                "Złożona korekta czasu");
        }

        return new SubtitleTimeScaleTransform(scale, description);
    }
}
