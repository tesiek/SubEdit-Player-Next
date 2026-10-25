using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms.Playback;

internal sealed class FlyleafEngineFactory : IPlaybackEngineFactory
{
    public IPlaybackEngine CreateEngine(
        Control? dispatcher,
        int initialVolume,
        Func<VideoDecodingMode> videoDecodingModeProvider)
    {
        FlyleafBootstrap.EnsureInitialized();
        return new FlyleafPlaybackEngine(dispatcher, initialVolume, videoDecodingModeProvider);
    }

    public IVideoHost CreateVideoHost(
        Panel container,
        IPlaybackEngine playbackEngine,
        ContextMenuStrip contextMenu)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(playbackEngine);
        ArgumentNullException.ThrowIfNull(contextMenu);

        if (playbackEngine is not FlyleafPlaybackEngine flyleafPlayback)
        {
            throw new ArgumentException(
                "Playback engine must be a FlyleafPlaybackEngine instance for FlyleafVideoHost.",
                nameof(playbackEngine));
        }

        return new FlyleafVideoHost(
            container,
            flyleafPlayback,
            contextMenu);
    }
}
