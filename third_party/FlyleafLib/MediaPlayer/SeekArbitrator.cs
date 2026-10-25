#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;

namespace FlyleafLib.MediaPlayer;

/// <summary>
/// Thread-safe arbitrator for seek requests and frame-step operations within Flyleaf.
/// Enforces key invariants:
/// 1. Latest user intent always wins (superseding older in-flight or queued requests).
/// 2. New requests are never discarded by previous cleanup/clear operations (atomic pop and clear).
/// 3. Paused-to-Playing state transitions cleanly hand off pending seeks without dropping them.
/// 4. Frame-step operations invalidate stale in-flight seeks so older seeks cannot overwrite frame-step position.
/// </summary>
public sealed class SeekArbitrator
{
    private readonly object _lock = new();
    private readonly Stack<SeekData> _seeks = new();
    private long _latestIntentId;

    /// <summary>
    /// Gets the current monotonically increasing intent ID.
    /// </summary>
    public long LatestIntentId => Volatile.Read(ref _latestIntentId);

    /// <summary>
    /// Gets whether there are no pending seek requests in the queue.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _seeks.Count == 0;
            }
        }
    }

    /// <summary>
    /// Gets the synchronization root object.
    /// </summary>
    public object SyncRoot => _lock;

    /// <summary>
    /// Enqueues a new seek request with a newly allocated monotonically increasing intent ID.
    /// </summary>
    public SeekData Enqueue(int ms, bool forward, bool accurate, SeekAccurateMode mode)
    {
        lock (_lock)
        {
            long intentId = Interlocked.Increment(ref _latestIntentId);
            var seekData = new SeekData(ms, forward, accurate, mode, intentId);
            _seeks.Push(seekData);
            return seekData;
        }
    }

    /// <summary>
    /// Pops the latest seek request and atomically clears any older superseded requests below it.
    /// Returns null if the queue is empty.
    /// </summary>
    public SeekData? TryPopLatest()
    {
        lock (_lock)
        {
            if (_seeks.Count == 0)
                return null;

            var latest = _seeks.Pop();
            _seeks.Clear();
            return latest;
        }
    }

    /// <summary>
    /// Evaluates <paramref name="canProcessPredicate"/> under lock and, if true and a seek is present,
    /// pops the latest seek request and clears older requests.
    /// If <paramref name="canProcessPredicate"/> returns false (e.g. status transitioned to Playing),
    /// leaves the queue intact and returns null.
    /// </summary>
    public SeekData? TryPopLatestIf(Func<bool> canProcessPredicate)
    {
        lock (_lock)
        {
            if (_seeks.Count == 0 || !canProcessPredicate())
                return null;

            var latest = _seeks.Pop();
            _seeks.Clear();
            return latest;
        }
    }

    /// <summary>
    /// Invalidates all pending and in-flight seeks by bumping the intent version and clearing the queue.
    /// Used by Frame-step (ShowFrameNext/Prev/Index), Stop, and Reset.
    /// </summary>
    public void InvalidateAndClear()
    {
        lock (_lock)
        {
            Interlocked.Increment(ref _latestIntentId);
            _seeks.Clear();
        }
    }

    /// <summary>
    /// Checks whether the given intent ID has been superseded by a newer seek or frame step.
    /// </summary>
    public bool IsSuperseded(long intentId)
        => intentId != Volatile.Read(ref _latestIntentId);
}
