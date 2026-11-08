namespace SubEdit.Player.WinForms;

internal static class PlaybackSliderStyle
{
    internal static readonly Color TrackFill = Color.FromArgb(244, 244, 244);
    internal static readonly Color TrackBorder = Color.FromArgb(145, 145, 145);
    internal static readonly Color ThumbFill = Color.FromArgb(238, 238, 238);
    internal static readonly Color ThumbBorder = Color.FromArgb(90, 90, 90);
    internal static readonly Color SeekProgress = Color.FromArgb(170, 190, 205);
    internal static readonly Color VolumeProgress = Lighten(SeekProgress, 0.10);

    internal static int TrackHeight(int dpi) => Scale(7, dpi);
    internal static int ThumbWidth(int dpi) => Scale(8, dpi);
    internal static int ThumbHeight(int dpi) => Scale(14, dpi);

    internal static int Scale(int logical, int dpi) =>
        Math.Max(1, (int)Math.Round(logical * Math.Max(1, dpi) / 96d));

    private static Color Lighten(Color color, double amount)
    {
        int Blend(int channel) =>
            (int)Math.Round(channel + (255 - channel) * amount, MidpointRounding.AwayFromZero);

        return Color.FromArgb(Blend(color.R), Blend(color.G), Blend(color.B));
    }
}
