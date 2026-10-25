using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class RelativeSeekQueueTests
{
    private const long MediaLength = 300_000; // 5 minutes

    [Fact]
    public void Enqueue_PreciseForward_SetsPreciseModeAndForwardDirection()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 5_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(5_000, delta);
        Assert.Equal(65_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
        Assert.Equal(1_000, queue.LastInputTimestamp);
        Assert.True(queue.HasPendingInput);
    }

    [Fact]
    public void Enqueue_PreciseBackward_SetsPreciseModeAndBackwardDirection()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: -5_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(-5_000, delta);
        Assert.Equal(55_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_FastMode_SetsFastMode()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 5_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Fast,
            currentTimestamp: 1_000);

        Assert.Equal(5_000, delta);
        Assert.Equal(65_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Fast, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_RapidTaps_AccumulatesLogicalTarget()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 100_000L;

        // 3 rapid +5s taps
        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.Equal(105_000, queue.QueuedTargetPosition);

        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.Equal(110_000, queue.QueuedTargetPosition);

        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);
        Assert.Equal(115_000, queue.QueuedTargetPosition);
    }

    [Fact]
    public void Enqueue_RapidBackwardTaps_AccumulatesLogicalTarget()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 100_000L;

        // 3 rapid -5s taps
        queue.Enqueue(-5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.Equal(95_000, queue.QueuedTargetPosition);

        queue.Enqueue(-5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.Equal(90_000, queue.QueuedTargetPosition);

        queue.Enqueue(-5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);
        Assert.Equal(85_000, queue.QueuedTargetPosition);
    }

    [Fact]
    public void Enqueue_DirectionChange_AccumulatesCorrectNetTarget()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 100_000L;

        // +5, +5, -5 => net +5s
        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        queue.Enqueue(-5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);

        Assert.Equal(105_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_NearBOF_ClampsToZero()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: -5_000,
            currentPosition: 2_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(-2_000, delta);
        Assert.Equal(0, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
    }

    [Fact]
    public void Enqueue_NearEOF_ClampsToMediaLength()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 5_000,
            currentPosition: MediaLength - 2_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(2_000, delta);
        Assert.Equal(MediaLength, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
    }

    [Fact]
    public void Enqueue_ZeroMediaLength_ReturnsZeroWithoutModifyingQueue()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 5_000,
            currentPosition: 0,
            mediaLength: 0,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(0, delta);
        Assert.Null(queue.QueuedTargetPosition);
        Assert.False(queue.HasPendingInput);
    }

    [Fact]
    public void TryGetDispatchAction_ThrottlingAndDispatchBehavior()
    {
        var queue = new RelativeSeekQueue();

        // 1st input at t=1000
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        // Immediate dispatch at t=1000
        var canDispatch1 = queue.TryGetDispatchAction(1_000, out var target1, out var fwd1, out var mode1);
        Assert.True(canDispatch1);
        Assert.Equal(55_000, target1);
        Assert.True(fwd1);
        Assert.Equal(RelativeSeekMode.Precise, mode1);

        // 2nd input at t=1040 (within 150ms throttle window)
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_040);
        var canDispatch2 = queue.TryGetDispatchAction(1_040, out _, out _, out _);
        Assert.False(canDispatch2);

        // 3rd input at t=1080
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_080);
        var canDispatch3 = queue.TryGetDispatchAction(1_080, out _, out _, out _);
        Assert.False(canDispatch3);

        // At t=1150 (exactly 150ms after first dispatch) -> should dispatch final accumulated target (65_000)
        var canDispatchFinal = queue.TryGetDispatchAction(1_150, out var targetFinal, out var fwdFinal, out var modeFinal);
        Assert.True(canDispatchFinal);
        Assert.Equal(65_000, targetFinal);
        Assert.True(fwdFinal);
        Assert.Equal(RelativeSeekMode.Precise, modeFinal);

        // No new inputs, calling TryGetDispatchAction again returns false (target already applied)
        var canDispatchAgain = queue.TryGetDispatchAction(1_160, out _, out _, out _);
        Assert.False(canDispatchAgain);
    }

    [Fact]
    public void TryGetDispatchAction_ModeSwitch_DispatchesEvenWithSameTarget()
    {
        var queue = new RelativeSeekQueue();

        // 1st dispatch: FAST seek to 55,000 at t=1000
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Fast, 1_000);
        queue.TryGetDispatchAction(1_000, out _, out _, out _);

        // Queue expires at t=1200
        queue.CheckExpiration(1_200);

        // User now presses PRECISE seek to 55,000 at t=1300
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_300);
        var canDispatch = queue.TryGetDispatchAction(1_300, out var target, out _, out var mode);

        Assert.True(canDispatch);
        Assert.Equal(55_000, target);
        Assert.Equal(RelativeSeekMode.Precise, mode);
    }

    [Fact]
    public void CheckExpiration_ResetsQueueAfterTimeoutWhenApplied()
    {
        var queue = new RelativeSeekQueue();
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.TryGetDispatchAction(1_000, out _, out _, out _);

        // Before 120ms expiration threshold (e.g. at 1100) -> not expired
        queue.CheckExpiration(1_100);
        Assert.True(queue.HasPendingInput);

        // After 120ms expiration threshold (e.g. at 1150) -> expired and reset
        queue.CheckExpiration(1_150);
        Assert.False(queue.HasPendingInput);
        Assert.Null(queue.QueuedTargetPosition);
        Assert.Null(queue.LastAppliedTargetPosition);
    }

    [Fact]
    public void Reset_ClearsAllPendingAndAppliedState()
    {
        var queue = new RelativeSeekQueue();
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.TryGetDispatchAction(1_000, out _, out _, out _);

        queue.Reset();

        Assert.False(queue.HasPendingInput);
        Assert.Null(queue.QueuedTargetPosition);
        Assert.Null(queue.QueuedForward);
        Assert.Null(queue.QueuedMode);
        Assert.Null(queue.LastAppliedTargetPosition);
        Assert.Null(queue.LastAppliedForward);
        Assert.Null(queue.LastAppliedMode);
    }

    [Fact]
    public void Enqueue_UserDefined_1SecondForward_Calculates1000MsAndPreciseMode()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 1_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(1_000, delta);
        Assert.Equal(61_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_UserDefined_3SecondsBackward_Calculates3000MsAndPreciseMode()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: -3_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(-3_000, delta);
        Assert.Equal(57_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_UserDefined_Rapid1SecondTaps_AccumulatesTo3000Ms()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.Equal(61_000, queue.QueuedTargetPosition);

        queue.Enqueue(1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.Equal(62_000, queue.QueuedTargetPosition);

        queue.Enqueue(1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);
        Assert.Equal(63_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
    }

    [Fact]
    public void Enqueue_UserDefined_Rapid1SecondDirectionReversal_AccumulatesTo1000Ms()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        queue.Enqueue(-1_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);

        Assert.Equal(61_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
    }

    [Fact]
    public void Enqueue_UserDefined_AsymmetricOffsets_Forward7sBackward3s_AccumulatesNet11sAndBackwardDirection()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(7_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(7_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        queue.Enqueue(-3_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);

        Assert.Equal(71_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_UserDefined_AsymmetricOffsets_Backward3sBackward3sForward7s_AccumulatesNet1s()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(-3_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(-3_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        queue.Enqueue(7_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);

        Assert.Equal(61_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_UserDefined_60Seconds_Calculates60000MsAndPreciseMode()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(
            offset: 60_000,
            currentPosition: 60_000,
            mediaLength: MediaLength,
            mode: RelativeSeekMode.Precise,
            currentTimestamp: 1_000);

        Assert.Equal(60_000, delta);
        Assert.Equal(120_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_PlayerOnly_LeftArrow_CalculatesPreciseMinus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(-5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(-5_000, delta);
        Assert.Equal(55_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_PlayerOnly_RightArrow_CalculatesPrecisePlus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(5_000, delta);
        Assert.Equal(65_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_PlayerOnly_DownArrow_CalculatesPreciseMinus60s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(-60_000, 120_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(-60_000, delta);
        Assert.Equal(60_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_PlayerOnly_UpArrow_CalculatesPrecisePlus60s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(60_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(60_000, delta);
        Assert.Equal(120_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_NumPad1_CalculatesPreciseMinus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(-5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(-5_000, delta);
        Assert.Equal(55_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_NumPad3_CalculatesPrecisePlus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(5_000, delta);
        Assert.Equal(65_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_Toolbar_Backward_CalculatesPreciseMinus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(-5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(-5_000, delta);
        Assert.Equal(55_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_Toolbar_Forward_CalculatesPrecisePlus5s()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(5_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(5_000, delta);
        Assert.Equal(65_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_Rapid_UpArrow60s_AccumulatesTo180s()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.Equal(120_000, queue.QueuedTargetPosition);

        queue.Enqueue(60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.Equal(180_000, queue.QueuedTargetPosition);

        queue.Enqueue(60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);
        Assert.Equal(240_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_Rapid_DownArrow60s_AccumulatesToMinus180s()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 240_000L;

        queue.Enqueue(-60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.Equal(180_000, queue.QueuedTargetPosition);

        queue.Enqueue(-60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.Equal(120_000, queue.QueuedTargetPosition);

        queue.Enqueue(-60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);
        Assert.Equal(60_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_DirectionReversal_60s_UpUpDown_AccumulatesToNet60s()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        queue.Enqueue(60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);
        queue.Enqueue(-60_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_080);

        Assert.Equal(120_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_MouseWheel_UsesPreciseMode()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(10_000, 60_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(10_000, delta);
        Assert.Equal(70_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);
    }

    [Fact]
    public void Enqueue_MixedPreciseInputs_WheelAndKeyboard_AccumulatesCorrectly()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 100_000L;

        // Wheel forward +10s (delta=120)
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        // Ctrl+] keyboard forward +5s
        queue.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);

        Assert.Equal(115_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);

        // Reverse order test on fresh queue
        var queue2 = new RelativeSeekQueue();
        // Ctrl+] keyboard forward +5s
        queue2.Enqueue(5_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        // Wheel forward +10s (delta=120)
        queue2.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_040);

        Assert.Equal(115_000, queue2.QueuedTargetPosition);
        Assert.True(queue2.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue2.QueuedMode);
    }

    [Fact]
    public void Enqueue_RapidWheelSteps_AccumulateAndReverseCorrectly()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 60_000L;

        // +120 x 3 = +30s
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_030);
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 1_060);

        Assert.Equal(90_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);

        // Reset and test -120 x 3 = -30s
        queue.Reset();
        queue.Enqueue(-10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 2_000);
        queue.Enqueue(-10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 2_030);
        queue.Enqueue(-10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 2_060);

        Assert.Equal(30_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);

        // Reset and test +120, +120, -120 = +10s net
        queue.Reset();
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 3_000);
        queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 3_030);
        queue.Enqueue(-10_000, initialPos, MediaLength, RelativeSeekMode.Precise, 3_060);

        Assert.Equal(70_000, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
    }

    [Fact]
    public void Enqueue_HighFrequency_25WheelSteps_TargetPreservedAndThrottled()
    {
        var queue = new RelativeSeekQueue();
        var initialPos = 30_000L;
        var dispatchCount = 0;

        for (int i = 0; i < 25; i++)
        {
            var now = 1_000L + (i * 10L); // 25 steps across 240 ms
            queue.Enqueue(10_000, initialPos, MediaLength, RelativeSeekMode.Precise, now);
            if (queue.TryGetDispatchAction(now, out _, out _, out _))
            {
                dispatchCount++;
            }
        }

        // 25 steps * 10,000 = 250,000 + 30,000 = 280,000
        Assert.Equal(280_000, queue.QueuedTargetPosition);
        Assert.True(queue.QueuedForward);
        Assert.Equal(RelativeSeekMode.Precise, queue.QueuedMode);

        // Dispatches must be throttled (at most 2 in 240ms with 150ms throttle)
        Assert.True(dispatchCount <= 2);

        // Final dispatch after throttle expires must reach exact target
        var finalTime = 1_000L + 240L + RelativeSeekQueue.ThrottleIntervalMilliseconds;
        var hasFinal = queue.TryGetDispatchAction(finalTime, out var finalTarget, out var finalForward, out var finalMode);

        Assert.True(hasFinal);
        Assert.Equal(280_000, finalTarget);
        Assert.True(finalForward);
        Assert.Equal(RelativeSeekMode.Precise, finalMode);
    }

    [Fact]
    public void Enqueue_BOF_DownArrow60s_NearBOF_ClampsToZero()
    {
        var queue = new RelativeSeekQueue();
        var delta = queue.Enqueue(-60_000, 30_000, MediaLength, RelativeSeekMode.Precise, 1_000);

        Assert.Equal(-30_000, delta);
        Assert.Equal(0, queue.QueuedTargetPosition);
        Assert.False(queue.QueuedForward);
    }

    [Fact]
    public void TryGetDispatchAction_SameTargetPosition_DifferentForwardDirection_DispatchesActionWithNewDirection()
    {
        var queue = new RelativeSeekQueue();

        // 1st seek: backward to 45,000 at t=1000
        queue.Enqueue(-5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_000);
        var dispatched1 = queue.TryGetDispatchAction(1_000, out var target1, out var forward1, out _);
        Assert.True(dispatched1);
        Assert.Equal(45_000, target1);
        Assert.False(forward1);

        // Queue expires and is reset
        queue.CheckExpiration(1_200);
        Assert.False(queue.HasPendingInput);

        // 2nd seek: forward to 45,000 from base 40,000 at t=1300
        queue.Enqueue(5_000, 40_000, MediaLength, RelativeSeekMode.Precise, 1_300);
        var dispatched2 = queue.TryGetDispatchAction(1_300, out var target2, out var forward2, out var mode2);

        // State change must be detected because direction changed (forward == true vs previous false)
        Assert.True(dispatched2);
        Assert.Equal(45_000, target2);
        Assert.True(forward2);
        Assert.Equal(RelativeSeekMode.Precise, mode2);
    }

    [Fact]
    public void CheckExpiration_WhenExpiryElapsedBeforeThrottleAllowsDispatch_PreservesPendingTargetForSubsequentDispatch()
    {
        var queue = new RelativeSeekQueue();

        // Step 1: Input A at t=1000 is dispatched immediately
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_000);
        var dispatchedA = queue.TryGetDispatchAction(1_000, out var targetA, out _, out _);
        Assert.True(dispatchedA);
        Assert.Equal(55_000, targetA);

        // Step 2: Input B arrives at t=1050 (within 150ms throttle window, stays pending)
        queue.Enqueue(5_000, 50_000, MediaLength, RelativeSeekMode.Precise, 1_050);
        Assert.True(queue.HasPendingInput);
        Assert.Equal(60_000, queue.QueuedTargetPosition);

        // Step 3: At t=1180, 130ms have passed since LastInputTimestamp (1050),
        // which exceeds ExpiryTimeoutMilliseconds (120ms).
        // However, QueuedTarget (60,000) != LastAppliedTarget (55,000).
        queue.CheckExpiration(1_180);

        // Crucial invariant: CheckExpiration MUST NOT reset pending unapplied target
        Assert.True(queue.HasPendingInput);
        Assert.Equal(60_000, queue.QueuedTargetPosition);

        // Step 4: At t=1200 (150ms since LastApplyTimestamp=1000), throttle allows dispatch
        var dispatchedB = queue.TryGetDispatchAction(1_200, out var targetB, out var forwardB, out var modeB);
        Assert.True(dispatchedB);
        Assert.Equal(60_000, targetB);
        Assert.True(forwardB);
        Assert.Equal(RelativeSeekMode.Precise, modeB);

        // Step 5: After target is applied and expiry elapsed (t=1350), queue safely resets
        queue.CheckExpiration(1_350);
        Assert.False(queue.HasPendingInput);
    }
}
