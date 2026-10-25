namespace SubEdit.Player.Core.Playback;

public sealed record PlaybackVideoTrack(
    int Id,
    string? Name,
    bool HasDetails,
    string? Description,
    string? Language,
    string? CodecDescription,
    uint Width,
    uint Height,
    double? FramesPerSecond,
    uint Bitrate);
