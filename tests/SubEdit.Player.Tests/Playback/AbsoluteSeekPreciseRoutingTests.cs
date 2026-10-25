using System;
using System.Collections.Generic;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

/// <summary>
/// Unit tests for F04 - Unification of FAST vs PRECISE semantics for absolute seek operations:
/// - Ctrl+K ("Skocz do pozycji")
/// - Boundary Home (target 0) / End (non-M2TS vs M2TS)
/// - Chapter seek (standard vs EOF normalized fallback)
/// - Pending/deferred precise flag propagation (Playing -> Pause / PlaybackEnded)
/// </summary>
public class AbsoluteSeekPreciseRoutingTests
{
    private const long MediaLength = 120_000; // 2 minutes (120,000 ms)

    private sealed class MockPlaybackEngine : IPlaybackEngine
    {
        public bool IsMediaOpen { get; set; } = true;
        public long SessionId { get; set; } = 1;
        public PlaybackState State { get; set; } = PlaybackState.Playing;
        public long PositionMilliseconds { get; set; } = 50_000;
        public long DurationMilliseconds { get; set; } = MediaLength;
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
        public IReadOnlyList<PlaybackChapter> Chapters { get; set; } = Array.Empty<PlaybackChapter>();
        public int? ActiveChapterIndex => null;
        public int VolumePercent { get; set; } = 100;
        public double Speed { get; set; } = 1.0;

        public readonly List<(string Method, long Position, bool? Forward)> SeekLog = new();

        public event EventHandler? StateChanged { add { } remove { } }
        public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded { add { } remove { } }
        public event EventHandler? MediaInfoChanged { add { } remove { } }
        public event EventHandler? TracksReady { add { } remove { } }

        public void Open(string mediaPath) { }
        public void Close() { }
        public void Play() { State = PlaybackState.Playing; }
        public void Pause() { State = PlaybackState.Paused; }
        public void Stop() { State = PlaybackState.Stopped; }

        public void Seek(long positionMilliseconds, bool forward = false)
        {
            SeekLog.Add(("Seek", positionMilliseconds, forward));
            PositionMilliseconds = positionMilliseconds;
        }

        public void SeekPrecise(long positionMilliseconds)
        {
            SeekLog.Add(("SeekPrecise", positionMilliseconds, null));
            PositionMilliseconds = positionMilliseconds;
        }

        public void StepFrameForward() { }
        public void StepFrameBackward() { }
        public bool SelectAudioTrack(int trackId) => true;
        public bool SelectVideoTrack(int trackId) => true;
        public bool SelectEmbeddedSubtitleTrack(int? trackId) => true;

        public void SelectChapter(int chapterIndex)
        {
            if (chapterIndex >= 0 && chapterIndex < Chapters.Count)
            {
                SeekPrecise(Chapters[chapterIndex].PositionMilliseconds);
            }
        }

        public void Dispose() { }
    }

    // Simulated Form1 seek harness reflecting exact Form1 seek mechanics
    private sealed class SimulatedForm1SeekHarness
    {
        public readonly MockPlaybackEngine Playback = new();
        public string? CurrentVideoPath;
        public long LastKnownMediaLengthMilliseconds = MediaLength;
        public long StoppedSeekPosition;
        public bool IsStopped;
        public long? PendingPausedSeekPosition;
        public bool PendingPausedSeekPrecise;
        public long? PendingStoppedSeekPosition;
        public bool PendingStoppedSeekPrecise;
        public bool ShowVideoEndPosition;

        public void SeekToPosition(long targetPosition, long length, bool showVideoEndPosition = false, bool forward = false, bool precise = false)
        {
            if (Form1.IsUnsafeManualSeekNearEnd(CurrentVideoPath, targetPosition, length))
            {
                return;
            }

            targetPosition = Form1.NormalizeManualSeekTarget(CurrentVideoPath, targetPosition, length);
            targetPosition = Math.Clamp(targetPosition, 0, length);
            ShowVideoEndPosition = showVideoEndPosition;

            if (IsStopped && Playback.IsMediaOpen)
            {
                StoppedSeekPosition = targetPosition;
                if (precise)
                {
                    Playback.SeekPrecise(targetPosition);
                }
                else
                {
                    Playback.Seek(targetPosition, forward);
                }
                return;
            }

            if (precise)
            {
                Playback.SeekPrecise(targetPosition);
            }
            else
            {
                Playback.Seek(targetPosition, forward);
            }
        }

        public void SeekToBoundary(bool toEnd)
        {
            var length = Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds);
            if (length <= 0) return;

            if (toEnd)
            {
                var endPosition = Form1.NormalizeManualSeekTarget(CurrentVideoPath, length, length);
                if (Form1.IsUnsafeManualSeekNearEnd(CurrentVideoPath, endPosition, length))
                {
                    if (Playback.State == PlaybackState.Playing)
                    {
                        Playback.Pause();
                        IsStopped = false;
                    }
                    return;
                }

                if (Playback.State == PlaybackState.Playing)
                {
                    PendingStoppedSeekPosition = null;
                    PendingStoppedSeekPrecise = false;
                    PendingPausedSeekPosition = endPosition;
                    PendingPausedSeekPrecise = true;
                    ShowVideoEndPosition = true;
                    Playback.Pause();
                    IsStopped = false;
                }
                else
                {
                    SeekToPosition(endPosition, length, showVideoEndPosition: true, precise: true);
                }
            }
            else
            {
                SeekToPosition(0, length, precise: true);
            }
        }

        public void ApplyPendingSeekAfterPause()
        {
            if (PendingStoppedSeekPosition is { } stoppedPosition)
            {
                var isPrecise = PendingStoppedSeekPrecise;
                PendingStoppedSeekPosition = null;
                PendingStoppedSeekPrecise = false;
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                IsStopped = true;
                StoppedSeekPosition = stoppedPosition;
                SeekToPosition(
                    stoppedPosition,
                    Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds),
                    showVideoEndPosition: true,
                    precise: isPrecise);
                return;
            }

            if (PendingPausedSeekPosition is { } targetPosition)
            {
                var isPrecise = PendingPausedSeekPrecise;
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                SeekToPosition(
                    targetPosition,
                    Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds),
                    showVideoEndPosition: true,
                    precise: isPrecise);
                IsStopped = false;
            }
        }

        public void HandlePlaybackEnded(long? eventSessionId = null)
        {
            var sessionId = eventSessionId ?? Playback.SessionId;
            if (sessionId <= 0 || sessionId != Playback.SessionId || !Playback.IsMediaOpen)
            {
                return;
            }

            if (PendingStoppedSeekPosition is { } pendingStoppedPosition)
            {
                var isPrecise = PendingStoppedSeekPrecise;
                PendingStoppedSeekPosition = null;
                PendingStoppedSeekPrecise = false;
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                IsStopped = true;
                StoppedSeekPosition = pendingStoppedPosition;
                SeekToPosition(
                    pendingStoppedPosition,
                    Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds),
                    showVideoEndPosition: true,
                    precise: isPrecise);
                return;
            }

            if (PendingPausedSeekPosition is { } pendingEndFramePosition)
            {
                var isPrecise = PendingPausedSeekPrecise;
                PendingPausedSeekPosition = null;
                PendingPausedSeekPrecise = false;
                IsStopped = true;
                SeekToPosition(
                    pendingEndFramePosition,
                    Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds),
                    showVideoEndPosition: true,
                    precise: isPrecise);
            }
        }

        public void SeekToChapter(int chapterIndex, long chapterPosition)
        {
            var length = Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds);
            if (Form1.IsUnsafeManualSeekNearEnd(CurrentVideoPath, chapterPosition, length))
            {
                return;
            }

            var targetPosition = Form1.NormalizeManualSeekTarget(CurrentVideoPath, chapterPosition, length);
            if (targetPosition != chapterPosition)
            {
                SeekToPosition(targetPosition, length, precise: true);
                return;
            }

            if (IsStopped && Playback.IsMediaOpen)
            {
                StoppedSeekPosition = targetPosition;
                Playback.SelectChapter(chapterIndex);
            }
            else
            {
                Playback.SelectChapter(chapterIndex);
            }
        }

        public void ExecuteGoToVideoPosition(double targetSeconds)
        {
            var length = Math.Max(Playback.DurationMilliseconds, LastKnownMediaLengthMilliseconds);
            if (length <= 0) return;

            var targetPosition = targetSeconds >= long.MaxValue / 1000d
                ? length
                : Math.Clamp((long)Math.Round(targetSeconds * 1000d), 0, length);

            if (Form1.IsUnsafeManualSeekNearEnd(CurrentVideoPath, targetPosition, length))
            {
                return;
            }

            targetPosition = Form1.NormalizeManualSeekTarget(CurrentVideoPath, targetPosition, length);
            SeekToPosition(targetPosition, length, precise: true);
        }

        public void StopPlayback()
        {
            PendingPausedSeekPosition = null;
            PendingPausedSeekPrecise = false;
            PendingStoppedSeekPosition = null;
            PendingStoppedSeekPrecise = false;
            ShowVideoEndPosition = false;
            Playback.Stop();
            IsStopped = true;
            StoppedSeekPosition = 0;
        }

        public void StepFrame()
        {
            PendingPausedSeekPosition = null;
            PendingPausedSeekPrecise = false;
            PendingStoppedSeekPosition = null;
            PendingStoppedSeekPrecise = false;
            ShowVideoEndPosition = false;
            Playback.StepFrameForward();
            IsStopped = false;
        }

        public void Play()
        {
            PendingPausedSeekPosition = null;
            PendingPausedSeekPrecise = false;
            PendingStoppedSeekPosition = null;
            PendingStoppedSeekPrecise = false;
            ShowVideoEndPosition = false;
            IsStopped = false;
            Playback.Play();
        }
    }

    [Fact]
    public void F04_CtrlK_ValidTargetBelowDuration_RoutesAsPrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };

        harness.ExecuteGoToVideoPosition(45.5); // 45,500 ms

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(45_500, entry.Position);
    }

    [Fact]
    public void F04_CtrlK_TargetAtOrExceedingDuration_NonM2ts_NormalizesTo100MsBelowEof_AndRoutesAsPrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mkv"
        };

        // User enters 120.0s (exact duration of 120,000ms)
        harness.ExecuteGoToVideoPosition(120.0);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(119_900, entry.Position); // length - 100 ms
    }

    [Fact]
    public void F04_CtrlK_TargetNearEof_M2ts_BlockedByOneSecondGuard_NoSeekDispatched()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "disc.m2ts"
        };

        // 119.5s is within the 1-second guard (>= 119,000ms for 120,000ms media)
        harness.ExecuteGoToVideoPosition(119.5);

        Assert.Empty(harness.Playback.SeekLog);
    }

    [Fact]
    public void F04_BoundaryHome_RoutesTargetZero_AsPrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };

        harness.SeekToBoundary(toEnd: false);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(0, entry.Position);
    }

    [Fact]
    public void F04_BoundaryHome_InStoppedState_RoutesTargetZero_AsPrecise_AndUpdatesStoppedPosition()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4",
            IsStopped = true
        };

        harness.SeekToBoundary(toEnd: false);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(0, entry.Position);
        Assert.Equal(0, harness.StoppedSeekPosition);
    }

    [Fact]
    public void F04_BoundaryEnd_NonM2ts_PausedOrStopped_RoutesAsPreciseTo100MsBelowEof()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };
        harness.Playback.State = PlaybackState.Paused;

        harness.SeekToBoundary(toEnd: true);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(119_900, entry.Position);
        Assert.True(harness.ShowVideoEndPosition);
    }

    [Fact]
    public void F04_BoundaryEnd_NonM2ts_Playing_DefersPreciseSeekToPauseHandoff()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };
        harness.Playback.State = PlaybackState.Playing;

        // Press End while playing
        harness.SeekToBoundary(toEnd: true);

        // Should have paused and recorded pending precise position without immediate seek
        Assert.Equal(PlaybackState.Paused, harness.Playback.State);
        Assert.Equal(119_900, harness.PendingPausedSeekPosition);
        Assert.True(harness.PendingPausedSeekPrecise);
        Assert.Empty(harness.Playback.SeekLog);

        // Later, when Paused state event triggers ApplyPendingSeekAfterPause:
        harness.ApplyPendingSeekAfterPause();

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(119_900, entry.Position);
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);
    }

    [Fact]
    public void F04_BoundaryEnd_NonM2ts_Playing_DefersPreciseSeekToPlaybackEndedHandoff()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };
        harness.Playback.State = PlaybackState.Playing;

        harness.SeekToBoundary(toEnd: true);

        Assert.Equal(119_900, harness.PendingPausedSeekPosition);
        Assert.True(harness.PendingPausedSeekPrecise);

        // If PlaybackEnded triggers before ApplyPendingSeekAfterPause:
        harness.HandlePlaybackEnded();

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(119_900, entry.Position);
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);
        Assert.True(harness.IsStopped);
    }

    [Fact]
    public void F04_BoundaryEnd_M2ts_BlockedByOneSecondGuard_NoSeekDispatched()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.m2ts"
        };
        harness.Playback.State = PlaybackState.Playing;

        // Press End while playing on .m2ts file
        harness.SeekToBoundary(toEnd: true);

        // Engine is paused, but NO seek is enqueued/dispatched because of the 1s guard
        Assert.Equal(PlaybackState.Paused, harness.Playback.State);
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);
        Assert.Empty(harness.Playback.SeekLog);
    }

    [Fact]
    public void F04_ChapterSeek_StandardPosition_RoutesAsPrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mkv"
        };
        harness.Playback.Chapters = new[]
        {
            new PlaybackChapter(0, "Intro", 0),
            new PlaybackChapter(1, "Act 1", 30_000),
            new PlaybackChapter(2, "Act 2", 60_000),
        };

        harness.SeekToChapter(1, 30_000);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(30_000, entry.Position);
    }

    [Fact]
    public void F04_ChapterSeek_TargetAtOrExceedingDuration_NonM2ts_NormalizesTo100MsBelowEof_AndRoutesAsPrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mkv"
        };
        harness.Playback.Chapters = new[]
        {
            new PlaybackChapter(0, "Intro", 0),
            new PlaybackChapter(1, "Credits", 120_000), // timestamp at exact duration
        };

        harness.SeekToChapter(1, 120_000);

        Assert.Single(harness.Playback.SeekLog);
        var entry = harness.Playback.SeekLog[0];
        Assert.Equal("SeekPrecise", entry.Method);
        Assert.Equal(119_900, entry.Position); // normalized to length - 100 ms and precise
    }

    [Fact]
    public void F04_ChapterSeek_TargetNearEof_M2ts_BlockedByOneSecondGuard()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.m2ts"
        };
        harness.Playback.Chapters = new[]
        {
            new PlaybackChapter(0, "Intro", 0),
            new PlaybackChapter(1, "Credits", 119_500), // within 1s guard on .m2ts
        };

        harness.SeekToChapter(1, 119_500);

        Assert.Empty(harness.Playback.SeekLog);
    }

    [Fact]
    public void F04_PendingPreciseFlags_ResetOnStop_Play_AndStepFrame()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };
        harness.Playback.State = PlaybackState.Playing;

        // 1. Trigger boundary end to populate pending flags
        harness.SeekToBoundary(toEnd: true);
        Assert.NotNull(harness.PendingPausedSeekPosition);
        Assert.True(harness.PendingPausedSeekPrecise);

        // 2. StopPlayback cancels pending seeks
        harness.StopPlayback();
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);
        Assert.Null(harness.PendingStoppedSeekPosition);
        Assert.False(harness.PendingStoppedSeekPrecise);

        // 3. Set pending flags again, then StepFrame cancels them
        harness.PendingPausedSeekPosition = 50_000;
        harness.PendingPausedSeekPrecise = true;
        harness.StepFrame();
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);

        // 4. Set pending flags again, then Play cancels them
        harness.PendingPausedSeekPosition = 60_000;
        harness.PendingPausedSeekPrecise = true;
        harness.Play();
        Assert.Null(harness.PendingPausedSeekPosition);
        Assert.False(harness.PendingPausedSeekPrecise);
    }

    [Fact]
    public void F04_SeekBarInteraction_DragPreviewIsFast_ReleaseAndClickArePrecise()
    {
        var harness = new SimulatedForm1SeekHarness
        {
            CurrentVideoPath = "movie.mp4"
        };

        // Drag preview (e.g. PreviewSeekRequested) passes precise: false
        harness.SeekToPosition(50_000, MediaLength, showVideoEndPosition: false, forward: false, precise: false);
        Assert.Single(harness.Playback.SeekLog);
        Assert.Equal("Seek", harness.Playback.SeekLog[0].Method);
        Assert.Equal(50_000, harness.Playback.SeekLog[0].Position);

        // Commit on mouse release (SeekCommitted) passes precise: true
        harness.SeekToPosition(50_000, MediaLength, showVideoEndPosition: false, forward: false, precise: true);
        Assert.Equal(2, harness.Playback.SeekLog.Count);
        Assert.Equal("SeekPrecise", harness.Playback.SeekLog[1].Method);
        Assert.Equal(50_000, harness.Playback.SeekLog[1].Position);
    }
}
