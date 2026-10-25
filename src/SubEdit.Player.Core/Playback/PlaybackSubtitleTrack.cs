namespace SubEdit.Player.Core.Playback;

public sealed record PlaybackSubtitleTrack(
    int Id,
    string? Name,
    bool HasDetails,
    string? Description,
    string? Language);
