namespace SubEdit.Player.Core.Playback;

/// <summary>A detached, read-only view of the currently loaded file. No backend handles.</summary>
public sealed class VideoFileProperties
{
    public VideoFileProperties(string fileName, long? fileSizeBytes = null, string? container = null,
        long? durationMilliseconds = null, long? bitrate = null,
        IEnumerable<VideoStreamProperties>? video = null, IEnumerable<AudioStreamProperties>? audio = null,
        IEnumerable<SubtitleStreamProperties>? subtitles = null, IEnumerable<PlaybackChapter>? chapters = null)
    {
        FileName = fileName;
        FileSizeBytes = fileSizeBytes;
        Container = container;
        DurationMilliseconds = durationMilliseconds;
        Bitrate = bitrate;
        Video = Array.AsReadOnly(video?.ToArray() ?? []);
        Audio = Array.AsReadOnly(audio?.ToArray() ?? []);
        Subtitles = Array.AsReadOnly(subtitles?.ToArray() ?? []);
        Chapters = Array.AsReadOnly(chapters?.ToArray() ?? []);
    }

    public string FileName { get; }
    public long? FileSizeBytes { get; }
    public string? Container { get; }
    public long? DurationMilliseconds { get; }
    public long? Bitrate { get; }
    public IReadOnlyList<VideoStreamProperties> Video { get; }
    public IReadOnlyList<AudioStreamProperties> Audio { get; }
    public IReadOnlyList<SubtitleStreamProperties> Subtitles { get; }
    public IReadOnlyList<PlaybackChapter> Chapters { get; }
}

public sealed record FileStreamProperties(int Index, int? NativeId = null, string? Codec = null,
    string? Profile = null, string? Language = null, string? Title = null,
    bool? IsDefault = null, bool? IsForced = null);

public sealed record VideoStreamProperties(FileStreamProperties Stream, int? Width = null, int? Height = null,
    double? FramesPerSecond = null, long? Bitrate = null, string? PixelFormat = null, int? BitDepth = null,
    string? ColorSpace = null, string? ColorPrimaries = null, string? ColorTransfer = null,
    string? ColorRange = null, string? DynamicRange = null, string? Level = null,
    long? DisplayAspectRatioNumerator = null, long? DisplayAspectRatioDenominator = null,
    int? FrameRateNumerator = null, int? FrameRateDenominator = null);

public sealed record AudioStreamProperties(FileStreamProperties Stream, AudioCodecVariant CodecVariant = AudioCodecVariant.None,
    int? Channels = null, string? ChannelLayout = null, bool IsChannelLayoutConfirmed = false,
    int? SampleRate = null, long? Bitrate = null, string? EffectiveChannelLayout = null);

public sealed record SubtitleStreamProperties(FileStreamProperties Stream, bool? IsHearingImpaired = null);
