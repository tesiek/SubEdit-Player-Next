using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class TimeCounterTests
{
    [Fact]
    public void GetNextTimeDisplayMode_CyclesElapsedRemainingHiddenElapsed()
    {
        var mode = TimeDisplayMode.Elapsed;
        mode = Form1.GetNextTimeDisplayMode(mode);
        Assert.Equal(TimeDisplayMode.Remaining, mode);
        mode = Form1.GetNextTimeDisplayMode(mode);
        Assert.Equal(TimeDisplayMode.Hidden, mode);
        mode = Form1.GetNextTimeDisplayMode(mode);
        Assert.Equal(TimeDisplayMode.Elapsed, mode);
    }

    [Fact]
    public void FormatTimeCounterText_RemainingSubtractsPositionFromDurationBeforeFormatting()
    {
        Assert.Equal("-1:11:13 / 1:48:25",
            Form1.FormatTimeCounterText(TimeDisplayMode.Remaining, 2_232_000, 6_505_000));
        Assert.Equal("-0:00:01 / 0:00:02",
            Form1.FormatTimeCounterText(TimeDisplayMode.Remaining, 999, 2_000));
    }

    [Theory]
    [InlineData(6_505_000L)]
    [InlineData(6_506_000L)]
    public void FormatTimeCounterText_RemainingClampsToZeroAtOrBeyondEnd(long position)
    {
        Assert.Equal("-0:00:00 / 1:48:25",
            Form1.FormatTimeCounterText(TimeDisplayMode.Remaining, position, 6_505_000));
    }

    [Fact]
    public void FormatTimeCounterText_RemainingHasExactlyOneMinusPrefix()
    {
        var text = Form1.FormatTimeCounterText(TimeDisplayMode.Remaining, 2_232_000, 6_505_000);

        Assert.StartsWith("-", text);
        Assert.Equal(1, text.Count(character => character == '-'));
    }

    [Fact]
    public void FormatTimeCounterText_HiddenReturnsEmptyText()
    {
        Assert.Empty(Form1.FormatTimeCounterText(TimeDisplayMode.Hidden, 2_232_000, 6_505_000));
    }

    [Theory]
    [InlineData(2_232_000L, 6_505_000L, "0:37:12 / 1:48:25")]
    [InlineData(999L, 3_600_999L, "0:00:00 / 1:00:00")]
    [InlineData(-1L, 0L, "0:00:00 / 0:00:00")]
    [InlineData(360_000_000L, 360_000_000L, "100:00:00 / 100:00:00")]
    public void FormatTimeCounterText_ElapsedPreservesExistingFormat(long position, long length, string expected)
    {
        Assert.Equal(expected, Form1.FormatTimeCounterText(TimeDisplayMode.Elapsed, position, length));
    }
}
