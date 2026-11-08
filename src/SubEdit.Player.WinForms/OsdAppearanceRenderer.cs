namespace SubEdit.Player.WinForms;

internal static class OsdAppearanceRenderer
{
    public const int HorizontalPadding = 10;
    public const int VerticalPadding = 4;

    public static int GetAlpha(int transparencyPercent) =>
        (int)Math.Round(
            (100 - Math.Clamp(transparencyPercent, 0, 100)) * 255m / 100m,
            MidpointRounding.AwayFromZero);

    public static void DrawBackground(
        Graphics graphics,
        Rectangle bounds,
        Color borderColor,
        Color backgroundColor,
        int transparencyPercent,
        int requestedBorderThickness)
    {
        var alpha = GetAlpha(transparencyPercent);
        using var border = new SolidBrush(borderColor);
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

        // Paint disjoint regions so translucent backgrounds never blend with the border.
        graphics.FillRectangle(border, bounds.X, bounds.Y, bounds.Width, borderThickness);
        graphics.FillRectangle(border, bounds.X, bounds.Bottom - borderThickness, bounds.Width, borderThickness);
        var innerHeight = bounds.Height - (2 * borderThickness);
        graphics.FillRectangle(border, bounds.X, bounds.Y + borderThickness, borderThickness, innerHeight);
        graphics.FillRectangle(border, bounds.Right - borderThickness, bounds.Y + borderThickness, borderThickness, innerHeight);
        graphics.FillRectangle(
            background,
            bounds.X + borderThickness,
            bounds.Y + borderThickness,
            bounds.Width - (2 * borderThickness),
            innerHeight);
    }
}
