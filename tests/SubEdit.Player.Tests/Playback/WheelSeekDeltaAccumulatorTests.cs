using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class WheelSeekDeltaAccumulatorTests
{
    [Fact]
    public void ProcessDelta_SingleNotchForward_120_Returns10Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(120);

        Assert.Equal(10_000L, offset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_DoubleNotchForward_240_Returns20SecondsSingleOffset()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(240);

        Assert.Equal(20_000L, offset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_TripleNotchForward_360_Returns30Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(360);

        Assert.Equal(30_000L, offset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_SingleNotchBackward_Minus120_ReturnsMinus10Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(-120);

        Assert.Equal(-10_000L, offset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_DoubleNotchBackward_Minus240_ReturnsMinus20SecondsSingleOffset()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(-240);

        Assert.Equal(-20_000L, offset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_PartialDelta_60_ReturnsZeroAndSetsRemainder()
    {
        var accumulator = new WheelSeekDeltaAccumulator();
        var offset = accumulator.ProcessDelta(60);

        Assert.Equal(0L, offset);
        Assert.Equal(60, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_TwoPartialDeltas_60And60_ReturnsZeroThen10Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        var firstOffset = accumulator.ProcessDelta(60);
        Assert.Equal(0L, firstOffset);
        Assert.Equal(60, accumulator.Remainder);

        var secondOffset = accumulator.ProcessDelta(60);
        Assert.Equal(10_000L, secondOffset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_FourHighResolutionDeltas_30Each_AccumulatesToOneFullStep()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        Assert.Equal(0L, accumulator.ProcessDelta(30));
        Assert.Equal(30, accumulator.Remainder);

        Assert.Equal(0L, accumulator.ProcessDelta(30));
        Assert.Equal(60, accumulator.Remainder);

        Assert.Equal(0L, accumulator.ProcessDelta(30));
        Assert.Equal(90, accumulator.Remainder);

        Assert.Equal(10_000L, accumulator.ProcessDelta(30));
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_TwoNegativePartialDeltas_Minus60AndMinus60_ReturnsMinus10Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        var firstOffset = accumulator.ProcessDelta(-60);
        Assert.Equal(0L, firstOffset);
        Assert.Equal(-60, accumulator.Remainder);

        var secondOffset = accumulator.ProcessDelta(-60);
        Assert.Equal(-10_000L, secondOffset);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_DirectionReversal_Plus60ThenMinus120_CompensatesRemainderWithoutStep()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        var firstOffset = accumulator.ProcessDelta(60);
        Assert.Equal(0L, firstOffset);
        Assert.Equal(60, accumulator.Remainder);

        var secondOffset = accumulator.ProcessDelta(-120);
        Assert.Equal(0L, secondOffset);
        Assert.Equal(-60, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_DirectionReversal_Plus60ThenMinus120ThenMinus60_YieldsMinus10Seconds()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        Assert.Equal(0L, accumulator.ProcessDelta(60));
        Assert.Equal(60, accumulator.Remainder);

        Assert.Equal(0L, accumulator.ProcessDelta(-120));
        Assert.Equal(-60, accumulator.Remainder);

        Assert.Equal(-10_000L, accumulator.ProcessDelta(-60));
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_FullOppositeSteps_Plus120ThenMinus120_YieldsSequentialSteps()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        var forward = accumulator.ProcessDelta(120);
        Assert.Equal(10_000L, forward);
        Assert.Equal(0, accumulator.Remainder);

        var backward = accumulator.ProcessDelta(-120);
        Assert.Equal(-10_000L, backward);
        Assert.Equal(0, accumulator.Remainder);
    }

    [Fact]
    public void ProcessDelta_ZeroDelta_ReturnsZeroAndDoesNotAlterRemainder()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        Assert.Equal(0L, accumulator.ProcessDelta(50));
        Assert.Equal(50, accumulator.Remainder);

        Assert.Equal(0L, accumulator.ProcessDelta(0));
        Assert.Equal(50, accumulator.Remainder);
    }

    [Fact]
    public void Reset_ClearsPartialRemainder_PreventingSpuriousFullStep()
    {
        var accumulator = new WheelSeekDeltaAccumulator();

        Assert.Equal(0L, accumulator.ProcessDelta(60));
        Assert.Equal(60, accumulator.Remainder);

        accumulator.Reset();
        Assert.Equal(0, accumulator.Remainder);

        // Next +60 must start from zero remainder, so no full step
        Assert.Equal(0L, accumulator.ProcessDelta(60));
        Assert.Equal(60, accumulator.Remainder);
    }
}
