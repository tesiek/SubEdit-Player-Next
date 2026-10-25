using System;

namespace SubEdit.Player.WinForms.Playback;

/// <summary>
/// Specifies whether a queued relative seek should be dispatched via FAST keyframe seek or PRECISE accurate seek.
/// </summary>
public enum RelativeSeekMode
{
    Fast,
    Precise
}

/// <summary>
/// Coordinates relative seek input aggregation, throttling, direction tracking, and finalization for FAST and PRECISE relative navigation.
/// </summary>
public sealed class RelativeSeekQueue
{
    public const int TimerIntervalMilliseconds = 40;
    public const int ExpiryTimeoutMilliseconds = 120;
    public const int ThrottleIntervalMilliseconds = 150;

    public long? QueuedTargetPosition { get; private set; }
    public bool? QueuedForward { get; private set; }
    public RelativeSeekMode? QueuedMode { get; private set; }

    public long? LastAppliedTargetPosition { get; private set; }
    public bool? LastAppliedForward { get; private set; }
    public RelativeSeekMode? LastAppliedMode { get; private set; }

    public long LastInputTimestamp { get; private set; }
    public long LastApplyTimestamp { get; private set; }

    public bool HasPendingInput => QueuedTargetPosition.HasValue;

    /// <summary>
    /// Enqueues a relative seek offset in milliseconds, updating the logical target position.
    /// </summary>
    /// <returns>The actual applied delta in milliseconds after clamping to [0, mediaLength].</returns>
    public long Enqueue(
        long offset,
        long currentPosition,
        long mediaLength,
        RelativeSeekMode mode,
        long currentTimestamp)
    {
        if (mediaLength <= 0)
        {
            return 0;
        }

        if (offset > 0)
        {
            QueuedForward = true;
        }
        else if (offset < 0)
        {
            QueuedForward = false;
        }

        QueuedMode = mode;

        var basePosition = QueuedTargetPosition ?? currentPosition;
        var targetPosition = Math.Clamp(basePosition + offset, 0, mediaLength);
        var appliedOffset = targetPosition - basePosition;

        QueuedTargetPosition = targetPosition;
        LastInputTimestamp = currentTimestamp;

        return appliedOffset;
    }

    /// <summary>
    /// Checks if a throttled seek dispatch should be executed.
    /// </summary>
    public bool TryGetDispatchAction(
        long currentTimestamp,
        out long targetPosition,
        out bool forward,
        out RelativeSeekMode mode)
    {
        targetPosition = 0;
        forward = false;
        mode = RelativeSeekMode.Fast;

        if (QueuedTargetPosition is not { } queuedTarget)
        {
            return false;
        }

        var isStateChanged = queuedTarget != LastAppliedTargetPosition ||
                             QueuedForward != LastAppliedForward ||
                             QueuedMode != LastAppliedMode;

        if (isStateChanged && currentTimestamp - LastApplyTimestamp >= ThrottleIntervalMilliseconds)
        {
            LastApplyTimestamp = currentTimestamp;
            LastAppliedTargetPosition = queuedTarget;
            LastAppliedForward = QueuedForward;
            LastAppliedMode = QueuedMode;

            targetPosition = queuedTarget;
            forward = QueuedForward ?? false;
            mode = QueuedMode ?? RelativeSeekMode.Fast;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the queue has expired (no new inputs for > 120ms and last applied state matches current queued state) and resets it.
    /// </summary>
    public void CheckExpiration(long currentTimestamp)
    {
        if (currentTimestamp - LastInputTimestamp > ExpiryTimeoutMilliseconds &&
            QueuedTargetPosition == LastAppliedTargetPosition &&
            QueuedForward == LastAppliedForward &&
            QueuedMode == LastAppliedMode)
        {
            Reset();
        }
    }

    /// <summary>
    /// Resets all queued and last applied state.
    /// </summary>
    public void Reset()
    {
        QueuedTargetPosition = null;
        QueuedForward = null;
        QueuedMode = null;
        LastAppliedTargetPosition = null;
        LastAppliedForward = null;
        LastAppliedMode = null;
    }

    /// <summary>
    /// Synchronizes the current logical target position (e.g. when an unsafe seek was clamped).
    /// </summary>
    public void SyncCurrentPosition(long position)
    {
        QueuedTargetPosition = position;
        LastAppliedTargetPosition = position;
        LastAppliedForward = QueuedForward;
        LastAppliedMode = QueuedMode;
    }
}
