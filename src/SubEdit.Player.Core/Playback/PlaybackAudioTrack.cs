namespace SubEdit.Player.Core.Playback;

public sealed record PlaybackAudioTrack(
    int Id,
    string? Name,
    bool HasDetails,
    string? Description,
    string? Language,
    string? CodecDescription,
    uint SampleRate,
    uint Channels,
    uint Bitrate,
    AudioCodecVariant CodecVariant = AudioCodecVariant.None,
    string? EffectiveChannelLayout = null);
