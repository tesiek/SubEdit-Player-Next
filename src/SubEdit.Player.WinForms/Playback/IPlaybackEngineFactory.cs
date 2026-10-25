using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms.Playback;

internal interface IPlaybackEngineFactory
{
    IPlaybackEngine CreateEngine(
        Control? dispatcher,
        int initialVolume,
        Func<VideoDecodingMode> videoDecodingModeProvider);

    IVideoHost CreateVideoHost(
        Panel container,
        IPlaybackEngine playbackEngine,
        ContextMenuStrip contextMenu);
}
