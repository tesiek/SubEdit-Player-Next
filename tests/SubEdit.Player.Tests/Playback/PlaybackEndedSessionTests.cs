using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SubEdit.Player.Core.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

/// <summary>
/// Deterministic tests for F06: Media session identity scoping for deferred PlaybackEnded callbacks.
/// Validates Scenarios A through F and double-deferral guarantees.
/// </summary>
public class PlaybackEndedSessionTests
{
    private sealed class ControllablePlaybackEngine : IPlaybackEngine
    {
        private long _nextSessionId;
        private long _currentSessionId;

        public bool IsMediaOpen => Volatile.Read(ref _currentSessionId) > 0;
        public long SessionId => Volatile.Read(ref _currentSessionId);
        public PlaybackState State { get; set; } = PlaybackState.Stopped;
        public long PositionMilliseconds { get; set; }
        public long DurationMilliseconds { get; set; } = 100_000;
        public double? MediaFramesPerSecond => 25.0;
        public PlaybackMediaInfo? MediaInfo => null;
        public VideoFileProperties? GetVideoFileProperties() => null;
        public IReadOnlyList<PlaybackAudioTrack> AudioTracks => Array.Empty<PlaybackAudioTrack>();
        public int? ActiveAudioTrackId => null;
        public IReadOnlyList<PlaybackVideoTrack> VideoTracks => Array.Empty<PlaybackVideoTrack>();
        public int? ActiveVideoTrackId => null;
        public IReadOnlyList<PlaybackSubtitleTrack> EmbeddedSubtitleTracks => Array.Empty<PlaybackSubtitleTrack>();
        public int? ActiveEmbeddedSubtitleTrackId => null;
        public bool AreTracksReady => true;
        public IReadOnlyList<PlaybackChapter> Chapters => Array.Empty<PlaybackChapter>();
        public int? ActiveChapterIndex => null;
        public int VolumePercent { get; set; } = 100;
        public double Speed { get; set; } = 1.0;

        public string? CurrentMediaPath { get; private set; }
        public int StopCallCount { get; private set; }
        public int PlayCallCount { get; private set; }

        public event EventHandler? StateChanged { add { } remove { } }
        public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;
        public event EventHandler? MediaInfoChanged { add { } remove { } }
        public event EventHandler? TracksReady { add { } remove { } }

        public void Open(string mediaPath)
        {
            // Mirror FlyleafPlaybackEngine.Open: invalidate old session first
            Interlocked.Exchange(ref _currentSessionId, 0);

            if (mediaPath.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                CurrentMediaPath = null;
                State = PlaybackState.Stopped;
                throw new InvalidOperationException($"Failed to open media: {mediaPath}");
            }

            var newSessionId = Interlocked.Increment(ref _nextSessionId);
            Interlocked.Exchange(ref _currentSessionId, newSessionId);
            CurrentMediaPath = mediaPath;
            State = PlaybackState.Stopped;
        }

        public void Close()
        {
            Interlocked.Exchange(ref _currentSessionId, 0);
            CurrentMediaPath = null;
            State = PlaybackState.Stopped;
        }

        public void Play()
        {
            PlayCallCount++;
            State = PlaybackState.Playing;
        }

        public void Pause()
        {
            State = PlaybackState.Paused;
        }

        public void Stop()
        {
            StopCallCount++;
            State = PlaybackState.Stopped;
        }

        public void Seek(long positionMilliseconds, bool forward = false)
        {
            PositionMilliseconds = positionMilliseconds;
        }

        public void SeekPrecise(long positionMilliseconds)
        {
            PositionMilliseconds = positionMilliseconds;
        }

        public void StepFrameForward() { }
        public void StepFrameBackward() { }
        public bool SelectAudioTrack(int trackId) => true;
        public bool SelectVideoTrack(int trackId) => true;
        public bool SelectEmbeddedSubtitleTrack(int? trackId) => true;
        public void SelectChapter(int chapterIndex) { }

        public void SimulatePlayerPlaybackStopped(Action<Action> dispatcherPost)
        {
            // Mirror FlyleafPlaybackEngine.OnPlayerPlaybackStopped:
            // capture session id before deferral
            var capturedSessionId = Volatile.Read(ref _currentSessionId);
            if (capturedSessionId == 0)
            {
                return;
            }

            State = PlaybackState.Stopped;
            dispatcherPost(() =>
            {
                // Engine dispatcher verification:
                if (Volatile.Read(ref _currentSessionId) != capturedSessionId)
                {
                    return;
                }

                PlaybackEnded?.Invoke(this, new PlaybackEndedEventArgs(capturedSessionId));
            });
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _currentSessionId, 0);
            PlaybackEnded = null;
        }
    }

    private sealed class SimulatedFormHarness
    {
        public ControllablePlaybackEngine Playback { get; } = new();

        public bool LoopEnabled { get; set; }
        public int LoopExecutionCount { get; private set; }
        public int HandlePlaybackEndedExecutedCount { get; private set; }

        public long? PendingPausedSeekPosition { get; set; }
        public bool PendingPausedSeekPrecise { get; set; }
        public long? PendingStoppedSeekPosition { get; set; }
        public bool PendingStoppedSeekPrecise { get; set; }
        public long StoppedSeekPosition { get; private set; }
        public bool IsStopped { get; private set; }

        public SimulatedFormHarness()
        {
            Playback.PlaybackEnded += (_, e) =>
            {
                var eventSessionId = e.SessionId;
                EnqueueUiCallback(() => HandlePlaybackEnded(eventSessionId));
            };
        }

        public Queue<Action> UiQueue { get; } = new();

        public void EnqueueUiCallback(Action callback)
        {
            UiQueue.Enqueue(callback);
        }

        public void DrainOneUiCallback()
        {
            if (UiQueue.Count > 0)
            {
                var action = UiQueue.Dequeue();
                action();
            }
        }

        public void DrainAllUiCallbacks()
        {
            while (UiQueue.Count > 0)
            {
                DrainOneUiCallback();
            }
        }

        public void LoadVideoFile(string path)
        {
            try
            {
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                PendingStoppedSeekPosition = null;
                PendingStoppedSeekPrecise = false;
                Playback.Open(path);
                Playback.Play();
            }
            catch
            {
                HandleMediaOpenFailure();
            }
        }

        public void CloseVideoFile()
        {
            PendingPausedSeekPosition = null;
            PendingPausedSeekPrecise = false;
            PendingStoppedSeekPosition = null;
            PendingStoppedSeekPrecise = false;
            Playback.Close();
        }

        public void HandleMediaOpenFailure()
        {
            Playback.Close();
            PendingPausedSeekPosition = null;
            PendingPausedSeekPrecise = false;
            PendingStoppedSeekPosition = null;
            PendingStoppedSeekPrecise = false;
        }

        public void HandlePlaybackEnded(long eventSessionId)
        {
            // Form1 check:
            if (eventSessionId <= 0 || eventSessionId != Playback.SessionId || !Playback.IsMediaOpen)
            {
                return;
            }

            HandlePlaybackEndedExecutedCount++;

            if (PendingStoppedSeekPosition is { } pendingStopped)
            {
                StoppedSeekPosition = pendingStopped;
                PendingStoppedSeekPosition = null;
                PendingStoppedSeekPrecise = false;
                IsStopped = true;
                return;
            }

            if (PendingPausedSeekPosition is { } pendingPaused)
            {
                StoppedSeekPosition = pendingPaused;
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                IsStopped = true;
                return;
            }

            if (LoopEnabled && Playback.IsMediaOpen)
            {
                LoopExecutionCount++;
                Playback.Stop();
                Playback.Play();
                return;
            }

            IsStopped = true;
        }
    }

    [Fact]
    public void F06_ScenarioA_BasicStaleEof_IgnoredWhenNewSessionOpened()
    {
        var harness = new SimulatedFormHarness();

        // Step 1: Open session A (sessionId = 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 2: Trigger EOF for session A while UI callback is held in queue
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        Assert.Single(harness.UiQueue); // Adapter dispatch queued

        // Execute adapter dispatch -> queues Form1 UI callback
        harness.DrainOneUiCallback();
        Assert.Single(harness.UiQueue); // Form1 UI callback queued with sessionId = 1

        // Step 3: Open session B (sessionId = 2) with a pending seek
        harness.LoadVideoFile("videoB.mp4");
        Assert.Equal(2, harness.Playback.SessionId);
        harness.PendingPausedSeekPosition = 99_900;
        harness.PendingPausedSeekPrecise = true;

        // Step 4: Release and drain stale callback A
        harness.DrainOneUiCallback();

        // Step 5: Verify callback A was completely ignored
        Assert.Equal(0, harness.HandlePlaybackEndedExecutedCount);
        // Session B state must not be corrupted
        Assert.Equal(99_900, harness.PendingPausedSeekPosition);
        Assert.True(harness.PendingPausedSeekPrecise);
        Assert.Equal(0, harness.StoppedSeekPosition);
    }

    [Fact]
    public void F06_ScenarioB_CurrentSessionEof_ExecutesNormally()
    {
        var harness = new SimulatedFormHarness();

        // Step 1: Open session A (sessionId = 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);
        harness.PendingPausedSeekPosition = 50_000;
        harness.PendingPausedSeekPrecise = true;

        // Step 2: Trigger EOF for session A
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainAllUiCallbacks();

        // Step 3: Verify callback executed normally
        Assert.Equal(1, harness.HandlePlaybackEndedExecutedCount);
        Assert.Equal(50_000, harness.StoppedSeekPosition);
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.True(harness.IsStopped);
    }

    [Fact]
    public void F06_ScenarioC_CloseBetweenEventAndCallback_Ignored()
    {
        var harness = new SimulatedFormHarness();

        // Step 1: Open session A (sessionId = 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 2: Queue EOF
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainOneUiCallback(); // Adapter dispatch to Form1 queue
        Assert.Single(harness.UiQueue);

        // Step 3: User closes video file
        harness.CloseVideoFile();
        Assert.Equal(0, harness.Playback.SessionId);
        Assert.False(harness.Playback.IsMediaOpen);

        // Step 4: Drain stale callback
        harness.DrainOneUiCallback();

        // Step 5: Verify discarded
        Assert.Equal(0, harness.HandlePlaybackEndedExecutedCount);
    }

    [Fact]
    public void F06_ScenarioD_ReopenSamePath_DifferentSession_StaleCallbackIgnored()
    {
        var harness = new SimulatedFormHarness();

        // Step 1: Open "videoA.mp4" (session 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 2: Queue EOF for session 1
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainOneUiCallback();
        Assert.Single(harness.UiQueue); // Form1 callback for session 1

        // Step 3: Open "videoB.mp4" (session 2)
        harness.LoadVideoFile("videoB.mp4");
        Assert.Equal(2, harness.Playback.SessionId);

        // Step 4: Reopen "videoA.mp4" (session 3)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(3, harness.Playback.SessionId);
        Assert.Equal("videoA.mp4", harness.Playback.CurrentMediaPath);

        // Step 5: Drain old callback from session 1
        harness.DrainOneUiCallback();

        // Step 6: Rejected despite matching file path
        Assert.Equal(0, harness.HandlePlaybackEndedExecutedCount);
    }

    [Fact]
    public void F06_ScenarioE_OpenFailure_InvalidatesPreviousSession_StaleCallbackIgnored()
    {
        var harness = new SimulatedFormHarness();

        // Step 1: Open session A (session 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 2: Queue EOF for session 1
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainOneUiCallback();
        Assert.Single(harness.UiQueue);

        // Step 3: Attempt to open a failing media file
        harness.LoadVideoFile("failed_file.mp4");
        Assert.Equal(0, harness.Playback.SessionId);
        Assert.False(harness.Playback.IsMediaOpen);

        // Step 4: Drain stale callback from session 1
        harness.DrainOneUiCallback();

        // Step 5: Rejected
        Assert.Equal(0, harness.HandlePlaybackEndedExecutedCount);
    }

    [Fact]
    public void F06_ScenarioF_LoopPlayback_CurrentSessionMaintainsValidIdentity()
    {
        var harness = new SimulatedFormHarness { LoopEnabled = true };

        // Step 1: Open session A (session 1)
        harness.LoadVideoFile("videoA.mp4");
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 2: First loop iteration reaches EOF
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainAllUiCallbacks();

        // First loop handled
        Assert.Equal(1, harness.HandlePlaybackEndedExecutedCount);
        Assert.Equal(1, harness.LoopExecutionCount);
        Assert.Equal(1, harness.Playback.SessionId); // Session still valid
        Assert.Equal(PlaybackState.Playing, harness.Playback.State);

        // Step 3: Second loop iteration reaches EOF
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainAllUiCallbacks();

        // Second loop handled
        Assert.Equal(2, harness.HandlePlaybackEndedExecutedCount);
        Assert.Equal(2, harness.LoopExecutionCount);
        Assert.Equal(1, harness.Playback.SessionId);

        // Step 4: New video opened -> subsequent old callback rejected
        harness.Playback.SimulatePlayerPlaybackStopped(post => harness.EnqueueUiCallback(post));
        harness.DrainOneUiCallback(); // Queued in Form1 queue
        harness.LoadVideoFile("videoB.mp4"); // Session 2
        Assert.Equal(2, harness.Playback.SessionId);

        harness.DrainOneUiCallback();
        // Counter did NOT increment for session 1 callback on session 2
        Assert.Equal(2, harness.HandlePlaybackEndedExecutedCount);
    }

    [Fact]
    public async Task F06_DoubleDeferral_ThreadSafeCaptureAndRejection()
    {
        var engine = new ControllablePlaybackEngine();
        engine.Open("file1.mp4");
        var session1 = engine.SessionId;
        Assert.Equal(1, session1);

        var adapterQueue = new ConcurrentQueue<Action>();
        var formQueue = new ConcurrentQueue<Action>();

        var callbackExecuted = new ManualResetEventSlim(false);
        var finalExecutedCount = 0;

        engine.PlaybackEnded += (_, e) =>
        {
            var eventSessionId = e.SessionId;
            formQueue.Enqueue(() =>
            {
                if (eventSessionId > 0 && eventSessionId == engine.SessionId && engine.IsMediaOpen)
                {
                    Interlocked.Increment(ref finalExecutedCount);
                }
                callbackExecuted.Set();
            });
        };

        // Simulate background thread EOF
        var eofTask = Task.Run(() =>
        {
            engine.SimulatePlayerPlaybackStopped(post => adapterQueue.Enqueue(post));
        });
        await eofTask;

        // Drain adapter queue on simulated UI thread -> posts to formQueue
        Assert.True(adapterQueue.TryDequeue(out var adapterAction));
        adapterAction!();

        // Before draining formQueue, session 2 is opened
        engine.Open("file2.mp4");
        Assert.Equal(2, engine.SessionId);

        // Now drain formQueue
        Assert.True(formQueue.TryDequeue(out var formAction));
        formAction!();

        Assert.True(callbackExecuted.Wait(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, finalExecutedCount); // Stale callback was safely discarded
    }
}
