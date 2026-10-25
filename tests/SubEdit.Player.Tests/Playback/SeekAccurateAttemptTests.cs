using System;
using FlyleafLib.MediaPlayer;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class SeekAccurateAttemptTests
{
    [Fact]
    public void InitialState_HasExpectedDefaults()
    {
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => false,
            RejectOvershoot = false
        };

        Assert.Equal("FAILURE", probe.Result);
        Assert.Null(probe.FirstFramePtsTicks);
        Assert.Null(probe.FinalPtsTicks);
        Assert.Equal(0, probe.HalfFrameTicks);
    }

    [Fact]
    public void ObserveFrame_WhenWithinMargin_RecordsFirstFrameAndReturnsTrue()
    {
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => false,
            RejectOvershoot = true
        };

        const long targetTicks = 10_000_000L;
        const long framePtsTicks = 10_050_000L;
        const long halfFrameTicks = 200_000L;

        var accepted = probe.ObserveFrame(framePtsTicks, targetTicks, halfFrameTicks);

        Assert.True(accepted);
        Assert.Equal(framePtsTicks, probe.FirstFramePtsTicks);
        Assert.Equal(halfFrameTicks, probe.HalfFrameTicks);
        Assert.Equal("FAILURE", probe.Result); // stays FAILURE until Complete()
    }

    [Fact]
    public void ObserveFrame_WhenRejectOvershootTrue_AndPtsExceedsTargetPlusMargin_MarksOvershootAndReturnsFalse()
    {
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => false,
            RejectOvershoot = true
        };

        const long targetTicks = 10_000_000L;
        const long halfFrameTicks = 200_000L;
        const long overshootPtsTicks = targetTicks + halfFrameTicks + 10_000L; // 10,210,000 > 10,200,000

        var accepted = probe.ObserveFrame(overshootPtsTicks, targetTicks, halfFrameTicks);

        Assert.False(accepted);
        Assert.Equal(overshootPtsTicks, probe.FirstFramePtsTicks);
        Assert.Equal("OVERSHOOT", probe.Result);
    }

    [Fact]
    public void ObserveFrame_WhenRejectOvershootFalse_LegacyMode_AcceptsOvershootWithoutMarkingError()
    {
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => false,
            RejectOvershoot = false // Legacy behavior
        };

        const long targetTicks = 10_000_000L;
        const long halfFrameTicks = 200_000L;
        const long overshootPtsTicks = targetTicks + halfFrameTicks + 50_000L;

        var accepted = probe.ObserveFrame(overshootPtsTicks, targetTicks, halfFrameTicks);

        Assert.True(accepted);
        Assert.Equal(overshootPtsTicks, probe.FirstFramePtsTicks);
        Assert.Equal("FAILURE", probe.Result); // not marked OVERSHOOT
    }

    [Fact]
    public void ObserveFrame_WhenCancelled_MarksSupersededAndReturnsFalse()
    {
        var isCancelled = false;
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => isCancelled,
            RejectOvershoot = true
        };

        isCancelled = true;

        var accepted = probe.ObserveFrame(10_000_000L, 10_000_000L, 200_000L);

        Assert.False(accepted);
        Assert.Equal("SUPERSEDED", probe.Result);
    }

    [Fact]
    public void Complete_SetsFinalPtsTicksAndSuccess()
    {
        var probe = new SeekAccurateAttempt
        {
            Cancel = () => false,
            RejectOvershoot = true
        };

        const long finalPts = 10_002_000L;
        probe.Complete(finalPts);

        Assert.Equal("SUCCESS", probe.Result);
        Assert.Equal(finalPts, probe.FinalPtsTicks);
    }

    [Fact]
    public void ConditionalMode_TwoAttemptLifecycle_FallbackFromOvershootToPrerollSuccess()
    {
        // Attempt 1: preroll 0 ms, RejectOvershoot = true
        var attempt1 = new SeekAccurateAttempt
        {
            SeekStartMs = 5000,
            Cancel = () => false,
            RejectOvershoot = true
        };

        const long targetTicks = 50_000_000L;
        const long halfFrameTicks = 160_000L;
        const long overshootPts = targetTicks + halfFrameTicks + 100_000L;

        var attempt1Accepted = attempt1.ObserveFrame(overshootPts, targetTicks, halfFrameTicks);

        Assert.False(attempt1Accepted);
        Assert.Equal("OVERSHOOT", attempt1.Result);
        Assert.Null(attempt1.FinalPtsTicks);

        // Conditional policy triggers Attempt 2: preroll 3000 ms, RejectOvershoot = false
        var attempt2 = new SeekAccurateAttempt
        {
            SeekStartMs = 2000, // 5000 - 3000 ms preroll
            Cancel = () => false,
            RejectOvershoot = false
        };

        const long firstPtsAttempt2 = 20_000_000L;
        var attempt2Accepted = attempt2.ObserveFrame(firstPtsAttempt2, targetTicks, halfFrameTicks);

        Assert.True(attempt2Accepted);
        Assert.Equal(firstPtsAttempt2, attempt2.FirstFramePtsTicks);

        // Decoder feeds frames until accurate frame is reached
        const long accurateFramePts = 50_000_000L;
        attempt2.Complete(accurateFramePts);

        Assert.Equal("SUCCESS", attempt2.Result);
        Assert.Equal(accurateFramePts, attempt2.FinalPtsTicks);
    }

    [Fact]
    public void SeekData_Constructor_DefaultAccurateMode_IsLegacy()
    {
        // Verifies C09: SeekData default accurateMode aligns with Player default (Legacy)
        var seekData = new SeekData(ms: 10_000, forward: false, accurate: true);

        Assert.Equal(SeekAccurateMode.Legacy, seekData.accurateMode);
        Assert.True(seekData.accurate);
        Assert.False(seekData.forward);
        Assert.Equal(10_000, seekData.ms);
    }
}
