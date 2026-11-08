using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class OsdDpiIntegrationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(10)]
    [InlineData(18)]
    [InlineData(30)]
    public void InitialRaster_MatchesPreviousRendererPixelForPixel(int points) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var overlay = CreateOverlay(points);
        using var probe = new Bitmap(1, 1);
        foreach (var transparency in new[] { 0, 25, 50, 75, 100 })
        {
            overlay.OsdBackgroundTransparencyPercent = transparency;
            using var actual = Snapshot(overlay, probe.VerticalResolution);
            using var previous = new Bitmap(900, 400, PixelFormat.Format32bppArgb);
            previous.SetResolution(probe.VerticalResolution, probe.VerticalResolution);
            using (var graphics = Graphics.FromImage(previous)) DrawPreviousOsd(graphics, overlay);
            AssertSamePixels(previous, actual);
        }
        output.WriteLine($"Initial raster {probe.VerticalResolution} DPI, {points} pt: 5 transparency levels identical.");
    });

    [Theory]
    [InlineData("time")]
    [InlineData("playback")]
    [InlineData("volume")]
    public void RasterDpi_BothDirectionsAreReversibleAndKeepAnchors(string item) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var overlay = CreateOverlay(18);
        if (item != "time") overlay.OsdText = "";
        if (item != "playback") overlay.PlaybackText = "";
        if (item != "volume") overlay.VolumeText = "";
        using var at96 = Snapshot(overlay, 96);
        using var at120 = Snapshot(overlay, 120);
        using var return96 = Snapshot(overlay, 96);
        using var return120 = Snapshot(overlay, 120);
        AssertSamePixels(at96, return96);
        AssertSamePixels(at120, return120);
        var small = Extent(at96);
        var large = Extent(at120);
        using var previous = new Bitmap(900, 400, PixelFormat.Format32bppArgb);
        previous.SetResolution(96, 96);
        using (var graphics = Graphics.FromImage(previous)) DrawPreviousOsd(graphics, overlay);
        Assert.Equal(Extent(previous), small);
        Assert.True(large.Width > small.Width && large.Height > small.Height);
        Assert.Equal(10, small.Top);
        Assert.Equal(small.Top, large.Top);
        if (item == "time") Assert.Equal(10, large.Left);
        else
        {
            // Logical right edge is 890; antialiased coverage includes x=890.
            // Keep the exact coverage of the pre-change renderer, at both raster DPIs.
            Assert.Equal(891, small.Right);
            Assert.Equal(small.Right, large.Right);
        }
        output.WriteLine($"{item}: 96 DPI {small}; 120 DPI {large}. Raster simulation, not physical monitor QA.");
    });

    [Theory]
    [InlineData(SubtitleFontSizeMode.Constant)]
    [InlineData(SubtitleFontSizeMode.RelativeToWindow)]
    public void SharedBitmapDpi_DoesNotScaleSubtitlePixelsAgain(SubtitleFontSizeMode mode) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var overlay = CreateOverlay(18);
        overlay.OsdText = overlay.PlaybackText = overlay.VolumeText = "";
        overlay.SubtitleFontSizeMode = mode;
        overlay.TransparentSubtitles = false;
        overlay.SubtitleBackgroundTransparencyPercent = 25;
        overlay.SubtitleCue = new SubtitleCue(0, 1000,
            [new SubtitleLine([new SubtitleTextRun("Subtitle", SubtitleTextStyle.Bold, null),
                new SubtitleTextRun(" 18 pt", SubtitleTextStyle.Italic, Color.Yellow, FontSize: 18)])]);
        using var reference = Snapshot(overlay, 96);
        Assert.False(Extent(reference).IsEmpty);
        foreach (var dpi in new[] { 120, 96, 120 })
        {
            using var changed = Snapshot(overlay, dpi);
            AssertSamePixels(reference, changed);
        }
    });

    [Fact]
    public void LayeredRender_RepeatedRefreshAndCloseReleaseGdiResources() => SubtitleDialogLayoutTests.Sta(() =>
    {
        void Cycle()
        {
            using var owner = new Form { ClientSize = new Size(900, 400), ShowInTaskbar = false };
            using var overlay = CreateOverlay(18);
            owner.Show();
            overlay.Bounds = owner.RectangleToScreen(owner.ClientRectangle);
            overlay.Show(owner);
            Assert.True(overlay.IsHandleCreated);
            Assert.Equal(overlay.DeviceDpi, (int)GetDpiForWindow(overlay.Handle));
            for (var n = 0; n < 8; n++) overlay.VolumeText = $"Głośność: {n}%";
            overlay.Close();
            Assert.False(overlay.IsHandleCreated);
            owner.Close();
        }
        Cycle(); Cycle(); // Warm native font/control caches before measuring owned GDI resources.
        var before = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
        for (var n = 0; n < 6; n++) Cycle();
        var after = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
        output.WriteLine($"GDI handles before={before}, after={after}");
        Assert.InRange((int)after - (int)before, -16, 2);
    });

    private static VideoOverlayForm CreateOverlay(int points) => new()
    {
        ClientSize = new Size(900, 400), OsdFontSize = points,
        OsdText = "0:00:01 / 0:50:00", PlaybackText = "Odtwarzanie", VolumeText = "Głośność: 75%",
        OsdBackgroundColor = Color.Red, OsdBorderColor = Color.Blue, OsdTextColor = Color.White,
        OsdBackgroundTransparencyPercent = 50, OsdBorderThickness = 2
    };

    private static Bitmap Snapshot(VideoOverlayForm overlay, float dpi)
    {
        var bitmap = new Bitmap(900, 400, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(dpi, dpi);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        typeof(VideoOverlayForm).GetMethod("DrawOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, [graphics]);
        return bitmap;
    }

    // Frozen pre-2D.13c OSD-only reference: point font, context-free height measurement,
    // and the original three rectangles. This is test code, not an alternate production renderer.
    private static void DrawPreviousOsd(Graphics graphics, VideoOverlayForm overlay)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", overlay.OsdFontSize, FontStyle.Regular);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        using var background = new SolidBrush(Color.FromArgb(OsdAppearanceRenderer.GetAlpha(overlay.OsdBackgroundTransparencyPercent), overlay.OsdBackgroundColor));
        using var border = new SolidBrush(overlay.OsdBorderColor);
        using var foreground = new SolidBrush(overlay.OsdTextColor);
        var rightTop = 10;
        foreach (var (text, left) in new[] { (overlay.OsdText, true), (overlay.PlaybackText, false), (overlay.VolumeText, false) })
        {
            if (string.IsNullOrEmpty(text)) continue;
            var size = TextRenderer.MeasureText(text, font);
            var width = Math.Max(1, (int)Math.Ceiling(graphics.MeasureString(text, font, PointF.Empty, format).Width)) + 20;
            var bounds = new Rectangle(left ? 10 : 900 - 10 - width, left ? 10 : rightTop, width, size.Height + 8);
            OsdAppearanceRenderer.DrawBackground(graphics, bounds, border, background, overlay.OsdBorderThickness);
            graphics.DrawString(text, font, foreground, new PointF(bounds.X + 10, bounds.Y + 4), format);
            if (!left) rightTop = bounds.Bottom + 4;
        }
    }

    private static void AssertSamePixels(Bitmap expected, Bitmap actual)
    {
        Assert.Equal(expected.Size, actual.Size);
        for (var y = 0; y < expected.Height; y++)
        for (var x = 0; x < expected.Width; x++)
            Assert.True(expected.GetPixel(x, y) == actual.GetPixel(x, y), $"Pixel mismatch at ({x},{y})");
    }

    private static Rectangle Extent(Bitmap bitmap)
    {
        var left = bitmap.Width; var right = -1; var top = bitmap.Height; var bottom = -1;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            if (bitmap.GetPixel(x, y).A != 0)
            { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
}
