namespace SubEdit.Player.Core.Playback;

public interface IPlaybackEngine : IDisposable
{
    bool IsMediaOpen { get; }

    long SessionId { get; }

    PlaybackState State { get; }

    long PositionMilliseconds { get; }

    long DurationMilliseconds { get; }

    double? MediaFramesPerSecond { get; }

    PlaybackMediaInfo? MediaInfo { get; }

    VideoFileProperties? GetVideoFileProperties();

    IReadOnlyList<PlaybackAudioTrack> AudioTracks { get; }

    int? ActiveAudioTrackId { get; }

    IReadOnlyList<PlaybackVideoTrack> VideoTracks { get; }

    int? ActiveVideoTrackId { get; }

    IReadOnlyList<PlaybackSubtitleTrack> EmbeddedSubtitleTracks { get; }

    int? ActiveEmbeddedSubtitleTrackId { get; }

    bool AreTracksReady { get; }

    IReadOnlyList<PlaybackChapter> Chapters { get; }

    int? ActiveChapterIndex { get; }

    int VolumePercent { get; set; }

    double Speed { get; set; }

    event EventHandler? StateChanged;

    event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;

    event EventHandler? MediaInfoChanged;

    event EventHandler? TracksReady;

    void Open(string mediaPath);

    void Close();

    void Play();

    void Pause();

    void Stop();

    void Seek(long positionMilliseconds, bool forward = false);

    void SeekPrecise(long positionMilliseconds);

    void StepFrameForward();

    void StepFrameBackward();

    bool SelectAudioTrack(int trackId);

    bool SelectVideoTrack(int trackId);

    bool SelectEmbeddedSubtitleTrack(int? trackId);

    void SelectChapter(int chapterIndex);
}
