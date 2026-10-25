using System.Runtime.InteropServices;
using FlyleafLib.MediaPlayer;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using FlyleafPlayer = FlyleafLib.MediaPlayer.Player;
using FlyleafStatus = FlyleafLib.MediaPlayer.Status;

namespace SubEdit.Player.WinForms.Playback;

internal sealed partial class FlyleafPlaybackEngine : IPlaybackEngine, IEmbeddedTextSubtitleSource
{
    static FlyleafPlaybackEngine()
    {
        FixRendererSubtitleBlendState();
    }

    private readonly Control? _dispatcher;
    private readonly FlyleafPlayer _player;
    private string? _mediaPath;
    private IReadOnlyDictionary<int, BluRayClipStreamInfo>? _clipInfoStreams;
    private PlaybackState _state = PlaybackState.Stopped;
    private long _stoppedPosition;
    private bool _isStopped = true;
    private bool _isActionInProgress;
    private bool _playPendingAfterEndedReset;
    private int _volumePercent = 100;
    private double _speed = 1.0;
    private readonly Func<VideoDecodingMode> _videoDecodingModeProvider;
    private bool _tracksReady;
    private bool _disposed;
    private long _nextSessionId;
    private long _currentSessionId;

    public FlyleafPlaybackEngine(
        Control? dispatcher = null,
        int initialVolume = 100,
        Func<VideoDecodingMode>? videoDecodingModeProvider = null)
    {
        _dispatcher = dispatcher;
        _videoDecodingModeProvider = videoDecodingModeProvider ?? (() => VideoDecodingMode.Automatic);
        _player = new FlyleafPlayer();
        _player.Config.Player.AutoPlay = false;
        _player.Config.Subtitles.SearchLocal = false;
        _player.Config.Subtitles.SearchOnline = false;
        _volumePercent = Math.Clamp(initialVolume, 0, 100);
        ApplyVolume();

        _player.PlaybackStopped += OnPlayerPlaybackStopped;
        _player.SeekCompleted += OnPlayerSeekCompleted;
        _player.Subtitles.PropertyChanged += OnSubtitlesPropertyChanged;
    }

    internal FlyleafPlayer Player => _player;

    public bool IsMediaOpen => _mediaPath is not null;

    public long SessionId => Volatile.Read(ref _currentSessionId);

    public PlaybackState State => _state;

    public long PositionMilliseconds
    {
        get
        {
            if (_mediaPath is null)
            {
                return 0;
            }

            if (_isStopped)
            {
                return _stoppedPosition;
            }

            return Math.Max(0, _player.CurTime / 10000);
        }
    }

    public long DurationMilliseconds
    {
        get
        {
            if (_mediaPath is null)
            {
                return 0;
            }

            return Math.Max(0, _player.Duration / 10000);
        }
    }

    public SeekAccurateMode CurrentSeekAccurateMode => _player.SeekAccurateMode;

    public double? MediaFramesPerSecond
    {
        get
        {
            if (_mediaPath is null || _player.Video.Streams.Count == 0)
            {
                return null;
            }

            var stream = _player.Video.Streams.FirstOrDefault(s => s.StreamIndex == _player.Video.StreamIndex)
                ?? _player.Video.Streams.FirstOrDefault();

            if (stream is null || stream.FPS <= 0)
            {
                return null;
            }

            return stream.FPS;
        }
    }

    public PlaybackMediaInfo? MediaInfo
    {
        get
        {
            if (_mediaPath is null)
            {
                return null;
            }

            if (_player.Video.Streams.Count == 0)
            {
                return new PlaybackMediaInfo(
                    HasVideo: false,
                    Width: 0,
                    Height: 0,
                    DisplayWidth: 0,
                    DisplayHeight: 0,
                    VideoCodecName: string.Empty,
                    VideoFourCc: string.Empty,
                    IsHardwareAccelerated: null);
            }

            var stream = _player.Video.Streams.FirstOrDefault(s => s.StreamIndex == _player.Video.StreamIndex)
                ?? _player.Video.Streams[0];

            var width = (int)stream.Width;
            var height = (int)stream.Height;
            var displayWidth = width;
            var displayHeight = height;

            if (stream.SAR.Num > 0 && stream.SAR.Den > 0 && stream.SAR.Num != stream.SAR.Den)
            {
                displayWidth = (int)Math.Round(width * (double)stream.SAR.Num / stream.SAR.Den);
            }

            var codecDescription = stream.Codec ?? string.Empty;
            var fourCc = string.Empty;
            unsafe
            {
                if (stream.AVStream != null && stream.AVStream->codecpar != null)
                {
                    fourCc = MediaInspector.GetFourCc(stream.AVStream->codecpar->codec_tag);
                }
            }
            var codecName = MediaCodecDisplay.VideoName(codecDescription, detailed: false) ?? string.Empty;

            return new PlaybackMediaInfo(
                HasVideo: true,
                Width: width,
                Height: height,
                DisplayWidth: displayWidth,
                DisplayHeight: displayHeight,
                VideoCodecName: codecName,
                VideoFourCc: fourCc,
                IsHardwareAccelerated: _player.Video.VideoAcceleration);
        }
    }

    public IReadOnlyList<PlaybackAudioTrack> AudioTracks
    {
        get
        {
            if (_mediaPath is null || !_tracksReady || _player.Audio.Streams.Count == 0)
            {
                return [];
            }

            return _player.Audio.Streams.Select(stream =>
            {
                var lang = ResolveStreamLanguage(stream, _player.Audio.Streams, _clipInfoStreams);
                var title = !string.IsNullOrWhiteSpace(stream.Title) ? stream.Title : null;
                var name = title ?? lang ?? "Język nieznany";

                return new PlaybackAudioTrack(
                    Id: stream.StreamIndex,
                    Name: name,
                    HasDetails: true,
                    Description: title,
                    Language: lang,
                    CodecDescription: stream.Codec,
                    SampleRate: (uint)Math.Max(0, stream.SampleRate),
                    Channels: (uint)Math.Max(0, stream.Channels),
                    Bitrate: (uint)Math.Max(0, stream.BitRate),
                    CodecVariant: GetAudioCodecVariant(stream),
                    EffectiveChannelLayout: GetEffectiveAudioLayout(stream));
            }).ToArray();
        }
    }

    public int? ActiveAudioTrackId => _tracksReady && _player.Audio.StreamIndex >= 0
        ? _player.Audio.StreamIndex
        : null;

    public IReadOnlyList<PlaybackVideoTrack> VideoTracks
    {
        get
        {
            if (_mediaPath is null || !_tracksReady || _player.Video.Streams.Count == 0)
            {
                return [];
            }

            return _player.Video.Streams.Select(stream =>
            {
                var lang = ResolveStreamLanguage(stream, _player.Video.Streams, _clipInfoStreams);
                var title = !string.IsNullOrWhiteSpace(stream.Title) ? stream.Title : null;
                var name = title ?? lang ?? "Język nieznany";
                double? fps = stream.FPS > 0 ? stream.FPS : null;

                return new PlaybackVideoTrack(
                    Id: stream.StreamIndex,
                    Name: name,
                    HasDetails: true,
                    Description: title,
                    Language: lang,
                    CodecDescription: stream.Codec,
                    Width: stream.Width,
                    Height: stream.Height,
                    FramesPerSecond: fps,
                    Bitrate: (uint)Math.Max(0, stream.BitRate));
            }).ToArray();
        }
    }

    public int? ActiveVideoTrackId => _tracksReady && _player.Video.StreamIndex >= 0
        ? _player.Video.StreamIndex
        : null;

    public IReadOnlyList<PlaybackSubtitleTrack> EmbeddedSubtitleTracks
    {
        get
        {
            if (_mediaPath is null || !_tracksReady)
            {
                return [];
            }

            return _player.Subtitles.Streams.Select((stream, index) =>
            {
                var lang = ResolveStreamLanguage(stream, _player.Subtitles.Streams, _clipInfoStreams);
                var title = !string.IsNullOrWhiteSpace(stream.Title) ? stream.Title : null;
                var fallbackName = $"Język nieznany #{index + 1}";
                var name = title ?? lang ?? fallbackName;

                return new PlaybackSubtitleTrack(
                    Id: stream.StreamIndex,
                    Name: name,
                    HasDetails: true,
                    Description: title,
                    Language: lang);
            }).ToArray();
        }
    }

    public int? ActiveEmbeddedSubtitleTrackId => _tracksReady && _player.Config.Subtitles.Enabled && _player.Subtitles.StreamIndex >= 0
        ? _player.Subtitles.StreamIndex
        : null;

    public string? CurrentEmbeddedSubtitleText
    {
        get
        {
            if (_mediaPath is null || !_tracksReady || !_player.Config.Subtitles.Enabled || _player.Subtitles.StreamIndex < 0)
            {
                return null;
            }

            var activeStream = _player.Subtitles.Streams.FirstOrDefault(s => s.StreamIndex == _player.Subtitles.StreamIndex);
            if (activeStream != null && activeStream.IsBitmap)
            {
                return null;
            }

            var text = _player.Subtitles.SubsText;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }

    public bool AreTracksReady => _tracksReady;

    public IReadOnlyList<PlaybackChapter> Chapters
    {
        get
        {
            if (_mediaPath is null || _player.Chapters is null || _player.Chapters.Count == 0)
            {
                return [];
            }

            var chapters = new PlaybackChapter[_player.Chapters.Count];
            for (var i = 0; i < _player.Chapters.Count; i++)
            {
                var ch = _player.Chapters[i];
                chapters[i] = new PlaybackChapter(
                    i,
                    ch.Title,
                    ch.StartTime / 10000);
            }

            return chapters;
        }
    }

    public int? ActiveChapterIndex
    {
        get
        {
            if (_mediaPath is null || _player.Chapters is null || _player.Chapters.Count == 0)
            {
                return null;
            }

            var curTimeTicks = _player.CurTime;
            var activeIndex = -1;
            for (var i = 0; i < _player.Chapters.Count; i++)
            {
                var ch = _player.Chapters[i];
                if (curTimeTicks >= ch.StartTime)
                {
                    if (ch.EndTime > ch.StartTime && curTimeTicks < ch.EndTime)
                    {
                        return i;
                    }

                    activeIndex = i;
                }
            }

            return activeIndex >= 0 ? activeIndex : 0;
        }
    }

    public int VolumePercent
    {
        get => _volumePercent;
        set
        {
            _volumePercent = Math.Clamp(value, 0, 100);
            ApplyVolume();
        }
    }

    public double Speed
    {
        get => _player.Speed;
        set
        {
            ThrowIfDisposed();
            _speed = value;
            _player.Speed = value;
        }
    }

    public event EventHandler? StateChanged;

    public event EventHandler<PlaybackEndedEventArgs>? PlaybackEnded;

    public event EventHandler? MediaInfoChanged;

    public event EventHandler? TracksReady;

    public event EventHandler? EmbeddedSubtitleTextChanged;

    public void Open(string mediaPath)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);

        bool replacingMedia = IsMediaOpen;

        _isActionInProgress = true;
        try
        {
            Interlocked.Exchange(ref _currentSessionId, 0);
            // Preserve application state cleanup while Player.Open replaces the media.
            _tracksReady = false;
            if (replacingMedia) SetState(PlaybackState.Stopped);
            _playPendingAfterEndedReset = false;
            _mediaPath = mediaPath;
            _isStopped = true;
            _stoppedPosition = 0;

            var clpiPath = BluRayClipInfoReader.FindMatchingClpiPath(mediaPath);
            _clipInfoStreams = clpiPath != null ? BluRayClipInfoReader.ReadClipInfo(clpiPath) : null;

            _player.Config.Subtitles.Enabled = true;

            var decodingMode = _videoDecodingModeProvider();
            _player.Config.Video.VideoAcceleration = decodingMode != VideoDecodingMode.Software;

            ConfigureDemuxerProbing(mediaPath);

            var openResult = _player.Open(mediaPath);
            if (!openResult.Success)
            {
                _mediaPath = null;
                _tracksReady = false;
                SetState(PlaybackState.Stopped);
                throw new InvalidOperationException($"Failed to open media '{mediaPath}': {openResult.Error}");
            }

            var newSessionId = Interlocked.Increment(ref _nextSessionId);
            Interlocked.Exchange(ref _currentSessionId, newSessionId);

            var demuxerName = _player.VideoDemuxer?.Name ?? _player.AudioDemuxer?.Name;
            var policy = SeekAccuratePolicySelector.Select(demuxerName);
            _player.SeekAccurateMode = policy;
            System.Diagnostics.Debug.WriteLine($"[Playback] Demuxer: '{demuxerName}', SeekAccuratePolicy: {policy}");

            ApplyVolume();
            _player.Speed = _speed;
            _tracksReady = true;
            SetState(PlaybackState.Stopped);

            PostToDispatcher(() =>
            {
                if (_disposed || _mediaPath is null)
                {
                    return;
                }

                if (replacingMedia) EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty);
                MediaInfoChanged?.Invoke(this, EventArgs.Empty);
                TracksReady?.Invoke(this, EventArgs.Empty);
            });
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Close()
    {
        ThrowIfDisposed();
        if (_mediaPath is null && _player.Status == FlyleafStatus.Stopped)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            Interlocked.Exchange(ref _currentSessionId, 0);
            _playPendingAfterEndedReset = false;
            _tracksReady = false;
            _mediaPath = null;
            _clipInfoStreams = null;
            _isStopped = true;
            _stoppedPosition = 0;
            _player.CloseMedia();
            SetState(PlaybackState.Stopped);
            PostToDispatcher(() => EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty));
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Play()
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            if (_player.Status == FlyleafStatus.Ended)
            {
                _playPendingAfterEndedReset = true;
                if (!_isStopped || _stoppedPosition != 0)
                {
                    _player.SeekAccurate(0);
                }
                _isStopped = false;
                return;
            }

            _playPendingAfterEndedReset = false;
            _isStopped = false;
            _player.Play();
            SetState(PlaybackState.Playing);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            _playPendingAfterEndedReset = false;
            _isStopped = false;
            _player.Pause();
            SetState(PlaybackState.Paused);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            _playPendingAfterEndedReset = false;

            if (_player.Status != FlyleafStatus.Ended)
            {
                _player.Pause();
            }

            _player.SeekAccurate(0);
            _isStopped = true;
            _stoppedPosition = 0;
            SetState(PlaybackState.Stopped);
            PostToDispatcher(() => EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty));
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Seek(long positionMilliseconds, bool forward = false)
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _playPendingAfterEndedReset = false;
        var targetMs = (int)Math.Max(0, positionMilliseconds);
        if (_isStopped)
        {
            _stoppedPosition = targetMs;
            _player.Seek(targetMs, forward);
        }
        else
        {
            _player.Seek(targetMs, forward);
        }
    }

    public void SeekPrecise(long positionMilliseconds)
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _playPendingAfterEndedReset = false;
        var targetMs = (int)Math.Max(0, positionMilliseconds);
        if (_isStopped)
        {
            _stoppedPosition = targetMs;
            _player.SeekAccurate(targetMs);
        }
        else
        {
            _player.SeekAccurate(targetMs);
        }
    }

    public void StepFrameForward()
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            _playPendingAfterEndedReset = false;
            _isStopped = false;
            _player.ShowFrameNext();
            SetState(PlaybackState.Paused);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void StepFrameBackward()
    {
        ThrowIfDisposed();
        if (_mediaPath is null)
        {
            return;
        }

        _isActionInProgress = true;
        try
        {
            _playPendingAfterEndedReset = false;
            _isStopped = false;
            _player.ShowFramePrev();
            SetState(PlaybackState.Paused);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public bool SelectAudioTrack(int trackId)
    {
        ThrowIfDisposed();
        if (_mediaPath is null || !_tracksReady)
        {
            return false;
        }

        if (_player.Audio.StreamIndex == trackId)
        {
            return true;
        }

        var stream = _player.Audio.Streams.FirstOrDefault(s => s.StreamIndex == trackId);
        if (stream is null)
        {
            return false;
        }

        _isActionInProgress = true;
        try
        {
            var openResult = _player.Open(stream);
            return openResult.Success;
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public bool SelectVideoTrack(int trackId)
    {
        ThrowIfDisposed();
        if (_mediaPath is null || !_tracksReady)
        {
            return false;
        }

        if (_player.Video.StreamIndex == trackId)
        {
            return true;
        }

        var stream = _player.Video.Streams.FirstOrDefault(s => s.StreamIndex == trackId);
        if (stream is null)
        {
            return false;
        }

        _isActionInProgress = true;
        try
        {
            var openResult = _player.Open(stream);
            return openResult.Success;
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public bool SelectEmbeddedSubtitleTrack(int? trackId)
    {
        ThrowIfDisposed();
        if (_mediaPath is null || !_tracksReady)
        {
            return false;
        }

        if (trackId is null)
        {
            if (!_player.Config.Subtitles.Enabled || _player.Subtitles.StreamIndex < 0)
            {
                return true;
            }

            _isActionInProgress = true;
            try
            {
                _player.Config.Subtitles.Enabled = false;
                PostToDispatcher(() => EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty));
                return true;
            }
            finally
            {
                _isActionInProgress = false;
            }
        }

        if (_player.Config.Subtitles.Enabled && _player.Subtitles.StreamIndex == trackId.Value)
        {
            return true;
        }

        var stream = _player.Subtitles.Streams.FirstOrDefault(s => s.StreamIndex == trackId.Value);
        if (stream is null)
        {
            return false;
        }

        _isActionInProgress = true;
        try
        {
            _player.Config.Subtitles.Enabled = true;
            var openResult = _player.Open(stream);
            PostToDispatcher(() => EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty));
            return openResult.Success;
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void SelectChapter(int chapterIndex)
    {
        ThrowIfDisposed();
        if (_mediaPath is null || _player.Chapters is null || chapterIndex < 0 || chapterIndex >= _player.Chapters.Count)
        {
            return;
        }

        var chapter = _player.Chapters[chapterIndex];
        var positionMs = (int)(chapter.StartTime / 10000);

        _isActionInProgress = true;
        try
        {
            SeekPrecise(positionMs);
        }
        finally
        {
            _isActionInProgress = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Exchange(ref _currentSessionId, 0);
        _playPendingAfterEndedReset = false;
        _tracksReady = false;
        _mediaPath = null;
        _clipInfoStreams = null;
        _player.PlaybackStopped -= OnPlayerPlaybackStopped;
        _player.SeekCompleted -= OnPlayerSeekCompleted;
        _player.Subtitles.PropertyChanged -= OnSubtitlesPropertyChanged;
        _player.Dispose();

        StateChanged = null;
        PlaybackEnded = null;
        MediaInfoChanged = null;
        TracksReady = null;
        EmbeddedSubtitleTextChanged = null;
    }

    private void OnPlayerSeekCompleted(object? sender, int e)
    {
        if (_disposed || _mediaPath is null)
        {
            return;
        }

        if (!_playPendingAfterEndedReset)
        {
            return;
        }

        _playPendingAfterEndedReset = false;

        PostToDispatcher(() =>
        {
            if (_disposed || _mediaPath is null || _isStopped || _state == PlaybackState.Paused)
            {
                return;
            }

            _isActionInProgress = true;
            try
            {
                _isStopped = false;
                _player.Play();
                SetState(PlaybackState.Playing);
            }
            finally
            {
                _isActionInProgress = false;
            }
        });
    }

    private void OnSubtitlesPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Subtitles.SubsText))
        {
            PostToDispatcher(() => EmbeddedSubtitleTextChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    private void ConfigureDemuxerProbing(string mediaPath)
    {
        if (IsMpegProgramStream(mediaPath))
        {
            // MPEG-PS / VOB wymaga głębszego probingu (10MB / 15s), aby wykryć późne pakiety DVD Subpicture (0x20, 0x21, 0x22)
            _player.Config.Demuxer.FormatOpt["probesize"] = "10000000";
            _player.Config.Demuxer.FormatOpt["analyzeduration"] = "15000000";
        }
        else
        {
            _player.Config.Demuxer.FormatOpt.Remove("probesize");
            _player.Config.Demuxer.FormatOpt.Remove("analyzeduration");
        }
    }

    private static bool IsMpegProgramStream(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> header = stackalloc byte[4];
            int read = stream.Read(header);
            if (read == 4 && header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0xBA)
            {
                return true;
            }

            var ext = Path.GetExtension(path);
            if (string.Equals(ext, ".vob", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".mpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".mpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ext, ".m2p", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // Fallback w razie wyjątku wejścia-wyjścia
        }

        return false;
    }

    private static string? ResolveStreamLanguage<TStream>(
        TStream targetStream,
        IReadOnlyList<TStream> allStreamsOfSameType,
        IReadOnlyDictionary<int, BluRayClipStreamInfo>? clipInfoStreams)
        where TStream : FlyleafLib.MediaFramework.MediaStream.StreamBase
    {
        var pid = GetStreamPid(targetStream);
        var clpiLanguage = clipInfoStreams != null && pid >= 0 && clipInfoStreams.TryGetValue(pid, out var clpiStream)
            ? clpiStream.LanguageCode
            : null;

        // 1. Bezpośredni język z CLPI lub metadanych Flyleaf danego streamu
        var directLang = FormatLanguage(targetStream.Language, targetStream.Metadata, clpiLanguage);
        if (!string.IsNullOrWhiteSpace(directLang))
        {
            return directLang;
        }

        // 2. Bezpośrednie natywne AVStream->metadata dla danego streamu
        var nativeLang = GetNativeStreamLanguage(targetStream);
        if (!string.IsNullOrWhiteSpace(nativeLang))
        {
            return nativeLang;
        }

        // 3. Sprawdź inne streamy tego samego typu o identycznym MPEG-TS PID (np. podstrumień AC3 Core dla TrueHD o PID 0x1100)
        if (pid >= 0 && allStreamsOfSameType != null && allStreamsOfSameType.Count > 1)
        {
            string? candidateLang = null;
            bool conflict = false;

            foreach (var other in allStreamsOfSameType)
            {
                if (ReferenceEquals(other, targetStream))
                {
                    continue;
                }

                if (GetStreamPid(other) != pid)
                {
                    continue;
                }

                var otherClpiLang = clipInfoStreams != null && clipInfoStreams.TryGetValue(pid, out var otherClpi)
                    ? otherClpi.LanguageCode
                    : null;

                var otherLang = FormatLanguage(other.Language, other.Metadata, otherClpiLang)
                    ?? GetNativeStreamLanguage(other);

                if (!string.IsNullOrWhiteSpace(otherLang))
                {
                    if (candidateLang == null)
                    {
                        candidateLang = otherLang;
                    }
                    else if (!string.Equals(candidateLang, otherLang, StringComparison.OrdinalIgnoreCase))
                    {
                        // Sprzeczność w danych językowych dla tego samego PID
                        conflict = true;
                        break;
                    }
                }
            }

            if (!conflict && !string.IsNullOrWhiteSpace(candidateLang))
            {
                return candidateLang;
            }
        }

        // 4. Brak wiarygodnego źródła -> null (co da etykietę "Język nieznany")
        return null;
    }

    private static string? GetNativeStreamLanguage(FlyleafLib.MediaFramework.MediaStream.StreamBase stream)
    {
        try
        {
            unsafe
            {
                var avStream = stream.AVStream;
                if (avStream != null && avStream->metadata != null)
                {
                    var entry = Flyleaf.FFmpeg.Raw.av_dict_get(avStream->metadata, "language", null, (Flyleaf.FFmpeg.DictReadFlags)0);
                    if (entry == null)
                    {
                        entry = Flyleaf.FFmpeg.Raw.av_dict_get(avStream->metadata, "lang", null, (Flyleaf.FFmpeg.DictReadFlags)0);
                    }

                    if (entry != null && entry->value != null)
                    {
                        var val = Marshal.PtrToStringAnsi((IntPtr)entry->value)?.Trim();
                        if (!string.IsNullOrWhiteSpace(val) && !string.Equals(val, "und", StringComparison.OrdinalIgnoreCase))
                        {
                            var parsed = FlyleafLib.Language.Get(val);
                            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.TopEnglishName) && !string.Equals(parsed.TopEnglishName, "Unknown", StringComparison.OrdinalIgnoreCase))
                            {
                                return parsed.TopEnglishName;
                            }
                            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.CultureName))
                            {
                                return parsed.CultureName;
                            }
                            return val;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? FormatLanguage(FlyleafLib.Language? language, Dictionary<string, string>? metadata, string? clpiLanguageCode = null)
    {
        // 1. Jeśli przekazano wiarygodny kod języka z pliku Blu-ray .clpi (np. "pol", "eng", "spa")
        if (!string.IsNullOrWhiteSpace(clpiLanguageCode) && !string.Equals(clpiLanguageCode, "und", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = FlyleafLib.Language.Get(clpiLanguageCode.Trim());
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.TopEnglishName) && !string.Equals(parsed.TopEnglishName, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return parsed.TopEnglishName;
            }

            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.CultureName))
            {
                return parsed.CultureName;
            }

            return clpiLanguageCode.Trim();
        }

        // 2. Sprawdź bezpośredni tag języka w Metadata strumienia (np. MKV/MP4 "language" => "eng", "pol")
        if (metadata is not null)
        {
            foreach (var kvp in metadata)
            {
                if (string.Equals(kvp.Key, "language", StringComparison.OrdinalIgnoreCase))
                {
                    var rawTag = kvp.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(rawTag) && !string.Equals(rawTag, "und", StringComparison.OrdinalIgnoreCase))
                    {
                        var parsed = FlyleafLib.Language.Get(rawTag);
                        if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.TopEnglishName) && !string.Equals(parsed.TopEnglishName, "Unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            return parsed.TopEnglishName;
                        }
                        if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.CultureName))
                        {
                            return parsed.CultureName;
                        }
                        return rawTag;
                    }
                }
            }
        }

        // 3. Sprawdź, czy Language posiada rzeczywisty OriginalInput
        if (language is not null && !string.IsNullOrWhiteSpace(language.OriginalInput) && !string.Equals(language.OriginalInput, "und", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(language.TopEnglishName) &&
                !string.Equals(language.TopEnglishName, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return language.TopEnglishName;
            }

            if (!string.IsNullOrWhiteSpace(language.CultureName))
            {
                return language.CultureName;
            }

            return language.OriginalInput.Trim();
        }

        // 4. Brak wiarygodnego źródła języka -> null (nie zgadujemy domyślnego English)
        return null;
    }

    private static int GetStreamPid(FlyleafLib.MediaFramework.MediaStream.StreamBase stream)
    {
        try
        {
            unsafe
            {
                var avStream = stream.AVStream;
                if (avStream != null)
                {
                    return avStream->id;
                }
            }
        }
        catch
        {
        }

        return -1;
    }

    private void ApplyVolume()
    {
        if (_volumePercent <= 0)
        {
            _player.Audio.Mute = true;
            _player.Audio.Volume = 0;
            return;
        }

        _player.Audio.Mute = false;
        double normalized = _volumePercent / 100.0;
        // Cubic audio curve matching LibVLC perceptual loudness (aout_VolumeSoft / vlc_audio_volume)
        double gain = Math.Pow(normalized, 3.0);
        int flyleafVolume = Math.Clamp((int)Math.Round(100.0 * gain), 1, 100);
        _player.Audio.Volume = flyleafVolume;
    }

    private void OnPlayerPlaybackStopped(object? sender, PlaybackStoppedArgs e)
    {
        if (_disposed || _mediaPath is null)
        {
            return;
        }

        if (_isActionInProgress || _player.Status == FlyleafStatus.Paused || _state == PlaybackState.Paused)
        {
            return;
        }

        if (_state == PlaybackState.Playing)
        {
            var capturedSessionId = Volatile.Read(ref _currentSessionId);
            if (capturedSessionId == 0)
            {
                return;
            }

            _isStopped = true;
            _stoppedPosition = Math.Max(0, _player.Duration / 10000);
            SetState(PlaybackState.Stopped);
            PostToDispatcher(() =>
            {
                if (_disposed || Volatile.Read(ref _currentSessionId) != capturedSessionId)
                {
                    return;
                }

                PlaybackEnded?.Invoke(this, new PlaybackEndedEventArgs(capturedSessionId));
            });
        }
    }

    private void SetState(PlaybackState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        PostToDispatcher(() => StateChanged?.Invoke(this, EventArgs.Empty));
    }

    private void PostToDispatcher(Action action)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher is null)
        {
            action();
            return;
        }

        if (_dispatcher is { IsDisposed: false, IsHandleCreated: true })
        {
            try
            {
                _dispatcher.BeginInvoke(action);
                return;
            }
            catch (InvalidOperationException)
            {
                // The WinForms dispatcher is unavailable; discard the callback.
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Workaround dla FlyleafLib 3.11.3:
    /// Domyślny stan mieszania napisów (Renderer.blendDesc) definiuje SourceBlendAlpha=Zero i DestinationBlendAlpha=Zero.
    /// W rezultacie rasteryzacja bitmapowych napisów (np. PGS) zeruje kanał alfa w BackBufferze w obszarze napisu.
    /// Ponieważ FlyleafHost pod WinForms używa DirectComposition (AlphaMode.Premultiplied), kompozytor DWM
    /// traktuje ten obszar jako przezroczysty na tło okna WinForms, powodując białe prostokąty w trybie okienkowym.
    /// Ustawienie DestinationBlendAlpha=Blend.One zachowuje nieprzezroczystość bufora wideo (Alpha=1.0)
    /// i eliminuje białe prostokąty bez wpływu na jakość ani blending RGB grafiki PGS.
    /// </summary>
    private static void FixRendererSubtitleBlendState()
    {
        try
        {
            var rendererType = typeof(FlyleafPlayer).Assembly.GetType("FlyleafLib.MediaFramework.MediaRenderer.Renderer");
            var blendDescField = rendererType?.GetField("blendDesc", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (blendDescField?.GetValue(null) is Vortice.Direct3D11.BlendDescription desc)
            {
                desc.RenderTarget[0].SourceBlendAlpha = Vortice.Direct3D11.Blend.Zero;
                desc.RenderTarget[0].DestinationBlendAlpha = Vortice.Direct3D11.Blend.One;
                blendDescField.SetValue(null, desc);
            }
        }
        catch
        {
            // Fail-safe: jeśli w przyszłej wersji FlyleafLib pole zmieni nazwę, typ lub strukturę,
            // nie rzucaj wyjątku i pozwól aplikacji działać normalnie.
        }
    }
}
