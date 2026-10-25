using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace SubEdit.Player.WinForms.Controls;

internal enum EditorToolbarIcon
{
    OpenVideo, OpenSubtitles, New, Save, Undo, Redo, Editor, Find, ActiveFormat,
    SubtitleTrace, Convert, ShiftTime, DisplayTime, TransparentSubtitles,
    ShowSubtitles, AlwaysOnTop, Fullscreen, Options, Close
}

// Newly authored geometric symbols. Classic folder/page/ABC motifs are retained,
// without sampling, tracing or embedding any historical sprite pixels.
internal static class EditorToolbarIcons
{
    internal static void Draw(Graphics graphics, EditorToolbarIcon icon, RectangleF bounds, bool enabled, bool active)
    {
        var state = graphics.Save();
        try
        {
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Height / 20f, bounds.Height / 20f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var ink = !enabled ? PlaybackToolbarIcons.Disabled : active ? PlaybackToolbarIcons.Active : PlaybackToolbarIcons.Normal;
            Color Accent(Color value) => !enabled ? PlaybackToolbarIcons.Disabled : active ? PlaybackToolbarIcons.Active : value;
            using var pen = new Pen(ink, 1.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var blue = new Pen(Accent(Color.FromArgb(0x00, 0x68, 0xD7)), 1.5f) { LineJoin = LineJoin.Round };
            using var green = new Pen(Accent(Color.FromArgb(42, 143, 82)), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var red = new Pen(Accent(Color.FromArgb(199, 66, 63)), 1.5f);
            using var paper = new SolidBrush(enabled ? Color.FromArgb(249, 251, 253) : Color.FromArgb(231, 235, 238));
            using var inkBrush = new SolidBrush(ink);
            void Page()
            {
                graphics.FillPolygon(paper, [new PointF(4, 2), new PointF(12, 2), new PointF(16, 6), new PointF(16, 18), new PointF(4, 18)]);
                graphics.DrawLines(pen, [new PointF(4, 18), new PointF(4, 2), new PointF(12, 2), new PointF(16, 6), new PointF(16, 18), new PointF(4, 18)]);
                graphics.DrawLines(blue, [new PointF(12, 2), new PointF(12, 6), new PointF(16, 6)]);
            }
            void Abc(float y, Color color, float size = 7)
            {
                using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(color);
                using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                graphics.DrawString("ABC", font, brush, new RectangleF(1, y, 18, 8), format);
            }
            void Clock()
            {
                graphics.FillEllipse(paper, 9, 9, 9, 9);
                graphics.DrawEllipse(blue, 9, 9, 9, 9);
                graphics.DrawLines(pen, [new PointF(13.5f, 10.5f), new PointF(13.5f, 13.5f), new PointF(16, 14.5f)]);
            }
            switch (icon)
            {
                case EditorToolbarIcon.OpenVideo:
                case EditorToolbarIcon.OpenSubtitles:
                    using (var folder = new SolidBrush(enabled ? Color.FromArgb(247, 210, 115) : Color.FromArgb(212, 219, 224)))
                    {
                        graphics.FillPolygon(folder, [new PointF(2, 6), new PointF(2, 3), new PointF(8, 3), new PointF(10, 6), new PointF(17, 6), new PointF(18, 16), new PointF(2, 16)]);
                        graphics.DrawPolygon(pen, [new PointF(2, 6), new PointF(2, 3), new PointF(8, 3), new PointF(10, 6), new PointF(17, 6), new PointF(18, 16), new PointF(2, 16)]);
                        graphics.DrawLine(pen, 2, 7, 17, 7);
                    }
                    if (icon == EditorToolbarIcon.OpenVideo)
                    {
                        using var play = new SolidBrush(blue.Color);
                        graphics.FillPolygon(play, [new PointF(8, 9), new PointF(13, 11.5f), new PointF(8, 14)]);
                    }
                    else
                    {
                        graphics.FillRectangle(paper, 8, 9, 8, 9);
                        graphics.DrawRectangle(blue, 8, 9, 8, 9);
                        graphics.DrawLine(blue, 10, 12, 14, 12);
                        graphics.DrawLine(blue, 10, 15, 14, 15);
                    }
                    break;
                case EditorToolbarIcon.New:
                    Page();
                    break;
                case EditorToolbarIcon.Save:
                    using (var body = new SolidBrush(Accent(Color.FromArgb(66, 153, 97)))) graphics.FillRectangle(body, 3, 2, 14, 16);
                    graphics.DrawRectangle(pen, 3, 2, 14, 16);
                    graphics.FillRectangle(paper, 6, 3, 8, 5);
                    graphics.FillRectangle(paper, 6, 11, 8, 7);
                    graphics.DrawLine(pen, 12, 4, 12, 7);
                    graphics.DrawLine(pen, 8, 14, 12, 14);
                    break;
                case EditorToolbarIcon.Undo:
                case EditorToolbarIcon.Redo:
                    if (icon == EditorToolbarIcon.Redo) { graphics.TranslateTransform(20, 0); graphics.ScaleTransform(-1, 1); }
                    graphics.DrawBezier(green, new PointF(17, 16), new PointF(21, 4), new PointF(6, 2), new PointF(4, 9));
                    using (var arrow = new SolidBrush(green.Color)) graphics.FillPolygon(arrow, [new PointF(2, 8), new PointF(8, 5), new PointF(7, 12)]);
                    break;
                case EditorToolbarIcon.Editor:
                    graphics.FillRectangle(paper, 2, 3, 16, 14);
                    graphics.DrawRectangle(pen, 2, 3, 16, 14);
                    graphics.DrawLine(blue, 2, 6, 18, 6);
                    graphics.DrawLine(green, 5, 8, 5, 15);
                    graphics.DrawLine(pen, 8, 9, 15, 9);
                    graphics.DrawLine(pen, 8, 12, 15, 12);
                    graphics.DrawLine(pen, 8, 15, 12, 15);
                    break;
                case EditorToolbarIcon.Find:
                    using (var lens = new SolidBrush(enabled ? Color.FromArgb(215, 235, 252) : Color.FromArgb(229, 232, 235))) graphics.FillEllipse(lens, 3, 2, 10, 10);
                    graphics.DrawEllipse(blue, 3, 2, 10, 10);
                    using (var handle = new Pen(ink, 2.8f) { EndCap = LineCap.Round }) graphics.DrawLine(handle, 12, 11, 17, 17);
                    break;
                case EditorToolbarIcon.ActiveFormat:
                    graphics.FillRectangle(paper, 2, 3, 16, 14);
                    graphics.DrawRectangle(blue, 2, 3, 16, 14);
                    Abc(5, blue.Color);
                    graphics.DrawLine(green, 5, 15, 15, 15);
                    break;
                case EditorToolbarIcon.SubtitleTrace:
                    Abc(1, blue.Color);
                    graphics.DrawLine(pen, 3, 12, 12, 12);
                    graphics.DrawLine(pen, 3, 16, 12, 16);
                    graphics.DrawLine(green, 16, 9, 16, 17);
                    graphics.DrawLines(green, [new PointF(13, 14), new PointF(16, 17), new PointF(19, 14)]);
                    break;
                case EditorToolbarIcon.Convert:
                    Abc(0, blue.Color, 6.5f);
                    Abc(11, green.Color, 6.5f);
                    graphics.DrawLine(red, 4, 9, 16, 9);
                    graphics.DrawLines(red, [new PointF(13, 7), new PointF(16, 9), new PointF(13, 11)]);
                    break;
                case EditorToolbarIcon.ShiftTime:
                    Abc(0, blue.Color);
                    Clock();
                    graphics.DrawLine(green, 2, 12, 7, 12);
                    graphics.DrawLine(green, 4.5f, 9.5f, 4.5f, 14.5f);
                    break;
                case EditorToolbarIcon.DisplayTime:
                    Abc(0, blue.Color);
                    Clock();
                    graphics.DrawLine(red, 2, 12, 7, 12);
                    graphics.DrawLines(red, [new PointF(4, 10), new PointF(2, 12), new PointF(4, 14)]);
                    graphics.DrawLines(red, [new PointF(5, 10), new PointF(7, 12), new PointF(5, 14)]);
                    break;
                case EditorToolbarIcon.TransparentSubtitles:
                    using (var tile = new SolidBrush(enabled ? Color.FromArgb(211, 221, 231) : Color.FromArgb(224, 228, 232)))
                        foreach (var (x, y) in new[] { (2, 3), (10, 3), (6, 7), (14, 7), (2, 11), (10, 11) }) graphics.FillRectangle(tile, x, y, 4, 4);
                    Abc(6, blue.Color);
                    break;
                case EditorToolbarIcon.ShowSubtitles:
                    graphics.FillRectangle(paper, 2, 3, 16, 12);
                    graphics.DrawRectangle(blue, 2, 3, 16, 12);
                    graphics.DrawLines(blue, [new PointF(5, 15), new PointF(5, 18), new PointF(9, 15)]);
                    Abc(5, blue.Color);
                    break;
                case EditorToolbarIcon.AlwaysOnTop:
                    graphics.DrawRectangle(pen, 2, 3, 13, 13);
                    graphics.DrawLine(pen, 2, 6, 15, 6);
                    using (var pin = new SolidBrush(blue.Color)) graphics.FillPolygon(pin,
                        [new PointF(12, 3), new PointF(18, 3), new PointF(17, 5), new PointF(17, 9), new PointF(19, 11), new PointF(11, 11), new PointF(13, 9), new PointF(13, 5)]);
                    graphics.DrawLine(blue, 15, 11, 15, 18);
                    break;
                case EditorToolbarIcon.Fullscreen:
                    foreach (var (x, y, dx, dy) in new[] { (3, 3, 1, 1), (17, 3, -1, 1), (3, 17, 1, -1), (17, 17, -1, -1) })
                        graphics.DrawLines(blue, [new PointF(x + dx * 5, y), new PointF(x, y), new PointF(x, y + dy * 5)]);
                    break;
                case EditorToolbarIcon.Options:
                    using (var gear = new GraphicsPath(FillMode.Alternate))
                    {
                        var points = Enumerable.Range(0, 48).Select(i =>
                        {
                            var angle = i * Math.PI / 24;
                            var radius = i % 4 is 0 or 3 ? 6.8 : 8.4;
                            return new PointF(10 + (float)(Math.Cos(angle) * radius), 10 + (float)(Math.Sin(angle) * radius));
                        }).ToArray();
                        gear.AddPolygon(points);
                        gear.AddEllipse(7, 7, 6, 6);
                        graphics.FillPath(inkBrush, gear);
                    }
                    break;
                case EditorToolbarIcon.Close:
                    using (var cross = new Pen(ink, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        graphics.DrawLine(cross, 4, 4, 16, 16);
                        graphics.DrawLine(cross, 16, 4, 4, 16);
                    }
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(icon));
            }
        }
        finally { graphics.Restore(state); }
    }
}
