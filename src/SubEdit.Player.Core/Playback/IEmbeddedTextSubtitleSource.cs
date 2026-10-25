namespace SubEdit.Player.Core.Playback;

public interface IEmbeddedTextSubtitleSource
{
    string? CurrentEmbeddedSubtitleText { get; }

    event EventHandler? EmbeddedSubtitleTextChanged;
}
