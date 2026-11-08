using System.Drawing;
using System.Drawing.Imaging;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class OsdAppearanceRendererTests
{
    [Theory]
    [InlineData(0, 255)]
    [InlineData(25, 191)]
    [InlineData(50, 128)]
    [InlineData(75, 64)]
    [InlineData(100, 0)]
    public void Transparency_MapsDirectlyToExpectedAlpha(int transparency, int alpha)
        => Assert.Equal(alpha, OsdAppearanceRenderer.GetAlpha(transparency));

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(100)]
    public void BackgroundAndBorder_PaintDisjointRegions_WithUnchangedBounds(int transparency)
    {
        foreach (var thickness in new[] { 0, 1, 4, 20 })
        foreach (var useBrushes in new[] { false, true })
        foreach (var swapColors in new[] { false, true })
        {
            using var bitmap = new Bitmap(40, 30, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            var bounds = new Rectangle(5, 7, 24, 16);
            var borderColor = swapColors ? Color.Red : Color.Blue;
            var backgroundColor = swapColors ? Color.Blue : Color.Red;
            using var border = new SolidBrush(borderColor);
            using var background = new SolidBrush(Color.FromArgb(OsdAppearanceRenderer.GetAlpha(transparency), backgroundColor));
            if (useBrushes)
                OsdAppearanceRenderer.DrawBackground(graphics, bounds, border, background, thickness);
            else
                OsdAppearanceRenderer.DrawBackground(graphics, bounds, borderColor, backgroundColor, transparency, thickness);

            var actualThickness = Math.Min(thickness, Math.Min(bounds.Width / 2, bounds.Height / 2));
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (!bounds.Contains(x, y))
                    Assert.Equal(0, pixel.A);
                else if (x < bounds.Left + actualThickness || x >= bounds.Right - actualThickness ||
                         y < bounds.Top + actualThickness || y >= bounds.Bottom - actualThickness)
                    Assert.Equal(borderColor.ToArgb(), pixel.ToArgb());
                else
                {
                    Assert.Equal(OsdAppearanceRenderer.GetAlpha(transparency), pixel.A);
                    if (transparency != 100)
                    {
                        // GDI+ premultiplies and unpremultiplies translucent bitmap colors.
                        Assert.InRange(Math.Abs(backgroundColor.R - pixel.R), 0, 1);
                        Assert.InRange(Math.Abs(backgroundColor.G - pixel.G), 0, 1);
                        Assert.InRange(Math.Abs(backgroundColor.B - pixel.B), 0, 1);
                    }
                }
            }
        }
    }

    [Fact]
    public void TranslucentBorder_IsNotPaintedTwiceAtCorners_AndDoesNotTintInterior()
    {
        using var bitmap = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var border = Color.FromArgb(128, Color.Blue);
        OsdAppearanceRenderer.DrawBackground(graphics, new Rectangle(2, 2, 16, 16), border, Color.Red, 50, 3);
        Assert.Equal(128, bitmap.GetPixel(2, 2).A);
        Assert.Equal(128, bitmap.GetPixel(3, 8).A);
        Assert.Equal(128, bitmap.GetPixel(8, 8).A);
        Assert.Equal(0, bitmap.GetPixel(8, 8).B);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    public void SmallBounds_DoNotPaintOutsideTheRequestedRectangle(int width, int height)
    {
        using var bitmap = new Bitmap(12, 12, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var bounds = new Rectangle(3, 4, width, height);
        OsdAppearanceRenderer.DrawBackground(graphics, bounds, Color.Blue, Color.Red, 0, 10);
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
            Assert.Equal(bounds.Contains(x, y) ? 255 : 0, bitmap.GetPixel(x, y).A);
    }
}
