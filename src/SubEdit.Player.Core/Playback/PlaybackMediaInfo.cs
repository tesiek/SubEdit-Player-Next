namespace SubEdit.Player.Core.Playback;

public sealed record PlaybackMediaInfo(
    bool HasVideo,
    int Width,
    int Height,
    int DisplayWidth,
    int DisplayHeight,
    string VideoCodecName,
    string VideoFourCc,
    bool? IsHardwareAccelerated = null);
