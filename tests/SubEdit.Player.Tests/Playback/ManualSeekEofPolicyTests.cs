using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class ManualSeekEofPolicyTests
{
    private const long MediaLength = 100_000; // 100 seconds

    [Theory]
    [InlineData("video.m2ts")]
    [InlineData("VIDEO.M2TS")]
    [InlineData("c:\\path\\to\\file.m2ts")]
    public void IsUnsafeManualSeekNearEnd_M2ts_BlocksLastOneSecond(string videoPath)
    {
        // Boundary at length - 1000 ms (99,000 ms):
        Assert.True(Form1.IsUnsafeManualSeekNearEnd(videoPath, 99_000, MediaLength));
        Assert.True(Form1.IsUnsafeManualSeekNearEnd(videoPath, 99_500, MediaLength));
        Assert.True(Form1.IsUnsafeManualSeekNearEnd(videoPath, 100_000, MediaLength));
        Assert.True(Form1.IsUnsafeManualSeekNearEnd(videoPath, 105_000, MediaLength));

        // Just below the 1-second safety window:
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 98_999, MediaLength));
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 50_000, MediaLength));
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 0, MediaLength));
    }

    [Fact]
    public void IsUnsafeManualSeekNearEnd_ShortM2ts_ClampsSafetyWindowToZero()
    {
        const long shortLength = 500; // 500 ms
        // For lengths under 1s, Max(0, 500 - 1000) = 0, so any target >= 0 is guarded
        Assert.True(Form1.IsUnsafeManualSeekNearEnd("clip.m2ts", 0, shortLength));
        Assert.True(Form1.IsUnsafeManualSeekNearEnd("clip.m2ts", 250, shortLength));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mkv")]
    [InlineData("video.avi")]
    [InlineData("video.ts")]
    [InlineData("video.mts")]
    [InlineData("video.mov")]
    [InlineData(null)]
    [InlineData("")]
    public void IsUnsafeManualSeekNearEnd_NonM2ts_DoesNotBlockNearEnd(string? videoPath)
    {
        // Non-M2TS formats rely on NormalizeManualSeekTarget (100 ms clamp) rather than hard block
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 99_500, MediaLength));
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 100_000, MediaLength));
        Assert.False(Form1.IsUnsafeManualSeekNearEnd(videoPath, 105_000, MediaLength));
    }

    [Theory]
    [InlineData("video.mp4")]
    [InlineData("video.mkv")]
    [InlineData("video.avi")]
    [InlineData("video.ts")]
    [InlineData("video.mts")]
    [InlineData(null)]
    public void NormalizeManualSeekTarget_NonM2ts_ReachingOrExceedingDuration_CappedAtLengthMinus100Ms(string? videoPath)
    {
        // Targets at or exceeding Duration are normalized to length - 100 ms
        Assert.Equal(99_900, Form1.NormalizeManualSeekTarget(videoPath, 100_000, MediaLength));
        Assert.Equal(99_900, Form1.NormalizeManualSeekTarget(videoPath, 105_000, MediaLength));

        // Targets below Duration remain untouched
        Assert.Equal(99_900, Form1.NormalizeManualSeekTarget(videoPath, 99_900, MediaLength));
        Assert.Equal(99_899, Form1.NormalizeManualSeekTarget(videoPath, 99_899, MediaLength));
        Assert.Equal(50_000, Form1.NormalizeManualSeekTarget(videoPath, 50_000, MediaLength));
        Assert.Equal(0, Form1.NormalizeManualSeekTarget(videoPath, 0, MediaLength));
    }

    [Fact]
    public void NormalizeManualSeekTarget_M2ts_ReturnsTargetUntouched()
    {
        // For .m2ts, normalization leaves value untouched as IsUnsafeManualSeekNearEnd performs the 1000ms guard
        Assert.Equal(100_000, Form1.NormalizeManualSeekTarget("test.m2ts", 100_000, MediaLength));
        Assert.Equal(99_500, Form1.NormalizeManualSeekTarget("test.m2ts", 99_500, MediaLength));
        Assert.Equal(50_000, Form1.NormalizeManualSeekTarget("test.m2ts", 50_000, MediaLength));
    }

    [Fact]
    public void NormalizeManualSeekTarget_ZeroOrNegativeLength_ClampsToZero()
    {
        Assert.Equal(0, Form1.NormalizeManualSeekTarget("test.mp4", -500, 0));
        Assert.Equal(1000, Form1.NormalizeManualSeekTarget("test.mp4", 1000, 0));
    }
}
