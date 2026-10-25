namespace SubEdit.Player.Core.Playback;

public sealed class PlaybackEndedEventArgs : EventArgs
{
    public PlaybackEndedEventArgs(long sessionId)
    {
        SessionId = sessionId;
    }

    public long SessionId { get; }
}
