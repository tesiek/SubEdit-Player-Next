namespace SubEdit.Player.Core.Playback;

public sealed record PlaybackChapter(
    int Index,
    string? Name,
    long PositionMilliseconds);
