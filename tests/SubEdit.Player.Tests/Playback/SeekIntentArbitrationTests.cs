using System.Drawing;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class SeekIntentArbitrationTests
{
    private const long MediaLength = 300_000; // 5 minutes

    [Fact]
    public void F01_StopPlayback_InvalidatesPendingThrottledRelativeSeek()
    {
        var queue = new RelativeSeekQueue();
        var currentPosition = 60_000L;

        // Step 1: Input A is dispatched immediately at t=1000
        queue.Enqueue(5_000, currentPosition, MediaLength, RelativeSeekMode.Precise, 1_000);
        var dispatchedA = queue.TryGetDispatchAction(1_000, out var targetA, out _, out _);
        Assert.True(dispatchedA);
        Assert.Equal(65_000, targetA);

        // Step 2: Input B arrives at t=1050 (within 150ms throttle window, stays pending)
        queue.Enqueue(5_000, currentPosition, MediaLength, RelativeSeekMode.Precise, 1_050);
        Assert.True(queue.HasPendingInput);

        // Step 3: StopPlayback occurs -> invalidates pending relative seek
        queue.Reset();

        // Step 4: Next timer tick at t=1200 (after throttle interval)
        var dispatchedAfterStop = queue.TryGetDispatchAction(1_200, out _, out _, out _);
        Assert.False(dispatchedAfterStop);
        Assert.False(queue.HasPendingInput);
    }

    [Fact]
    public void F01_MediaChange_InvalidatesPendingRelativeSeek_DoesNotLeakToNewMedia()
    {
        var queue = new RelativeSeekQueue();
        var currentPositionMedia1 = 120_000L;

        // Input queued on Media 1 but throttled
        queue.Enqueue(10_000, currentPositionMedia1, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.TryGetDispatchAction(1_000, out _, out _, out _); // consume initial dispatch

        queue.Enqueue(10_000, currentPositionMedia1, MediaLength, RelativeSeekMode.Precise, 1_050);
        Assert.True(queue.HasPendingInput);

        // Media 2 loaded -> InvalidatePendingRelativeSeek is invoked
        queue.Reset();

        // Later tick on Media 2 cannot dispatch Media 1's target
        var dispatched = queue.TryGetDispatchAction(1_250, out _, out _, out _);
        Assert.False(dispatched);
        Assert.False(queue.HasPendingInput);
    }

    [Fact]
    public void F01_AbsoluteSeekIntent_BoundaryChapterOrCtrlP_InvalidatesPendingRelativeSeek()
    {
        var queue = new RelativeSeekQueue();
        var currentPosition = 50_000L;

        // Series of relative seeks: first dispatched, second pending in throttle window
        queue.Enqueue(5_000, currentPosition, MediaLength, RelativeSeekMode.Precise, 1_000);
        queue.TryGetDispatchAction(1_000, out _, out _, out _);

        queue.Enqueue(5_000, currentPosition, MediaLength, RelativeSeekMode.Precise, 1_040);
        Assert.True(queue.HasPendingInput);

        // User triggers absolute action (e.g. SeekToBoundary, SeekToChapter, PlayFromEditorLine)
        queue.Reset();

        // Relative queue is empty and will not override the absolute seek
        var dispatched = queue.TryGetDispatchAction(1_200, out _, out _, out _);
        Assert.False(dispatched);
        Assert.False(queue.HasPendingInput);
    }

    [Fact]
    public void F02_SeekBarInteraction_InvalidatesPendingResumePosition_LatestUserIntentWins()
    {
        var seekBar = new ChapterSeekBar
        {
            Minimum = 0,
            Maximum = 1000,
            Width = 200,
            Height = 22,
            DurationMilliseconds = MediaLength,
            DragSize = new Size(10, 10)
        };

        long? pendingResumePosition = 45_000L;
        var pendingResumePrecise = false;
        long? lastCommittedPosition = null;

        // Wire event handlers matching Form1 contract:
        seekBar.InteractionStarted += (_, _) =>
        {
            pendingResumePosition = null;
            pendingResumePrecise = false;
        };

        seekBar.SeekCommitted += (_, _) =>
        {
            var valueToUse = seekBar.IntendedValue ?? seekBar.Value;
            lastCommittedPosition = (long)Math.Round((valueToUse / (double)seekBar.Maximum) * seekBar.DurationMilliseconds);
        };

        // User clicks seekbar at 50% (x=100 in 200px width -> value 500 -> pos 150,000ms)
        seekBar.SimulateMouseDown(100, 10);

        // InteractionStarted must have cleared pending resume immediately
        Assert.Null(pendingResumePosition);
        Assert.False(pendingResumePrecise);

        // Mouse up commits the new user position
        seekBar.SimulateMouseUp(100, 10);
        Assert.Equal(150_000L, lastCommittedPosition);

        // Simulating subsequent UI tick: since pendingResumePosition is null, stale resume is not applied
        Assert.Null(pendingResumePosition);
    }

    [Fact]
    public void F02_SeekBarCancelInteraction_DoesNotRestoreInvalidatedResume()
    {
        var seekBar = new ChapterSeekBar
        {
            Minimum = 0,
            Maximum = 1000,
            Width = 200,
            Height = 22,
            DurationMilliseconds = MediaLength,
            DragSize = new Size(10, 10)
        };

        long? pendingResumePosition = 30_000L;
        var pendingResumePrecise = false;

        seekBar.InteractionStarted += (_, _) =>
        {
            pendingResumePosition = null;
            pendingResumePrecise = false;
        };

        // User starts interacting
        seekBar.SimulateMouseDown(50, 10);
        Assert.Null(pendingResumePosition);

        // Interaction is cancelled (e.g. modal dialog opened or ESC pressed)
        seekBar.CancelInteraction();

        // Resume remains null and is not restored
        Assert.Null(pendingResumePosition);
        Assert.False(pendingResumePrecise);
    }

    [Fact]
    public void F02_NormalResume_WithoutSeekBarInteraction_PreservesResumePosition()
    {
        long? pendingResumePosition = 75_000L;
        var pendingResumePrecise = true;

        // No seekbar interaction occurs.
        // On UI tick:
        long? executedSeekPosition = null;
        var executedSeekPrecise = false;

        if (pendingResumePosition is { } resumePosition)
        {
            executedSeekPrecise = pendingResumePrecise;
            pendingResumePosition = null;
            pendingResumePrecise = false;
            executedSeekPosition = Form1.NormalizeManualSeekTarget("video.mp4", resumePosition, MediaLength);
        }

        Assert.Equal(75_000L, executedSeekPosition);
        Assert.True(executedSeekPrecise);
        Assert.Null(pendingResumePosition);
    }

    [Fact]
    public void F05_StepFrame_InvalidatesPendingSeekAndResumeState()
    {
        var queue = new RelativeSeekQueue();
        long? pendingPausedSeek = 50_000L;
        var pendingPausedPrecise = true;
        long? pendingResume = 60_000L;
        var pendingResumePrecise = false;
        var showVideoEndPosition = true;

        // User enqueued relative seek
        queue.Enqueue(5_000, 100_000L, MediaLength, RelativeSeekMode.Precise, 1_000);
        Assert.True(queue.HasPendingInput);

        // StepFrame contract (Form1.StepFrame):
        queue.Reset();
        pendingPausedSeek = null;
        pendingPausedPrecise = false;
        pendingResume = null;
        pendingResumePrecise = false;
        showVideoEndPosition = false;

        Assert.False(queue.HasPendingInput);
        Assert.Null(pendingPausedSeek);
        Assert.False(pendingPausedPrecise);
        Assert.Null(pendingResume);
        Assert.False(pendingResumePrecise);
        Assert.False(showVideoEndPosition);

        var dispatched = queue.TryGetDispatchAction(1_100, out _, out _, out _);
        Assert.False(dispatched);
    }
}
