namespace SubEdit.Player.WinForms.Controls;

internal sealed class FullScreenToolbarRenderer : ToolStripProfessionalRenderer
{
    internal static Color BackgroundColor => SystemColors.Control;

    public FullScreenToolbarRenderer()
        : base(new FullScreenToolbarColorTable())
    {
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // The transport rectangle produced the two end lines and its bottom line.
        // Retain the existing, separate fullscreen status-strip frame.
        if (e.ToolStrip is not StatusStrip) return;

        using var pen = new Pen(Color.FromArgb(150, 150, 150));
        var bounds = new Rectangle(Point.Empty, e.ToolStrip.Size);
        bounds.Width--;
        bounds.Height--;
        e.Graphics.DrawRectangle(pen, bounds);
    }

    private sealed class FullScreenToolbarColorTable : ProfessionalColorTable
    {
        private static Color Background => BackgroundColor;

        public override Color ToolStripGradientBegin => Background;
        public override Color ToolStripGradientMiddle => Background;
        public override Color ToolStripGradientEnd => Background;
        public override Color StatusStripGradientBegin => Color.Black;
        public override Color StatusStripGradientEnd => Color.Black;
        public override Color MenuItemSelected => Color.FromArgb(196, 196, 196);
        public override Color ButtonSelectedHighlight => Color.FromArgb(196, 196, 196);
        public override Color ButtonSelectedBorder => Color.FromArgb(150, 150, 150);
        public override Color ButtonPressedHighlight => Color.FromArgb(180, 180, 180);
        public override Color ButtonPressedBorder => Color.FromArgb(135, 135, 135);
        public override Color SeparatorDark => Color.FromArgb(150, 150, 150);
        public override Color SeparatorLight => Color.FromArgb(190, 190, 190);
    }
}
