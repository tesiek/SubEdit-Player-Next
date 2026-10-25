namespace SubEdit.Player.WinForms;

internal static class OsdAppearanceRenderer
{
    public const int HorizontalPadding = 10;
    public const int VerticalPadding = 4;

    public static int GetAlpha(int opacityPercent) =>
        (int)Math.Round(
            Math.Clamp(opacityPercent, 0, 100) * 255m / 100m,
            MidpointRounding.AwayFromZero);

    public static void DrawBackground(
        Graphics graphics,
        Rectangle bounds,
        Color borderColor,
        Color backgroundColor,
        int opacityPercent,
        int requestedBorderThickness)
    {
        var alpha = GetAlpha(opacityPercent);
        using var border = new SolidBrush(Color.FromArgb(alpha, borderColor));
        using var background = new SolidBrush(Color.FromArgb(alpha, backgroundColor));
        DrawBackground(graphics, bounds, border, background, requestedBorderThickness);
    }

    public static void DrawBackground(
        Graphics graphics,
        Rectangle bounds,
        Brush border,
        Brush background,
        int requestedBorderThickness)
    {
        var borderThickness = Math.Min(
            requestedBorderThickness,
            Math.Min(bounds.Width / 2, bounds.Height / 2));
        if (borderThickness <= 0)
        {
            graphics.FillRectangle(background, bounds);
            return;
        }

        graphics.FillRectangle(border, bounds);
        graphics.FillRectangle(
            background,
            bounds.X + borderThickness,
            bounds.Y + borderThickness,
            Math.Max(1, bounds.Width - (2 * borderThickness)),
            Math.Max(1, bounds.Height - (2 * borderThickness)));
    }
}
