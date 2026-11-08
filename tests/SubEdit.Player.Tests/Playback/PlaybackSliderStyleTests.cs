using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

[Collection("Subtitle dialog layout")]
public class PlaybackSliderStyleTests
{
    [Theory]
    [InlineData(96, 90, 15, 8, 9, 15)]
    [InlineData(120, 112, 22, 10, 11, 19)]
    [InlineData(120, 112, 18, 10, 11, 19)]
    public void VolumeRasterMatchesSeekMetricsAndPreservesTrackEndpoints(
        int dpi, int width, int height, int trackHeight, int thumbWidth, int thumbHeight) =>
        SubtitleDialogLayoutTests.Sta(() =>
        {
            using var seek = new ChapterSeekBar { Size = new Size(200, 28), Value = 50 };
            using var volume = new VolumeTrackBar { Size = new Size(width, height) };
            using var seekRaster = Paint(seek, dpi);
            var trackBorder = Color.FromArgb(145, 145, 145);
            var thumbBorder = Color.FromArgb(90, 90, 90);
            var progress = Color.FromArgb(179, 197, 210);
            Assert.Equal(trackHeight, FindColor(seekRaster, trackBorder).Height);
            Assert.Equal(new Size(thumbWidth, thumbHeight), FindColor(seekRaster, thumbBorder).Size);
            Assert.False(FindColor(seekRaster, Color.FromArgb(170, 190, 205)).IsEmpty);

            foreach (var value in new[] { 0, 50, 100 })
            {
                volume.Value = value;
                using var raster = Paint(volume, dpi);
                var track = FindColor(raster, trackBorder);
                var thumb = FindColor(raster, thumbBorder);
                var fill = FindColor(raster, progress);
                var inset = dpi == 96 ? 5 : 6;
                Assert.Equal(new Size(thumbWidth, Math.Min(height, thumbHeight)), thumb.Size);
                Assert.Equal(trackHeight, track.Height);
                if (value == 50)
                {
                    Assert.Equal(inset, track.Left);
                    Assert.Equal(width - inset, track.Right - 1);
                }

                if (value == 0)
                {
                    Assert.True(fill.IsEmpty);
                    continue;
                }

                Assert.Equal(new Rectangle(inset + 1, track.Top + 1, thumb.Left - inset - 1, trackHeight - 2), fill);
            }
        });

    private static Bitmap Paint(Control control, int dpi)
    {
        typeof(Control).GetProperty("DeviceDpiInternal", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(control, dpi);
        var raster = new Bitmap(control.Width, control.Height);
        using var graphics = Graphics.FromImage(raster);
        using var args = new PaintEventArgs(graphics, control.ClientRectangle);
        control.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, [args]);
        return raster;
    }

    private static Rectangle FindColor(Bitmap raster, Color color)
    {
        var left = raster.Width;
        var top = raster.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < raster.Height; y++)
        for (var x = 0; x < raster.Width; x++)
        {
            if (raster.GetPixel(x, y).ToArgb() != color.ToArgb()) continue;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }
}
