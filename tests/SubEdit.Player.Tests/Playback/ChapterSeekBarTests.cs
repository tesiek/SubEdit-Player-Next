using System.Drawing;
using System.Windows.Forms;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class ChapterSeekBarTests
{
    private static ChapterSeekBar CreateSeekBar(int width = 200, int minimum = 0, int maximum = 1000)
    {
        var seekBar = new ChapterSeekBar
        {
            Minimum = minimum,
            Maximum = maximum,
            Width = width,
            Height = 22,
            DragSize = new Size(10, 10)
        };
        return seekBar;
    }

    [Fact]
    public void Click_SingleLeftClick_FiresExactlyOneSeekCommitted_AndZeroPreviewSeekRequested()
    {
        var seekBar = CreateSeekBar();
        var previewCount = 0;
        var commitCount = 0;
        var interactionStartedCount = 0;

        seekBar.InteractionStarted += (_, _) => interactionStartedCount++;
        seekBar.PreviewSeekRequested += (_, _) => previewCount++;
        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(100, 10);

        Assert.Equal(1, interactionStartedCount);
        Assert.True(seekBar.IsInteracting);
        Assert.False(seekBar.IsDragging);
        Assert.Equal(SeekBarInteractionState.Pressed, seekBar.InteractionState);

        seekBar.SimulateMouseUp(100, 10);

        Assert.Equal(0, previewCount);
        Assert.Equal(1, commitCount);
        Assert.False(seekBar.IsInteracting);
        Assert.False(seekBar.IsDragging);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);
    }

    [Fact]
    public void Click_SameValueAsCurrent_FiresExactlyOneSeekCommitted_NotDeduplicated()
    {
        var seekBar = CreateSeekBar();
        seekBar.Value = 500;
        var commitCount = 0;
        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(100, 10);
        seekBar.SimulateMouseUp(100, 10);

        Assert.Equal(1, commitCount);
    }

    [Fact]
    public void StationaryLongPress_WithoutMovement_FiresZeroPreviewSeekRequested_AndOneSeekCommittedOnMouseUp()
    {
        var seekBar = CreateSeekBar();
        var previewCount = 0;
        var commitCount = 0;

        seekBar.PreviewSeekRequested += (_, _) => previewCount++;
        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(100, 10);
        Assert.Equal(SeekBarInteractionState.Pressed, seekBar.InteractionState);

        // Timer ticks multiple times while LMB is stationary
        seekBar.SimulateDragSeekTimerTick();
        seekBar.SimulateDragSeekTimerTick();
        seekBar.SimulateDragSeekTimerTick();

        Assert.Equal(0, previewCount);

        seekBar.SimulateMouseUp(100, 10);

        Assert.Equal(0, previewCount);
        Assert.Equal(1, commitCount);
    }

    [Fact]
    public void SmallMovement_BelowDragThreshold_RemainsInPressedState_FiresZeroPreviewSeekRequested()
    {
        var seekBar = CreateSeekBar();
        var previewCount = 0;
        var commitCount = 0;

        seekBar.PreviewSeekRequested += (_, _) => previewCount++;
        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(100, 10);

        // Move by 2 pixels (within 10x10 drag size box)
        seekBar.SimulateMouseMove(102, 10);
        Assert.Equal(SeekBarInteractionState.Pressed, seekBar.InteractionState);

        seekBar.SimulateDragSeekTimerTick();
        Assert.Equal(0, previewCount);

        seekBar.SimulateMouseUp(102, 10);

        Assert.Equal(0, previewCount);
        Assert.Equal(1, commitCount);
    }

    [Fact]
    public void Drag_BeyondThreshold_EntersDragging_FiresPreviewOnTimerTick_AndOneSeekCommittedOnMouseUp()
    {
        var seekBar = CreateSeekBar();
        var previewCount = 0;
        var commitCount = 0;

        seekBar.PreviewSeekRequested += (_, _) => previewCount++;
        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(50, 10);
        Assert.Equal(SeekBarInteractionState.Pressed, seekBar.InteractionState);

        // Move well beyond 10px threshold
        seekBar.SimulateMouseMove(150, 10);
        Assert.Equal(SeekBarInteractionState.Dragging, seekBar.InteractionState);
        Assert.True(seekBar.IsDragging);
        Assert.True(seekBar.IsInteracting);

        seekBar.SimulateDragSeekTimerTick();
        Assert.Equal(1, previewCount);

        // Final release
        seekBar.SimulateMouseUp(150, 10);

        Assert.Equal(1, previewCount);
        Assert.Equal(1, commitCount);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);
    }

    [Fact]
    public void Drag_RepeatedTimerTicksWithoutMouseMovement_DeduplicatesPreviewSeeks()
    {
        var seekBar = CreateSeekBar();
        var previewCount = 0;

        seekBar.PreviewSeekRequested += (_, _) => previewCount++;

        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);

        seekBar.SimulateDragSeekTimerTick();
        Assert.Equal(1, previewCount);

        // Second tick with no mouse move should be deduplicated
        seekBar.SimulateDragSeekTimerTick();
        Assert.Equal(1, previewCount);

        // Moving mouse again enables next preview
        seekBar.SimulateMouseMove(180, 10);
        seekBar.SimulateDragSeekTimerTick();
        Assert.Equal(2, previewCount);
    }

    [Fact]
    public void Drag_ProgrammaticValueChangeDuringDrag_PreservesIntendedValueForFinalCommit()
    {
        var seekBar = CreateSeekBar();
        int? valueAtCommit = null;

        seekBar.SeekCommitted += (_, _) =>
        {
            // At the moment of commit, IntendedValue holds the user target
            valueAtCommit = seekBar.IntendedValue;
        };

        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);

        var intendedBeforeOverwrite = seekBar.IntendedValue;
        Assert.NotNull(intendedBeforeOverwrite);

        // Simulating playback engine/timer updating seekBar.Value to a keyframe PTS (e.g. 100)
        seekBar.Value = 100;

        // IntendedValue remains unchanged despite programmatic Value update
        Assert.Equal(intendedBeforeOverwrite, seekBar.IntendedValue);

        seekBar.SimulateMouseUp(150, 10);

        Assert.Equal(intendedBeforeOverwrite, valueAtCommit);
        // After commit completes, IntendedValue is cleared
        Assert.Null(seekBar.IntendedValue);
    }

    [Fact]
    public void CancelInteraction_DuringPressed_StopsInteraction_FiresZeroSeekCommitted()
    {
        var seekBar = CreateSeekBar();
        var commitCount = 0;

        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(100, 10);
        Assert.True(seekBar.IsInteracting);

        seekBar.CancelInteraction();

        Assert.False(seekBar.IsInteracting);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);
        Assert.Null(seekBar.IntendedValue);

        seekBar.SimulateMouseUp(100, 10);

        Assert.Equal(0, commitCount);
    }

    [Fact]
    public void CancelInteraction_DuringDragging_StopsInteraction_FiresZeroSeekCommitted()
    {
        var seekBar = CreateSeekBar();
        var commitCount = 0;

        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);
        Assert.True(seekBar.IsDragging);

        seekBar.CancelInteraction();

        Assert.False(seekBar.IsInteracting);
        Assert.False(seekBar.IsDragging);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);

        seekBar.SimulateMouseUp(150, 10);

        Assert.Equal(0, commitCount);
    }

    [Fact]
    public void CaptureLoss_WithoutCancel_FiresExactlyOneSeekCommitted()
    {
        var seekBar = CreateSeekBar();
        var commitCount = 0;

        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);

        seekBar.SimulateCaptureLoss();

        Assert.Equal(1, commitCount);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);
    }

    [Fact]
    public void CaptureLoss_AfterCancel_DoesNotFireSeekCommitted()
    {
        var seekBar = CreateSeekBar();
        var commitCount = 0;

        seekBar.SeekCommitted += (_, _) => commitCount++;

        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);

        seekBar.CancelInteraction();
        seekBar.SimulateCaptureLoss();

        Assert.Equal(0, commitCount);
    }

    [Fact]
    public void StoppedState_Contract_PreviewSuppressed_CommitExecutesPrecise()
    {
        var seekBar = CreateSeekBar();
        var isStopped = true;
        var executedPreviews = 0;
        var executedCommits = 0;

        // Wire exactly as in Form1:
        seekBar.PreviewSeekRequested += (_, _) =>
        {
            if (!isStopped)
            {
                executedPreviews++;
            }
        };
        seekBar.SeekCommitted += (_, _) =>
        {
            executedCommits++;
        };

        // User drags in Stopped state
        seekBar.SimulateMouseDown(50, 10);
        seekBar.SimulateMouseMove(150, 10);
        seekBar.SimulateDragSeekTimerTick();

        // In stopped state, preview was not executed
        Assert.Equal(0, executedPreviews);

        seekBar.SimulateMouseUp(150, 10);

        // Final commit executes exactly once
        Assert.Equal(1, executedCommits);
    }

    [Fact]
    public void Drag_ReleaseOutsideControlBounds_ClampsValueAndCommitsExactlyOnce()
    {
        var seekBar = CreateSeekBar(width: 200, minimum: 0, maximum: 1000);
        var commitCount = 0;
        int? committedValue = null;

        seekBar.SeekCommitted += (_, _) =>
        {
            commitCount++;
            committedValue = seekBar.IntendedValue ?? seekBar.Value;
        };

        // Start drag at x=50
        seekBar.SimulateMouseDown(50, 10);
        // Drag far beyond right edge (x=450 when width=200)
        seekBar.SimulateMouseMove(450, -50);
        seekBar.SimulateMouseUp(450, -50);

        Assert.Equal(1, commitCount);
        Assert.Equal(1000, committedValue);
        Assert.Equal(SeekBarInteractionState.Idle, seekBar.InteractionState);
    }
}
