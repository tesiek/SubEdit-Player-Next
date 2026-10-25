using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.Core.Playback;
using static Flyleaf.FFmpeg.Raw;

namespace SubEdit.Player.WinForms.Playback;

internal sealed partial class FlyleafPlaybackEngine
{
    internal VideoFileProperties? TryCaptureDiagnosticsProperties()
    {
        if (_dispatcher is { InvokeRequired: true })
            throw new InvalidOperationException("Diagnostics must be captured on the playback owner thread.");
        if (_disposed || !_tracksReady || _mediaPath is null) return null;
        var demuxer = _player.MainDemuxer;
        // Diagnostics never wait for demuxer I/O. The existing snapshot lock is reentrant.
        if (demuxer is null || demuxer.Disposed || !Monitor.TryEnter(demuxer.lockFmtCtx)) return null;
        try { return CaptureVideoFileProperties(includeFileSize: false); }
        finally { Monitor.Exit(demuxer.lockFmtCtx); }
    }
}

/// <summary>Backend-specific reads stay here; callers receive detached neutral values only.</summary>
internal static class PlaybackDiagnostics
{
    internal static DiagnosticMediaInfo Capture(IPlaybackEngine playback, string? path, string decodingMode)
    {
        if (!playback.IsMediaOpen) return new(false);
        var properties = (playback as FlyleafPlaybackEngine)?.TryCaptureDiagnosticsProperties();
        var info = playback.AreTracksReady ? playback.MediaInfo : null;
        var audio = playback.AudioTracks.FirstOrDefault(track => track.Id == playback.ActiveAudioTrackId);
        var audioName = audio is { HasDetails: true }
            ? MediaCodecDisplay.AudioName(audio.CodecDescription, audio.CodecVariant, detailed: false) : null;
        var layout = MediaCodecDisplay.EffectiveLayout(audio?.EffectiveChannelLayout);
        if (string.IsNullOrWhiteSpace(layout) && audio?.Channels is > 0) layout = $"{audio.Channels} ch";
        var audioDescription = string.Join(" ", new[] { audioName, layout }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var video = properties?.Video.FirstOrDefault(stream => stream.Stream.Index == playback.ActiveVideoTrackId);
        return new(true, properties?.FileName ?? (path is null ? null : Path.GetFileName(path)),
            Container: properties?.Container, DurationMilliseconds: properties?.DurationMilliseconds ??
                (playback.DurationMilliseconds > 0 ? playback.DurationMilliseconds : null),
            VideoCodec: video is null ? info?.VideoCodecName : MediaCodecDisplay.VideoName(video.Stream.Codec, detailed: false),
            Width: video?.Width ?? (info?.Width is > 0 ? info.Width : null),
            Height: video?.Height ?? (info?.Height is > 0 ? info.Height : null),
            Audio: audioDescription, DecodingMode: decodingMode,
            IsHardwareAccelerated: info is { HasVideo: true } ? info.IsHardwareAccelerated : null);
    }

    internal static IReadOnlyList<DiagnosticLibrary> CaptureFfmpegVersions()
    {
        var libraries = new List<DiagnosticLibrary>();
        if (!FlyleafLib.Engine.IsLoaded)
        {
            foreach (var name in new[] { "FFmpeg", "avcodec", "avformat", "avutil", "avfilter", "swresample", "swscale" })
                libraries.Add(new(name, null));
            return libraries.AsReadOnly();
        }
        try { libraries.Add(new("FFmpeg", av_version_info(), "runtime build")); }
        catch (Exception) { libraries.Add(new("FFmpeg", null)); }
        Add("avcodec", avcodec_version);
        Add("avformat", avformat_version);
        Add("avutil", avutil_version);
        Add("avfilter", avfilter_version);
        Add("swresample", swresample_version);
        Add("swscale", swscale_version);
        return libraries.AsReadOnly();

        void Add(string name, Func<uint> read)
        {
            try
            {
                var version = read();
                libraries.Add(new(name, $"{version >> 16}.{version >> 8 & 255}.{version & 255}", "runtime API"));
            }
            catch (Exception) { libraries.Add(new(name, null)); }
        }
    }
}
