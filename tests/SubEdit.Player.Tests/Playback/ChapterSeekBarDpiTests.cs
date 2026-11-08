using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

[Collection("Subtitle dialog layout")]
public class ChapterSeekBarDpiTests
{
    // Exercises metric logic only; changing this field does not simulate native PMv2.
    private static void SetDpi(ChapterSeekBar bar, int dpi) =>
        typeof(Control).GetProperty("DeviceDpiInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(bar, dpi);

    private static object? Call(ChapterSeekBar bar, string name, params object[] args) =>
        typeof(ChapterSeekBar).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(bar, args);

    [Theory]
    [InlineData(22)]
    [InlineData(28)]
    public void Metrics_RoundTripFitHeight_AndKeepHorizontalTrack(int height) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var bar = new ChapterSeekBar { Width = 900, Height = height, Maximum = 1000 };
        foreach (var dpi in new[] { 96, 120, 96, 120, 96, 120 })
        {
            SetDpi(bar, dpi);
            var track = (Rectangle)Call(bar, "GetTrackRectangle")!;
            Assert.Equal(9, track.Left);
            Assert.Equal(882, track.Width);
            Assert.Equal(dpi == 96 ? 7 : 9, track.Height);
            Assert.Equal(dpi == 96 ? 895 : 896, bar.MaximumThumbRightPixel);
            var thumbHeight = dpi == 96 ? 14 : 18;
            Assert.InRange((height - thumbHeight) / 2 + thumbHeight, 0, height - 1);
            var markerExtension = dpi == 96 ? 3 : 4;
            Assert.True(track.Top - markerExtension >= 0);
            Assert.True(track.Bottom + markerExtension < height);
            Assert.Equal(height, bar.Height);
        }
    });

    [Fact]
    public void DpiDoesNotChangeMouseMappingOrInteraction() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var bar = new ChapterSeekBar { Width = 900, Height = 22, Maximum = 1000 };
        var commits = 0;
        bar.SeekCommitted += (_, _) => commits++;
        foreach (var dpi in new[] { 96, 120, 96 })
        {
            SetDpi(bar, dpi);
            for (var x = -10; x <= 910; x++)
            {
                Call(bar, "SetValueFromX", x);
                Assert.Equal((int)Math.Round(1000 * Math.Clamp((x - 9d) / 882d, 0, 1)), bar.Value);
            }
            bar.SimulateMouseDown(9, 10);
            Assert.Equal(0, bar.IntendedValue);
            bar.SimulateMouseMove(891, 10);
            Assert.Equal(1000, bar.IntendedValue);
            bar.SimulateCaptureLoss();
            Assert.Equal(SeekBarInteractionState.Idle, bar.InteractionState);
        }
        Assert.Equal(3, commits);
    });
}
