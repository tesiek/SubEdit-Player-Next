using Flyleaf.FFmpeg;
using FlyleafLib;

namespace SubEdit.Player.WinForms.Playback;

internal static class FlyleafBootstrap
{
    private static readonly Lock _syncLock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (_syncLock)
        {
            if (_initialized)
            {
                return;
            }

            Engine.Start(new EngineConfig
            {
                FFmpegPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "FFmpeg"),
                FFmpegLoadProfile = LoadProfile.Filters,
                UIRefresh = false
            });

            _initialized = true;
        }
    }
}
