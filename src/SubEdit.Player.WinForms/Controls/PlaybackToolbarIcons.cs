using System.Drawing.Drawing2D;

namespace SubEdit.Player.WinForms.Controls;

internal enum PlaybackToolbarCommand
{
    Play, Pause, Stop, SeekBackward, SeekForward, PreviousFile, NextFile,
    PlayDirectory, Subtitles, Audio, Video, Chapters, Volume
}

// Original vector geometry in a 20 logical-pixel canvas, with six extra pixels for menus.
// Symbols have no dependency on button order, playback state or historical sprite sheets.
internal static class PlaybackToolbarIcons
{
    internal static readonly Color Normal = Color.FromArgb(0x30, 0x42, 0x4D);
    internal static readonly Color Active = Color.FromArgb(0x00, 0x68, 0xD7);
    internal static readonly Color Disabled = Color.FromArgb(0x98, 0xA2, 0xA9);
    internal static readonly Color ActiveBackground = Color.FromArgb(0xDC, 0xEB, 0xFF);

    internal static bool HasMenu(PlaybackToolbarCommand command) => command is
        PlaybackToolbarCommand.Subtitles or PlaybackToolbarCommand.Audio or
        PlaybackToolbarCommand.Video or PlaybackToolbarCommand.Chapters;

    internal static Size ButtonSize(PlaybackToolbarCommand command, int dpi) => new(
        Scale(HasMenu(command) ? 35 : 29, dpi), Scale(29, dpi));

    internal static int Scale(int logicalPixels, int dpi) =>
        Math.Max(1, (int)Math.Round(logicalPixels * dpi / 96d));

    internal static void Draw(Graphics graphics, PlaybackToolbarCommand command,
        RectangleF bounds, Color color, bool muted)
    {
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Height / 20f, bounds.Height / 20f);
            using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);
            void Triangle(float tip, float edge) => graphics.FillPolygon(brush,
                [new PointF(edge, 4), new PointF(tip, 10), new PointF(edge, 16)]);

            switch (command)
            {
                case PlaybackToolbarCommand.Play:
                    Triangle(16, 6);
                    break;
                case PlaybackToolbarCommand.Pause:
                    graphics.FillRectangle(brush, 5, 4, 3.5f, 12);
                    graphics.FillRectangle(brush, 11.5f, 4, 3.5f, 12);
                    break;
                case PlaybackToolbarCommand.Stop:
                    graphics.FillRectangle(brush, 5, 5, 10, 10);
                    break;
                case PlaybackToolbarCommand.SeekBackward:
                    Triangle(2.5f, 10);
                    Triangle(10, 17.5f);
                    break;
                case PlaybackToolbarCommand.SeekForward:
                    Triangle(10, 2.5f);
                    Triangle(17.5f, 10);
                    break;
                case PlaybackToolbarCommand.PreviousFile:
                    graphics.FillRectangle(brush, 3, 4, 2, 12);
                    Triangle(6, 16);
                    break;
                case PlaybackToolbarCommand.NextFile:
                    Triangle(14, 4);
                    graphics.FillRectangle(brush, 15, 4, 2, 12);
                    break;
                case PlaybackToolbarCommand.PlayDirectory:
                    graphics.DrawLines(pen, [new PointF(2, 16), new PointF(2, 4), new PointF(8, 4),
                        new PointF(10, 6), new PointF(18, 6), new PointF(18, 16), new PointF(2, 16)]);
                    graphics.FillPolygon(brush, [new PointF(8, 8), new PointF(14, 11), new PointF(8, 14)]);
                    break;
                case PlaybackToolbarCommand.Video:
                    graphics.DrawRectangle(pen, 2, 4, 16, 12);
                    graphics.FillPolygon(brush, [new PointF(8, 7), new PointF(13, 10), new PointF(8, 13)]);
                    break;
                case PlaybackToolbarCommand.Audio:
                    // A centered audio signal: symmetric amplitudes, no slider handles.
                    foreach (var (x, halfHeight) in new[] { (3, 2), (6, 5), (9, 7), (12, 4), (15, 6), (18, 2) })
                        graphics.DrawLine(pen, x, 10 - halfHeight, x, 10 + halfHeight);
                    break;
                case PlaybackToolbarCommand.Subtitles:
                    graphics.DrawRectangle(pen, 2, 3, 16, 14);
                    graphics.DrawArc(pen, 4, 6, 5, 8, 50, 260);
                    graphics.DrawArc(pen, 11, 6, 5, 8, 50, 260);
                    break;
                case PlaybackToolbarCommand.Chapters:
                    graphics.DrawRectangle(pen, 2, 3, 16, 14);
                    graphics.DrawLine(pen, 6, 3, 6, 17);
                    graphics.DrawLine(pen, 14, 3, 14, 17);
                    graphics.DrawLine(pen, 6, 10, 14, 10);
                    foreach (var y in new[] { 5, 9, 13 })
                    {
                        graphics.FillRectangle(brush, 3, y, 2, 2);
                        graphics.FillRectangle(brush, 15, y, 2, 2);
                    }
                    break;
                case PlaybackToolbarCommand.Volume:
                    graphics.FillPolygon(brush, [new PointF(2, 8), new PointF(6, 8), new PointF(11, 4),
                        new PointF(11, 16), new PointF(6, 12), new PointF(2, 12)]);
                    if (muted)
                    {
                        using var slash = new Pen(color == Disabled ? Disabled : Color.FromArgb(0xCB, 0x38, 0x42), 1.9f)
                            { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        graphics.DrawLine(slash, 3, 3, 17, 17);
                    }
                    else
                    {
                        graphics.DrawArc(pen, 10, 6, 5, 8, -55, 110);
                        graphics.DrawArc(pen, 10, 3, 8, 14, -55, 110);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }

            if (HasMenu(command))
                graphics.FillPolygon(brush, [new PointF(21, 9), new PointF(26, 9), new PointF(23.5f, 12)]);
        }
        finally { graphics.Restore(state); }
    }
}
