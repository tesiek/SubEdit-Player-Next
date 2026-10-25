using System.Buffers.Binary;
using Flyleaf.FFmpeg;
using FlyleafLib.MediaFramework.MediaStream;
using SubEdit.Player.Core.Playback;
using static Flyleaf.FFmpeg.Raw;

namespace SubEdit.Player.WinForms.Playback;

internal sealed partial class FlyleafPlaybackEngine
{
    public unsafe VideoFileProperties? GetVideoFileProperties()
        => CaptureVideoFileProperties(includeFileSize: true);

    private unsafe VideoFileProperties? CaptureVideoFileProperties(bool includeFileSize)
    {
        // Open/Close/Dispose and this snapshot belong to the application's UI thread.
        // lockFmtCtx alone is not a lifecycle lock: Flyleaf Dispose does not acquire it.
        if (_dispatcher is { InvokeRequired: true })
            throw new InvalidOperationException("File properties must be captured on the playback owner thread.");
        if (_disposed || _mediaPath is null || !_tracksReady) return null;
        var path = _mediaPath;
        var fileSize = includeFileSize ? MediaInspector.TryGetFileSizeBytes(path) : null; // FileInfo only; no header probing.
        var demuxer = _player.MainDemuxer;
        if (demuxer is null || demuxer.Disposed) return null;

        // Only copy loaded metadata here. Never open media, switch streams or decode frames.
        lock (demuxer.lockFmtCtx)
        {
            var context = demuxer.FormatContext;
            if (context == null || demuxer.Disposed) return null;
            var video = demuxer.VideoStreams.Select(CaptureVideoProperties).ToArray();
            var audio = demuxer.AudioStreams.Select(stream =>
            {
                var info = CaptureStreamProperties(stream,
                    ResolveStreamLanguage(stream, demuxer.AudioStreams, _clipInfoStreams));
                var cp = stream.AVStream->codecpar;
                return new AudioStreamProperties(info,
                    FlyleafAudioFormat.GetVariant(cp->codec_id, cp->profile),
                    Positive(cp->ch_layout.nb_channels),
                    // Flyleaf overwrites unspecified layouts with a default. Provenance is lost.
                    // Do not expose its synthesized mask as a confirmed source speaker layout.
                    ChannelLayout: null, IsChannelLayoutConfirmed: false,
                    SampleRate: Positive(cp->sample_rate),
                    Bitrate: LoadedMediaMetadata.StreamBitrate(cp->bit_rate, stream.Metadata, stream.BitRate),
                    EffectiveChannelLayout: GetEffectiveAudioLayout(stream));
            }).ToArray();
            var subtitles = demuxer.SubtitlesStreams.Select(stream => new SubtitleStreamProperties(
                CaptureStreamProperties(stream, ResolvePropertiesLanguage(stream.Metadata, stream.Language?.OriginalInput,
                    ResolveStreamLanguage(stream, demuxer.SubtitlesStreams, _clipInfoStreams))),
                (stream.AVStream->disposition & DispositionFlags.HearingImpaired) != 0 ? true : null)).ToArray();
            var chapters = demuxer.Chapters.Select((chapter, i) =>
                new PlaybackChapter(i, chapter.Title, chapter.StartTime / 10000)).ToArray();
            var container = LoadedMediaMetadata.ContainerName(demuxer.Name, path, demuxer.LongName);
            return new VideoFileProperties(Path.GetFileName(path), fileSize, container,
                context->duration != AV_NOPTS_VALUE && context->duration > 0 ? context->duration / 1000 : null,
                Positive(context->bit_rate), video, audio, subtitles, chapters);
        }
    }

    private static unsafe FileStreamProperties CaptureStreamProperties(StreamBase stream, string? language = null)
    {
        var st = stream.AVStream;
        var cp = st->codecpar;
        var profile = cp->profile == AV_PROFILE_UNKNOWN ? null : avcodec_profile_name(cp->codec_id, cp->profile);
        return new FileStreamProperties(st->index, st->id, avcodec_get_name(cp->codec_id), profile,
            language ?? FormatLanguage(stream.Language, stream.Metadata), stream.Title,
            (st->disposition & DispositionFlags.Default) != 0, (st->disposition & DispositionFlags.Forced) != 0);
    }

    internal static string? ResolvePropertiesLanguage(Dictionary<string, string> metadata, string? originalInput, string? resolvedLanguage)
    {
        // Only the F9 snapshot uses this fallback. Preserve selection/statusbar language policy.
        foreach (var key in new[] { "language-ietf", "language_ietf", "language", "lang" })
        {
            var raw = metadata.FirstOrDefault(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value?.Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw.Equals("und", StringComparison.OrdinalIgnoreCase)) continue;
            var resolved = key is "language-ietf" or "language_ietf"
                ? FormatLanguage(null, new Dictionary<string, string> { ["language"] = raw }) : resolvedLanguage;
            return LoadedMediaMetadata.PropertiesLanguageName(raw, resolved);
        }
        return LoadedMediaMetadata.PropertiesLanguageName(originalInput, resolvedLanguage);
    }

    private static unsafe VideoStreamProperties CaptureVideoProperties(VideoStream stream)
    {
        var st = stream.AVStream;
        var cp = st->codecpar;
        var pixelFormat = (AVPixelFormat)cp->format;
        var descriptor = av_pix_fmt_desc_get(pixelFormat);
        int? depth = descriptor != null && (descriptor->flags & PixFmtFlags.Hwaccel) == 0 && descriptor->nb_components > 0
            ? descriptor->comp[0].depth : null;
        var fps = st->avg_frame_rate;
        if (fps.Num <= 0 || fps.Den <= 0) fps = cp->framerate;
        var crop = cp->coded_side_data != null && cp->nb_coded_side_data > 0
            ? av_packet_side_data_get(cp->coded_side_data, cp->nb_coded_side_data, AVPacketSideDataType.FrameCropping) : null;
        // Copy only source geometry. VideoStream.SAR may have a decoder/default fallback.
        var dar = crop != null && (crop->data == null || crop->size != 16)
            ? ((long?)null, (long?)null)
            : GetSourceDisplayAspectRatio(cp->width, cp->height, st->sample_aspect_ratio, cp->sample_aspect_ratio,
                crop == null ? ReadOnlySpan<byte>.Empty : new ReadOnlySpan<byte>(crop->data, 16));
        return new VideoStreamProperties(CaptureStreamProperties(stream), Positive(cp->width), Positive(cp->height),
            fps.Num > 0 && fps.Den > 0 ? (double)fps.Num / fps.Den : null,
            LoadedMediaMetadata.StreamBitrate(cp->bit_rate, stream.Metadata, stream.BitRate),
            pixelFormat == AVPixelFormat.None ? null : av_get_pix_fmt_name(pixelFormat), depth,
            cp->color_space == AVColorSpace.Unspecified ? null : av_color_space_name(cp->color_space),
            cp->color_primaries == AVColorPrimaries.Unspecified ? null : av_color_primaries_name(cp->color_primaries),
            cp->color_trc == AVColorTransferCharacteristic.Unspecified ? null : av_color_transfer_name(cp->color_trc),
            cp->color_range == AVColorRange.Unspecified ? null : av_color_range_name(cp->color_range),
            cp->color_trc switch
            {
                AVColorTransferCharacteristic.Smpte2084 => "HDR (PQ)",
                AVColorTransferCharacteristic.AribStdB67 => "HDR (HLG)",
                // Unspecified or another transfer does not prove absence of HDR side data.
                _ => null
            }, MediaCodecDisplay.VideoLevel(stream.Codec, cp->level), dar.Item1, dar.Item2,
            fps.Num > 0 && fps.Den > 0 ? fps.Num : null,
            fps.Num > 0 && fps.Den > 0 ? fps.Den : null);
    }

    internal static (long? Numerator, long? Denominator) GetSourceDisplayAspectRatio(
        int width, int height, AVRational streamSar, AVRational codecSar, ReadOnlySpan<byte> crop)
    {
        var sar = streamSar.Num > 0 && streamSar.Den > 0 ? streamSar : codecSar;
        if (width <= 0 || height <= 0 || sar.Num <= 0 || sar.Den <= 0) return (null, null);
        long visibleWidth = width, visibleHeight = height;
        if (!crop.IsEmpty)
        {
            if (crop.Length != 16) return (null, null);
            // FFmpeg FrameCropping: four little-endian uint32 values, top/bottom/left/right.
            visibleHeight -= (long)BinaryPrimitives.ReadUInt32LittleEndian(crop) + BinaryPrimitives.ReadUInt32LittleEndian(crop[4..]);
            visibleWidth -= (long)BinaryPrimitives.ReadUInt32LittleEndian(crop[8..]) + BinaryPrimitives.ReadUInt32LittleEndian(crop[12..]);
        }
        return visibleWidth > 0 && visibleHeight > 0
            ? (visibleWidth * sar.Num, visibleHeight * sar.Den) : (null, null);
    }

    private static unsafe AudioCodecVariant GetAudioCodecVariant(AudioStream stream)
    {
        var st = stream.AVStream;
        return st == null || st->codecpar == null ? AudioCodecVariant.None
            : FlyleafAudioFormat.GetVariant(st->codecpar->codec_id, st->codecpar->profile);
    }

    private unsafe string? GetEffectiveAudioLayout(AudioStream stream)
    {
        if (stream.StreamIndex != ActiveAudioTrackId) return null;
        var layout = stream.ChannelLayout;
        // Only a native mask is self-contained: custom layouts may borrow frame-owned pointers.
        if (layout.order != AVChannelOrder.Native || layout.nb_channels <= 0 || layout.u.mask == 0) return null;
        byte* buffer = stackalloc byte[256];
        var length = av_channel_layout_describe(&layout, buffer, 256);
        return length >= 0 && length < 256 ? System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)buffer) : null;
    }

    private static int? Positive(int value) => value > 0 ? value : null;
    private static long? Positive(long value) => value > 0 ? value : null;
}

/// <summary>Only explicit FFmpeg profiles identify enhanced variants; display names live in Core.</summary>
internal static class FlyleafAudioFormat
{
    public static AudioCodecVariant GetVariant(AVCodecID codec, int profile) => codec switch
    {
        AVCodecID.Eac3 when profile == AV_PROFILE_EAC3_DDP_ATMOS => AudioCodecVariant.Atmos,
        AVCodecID.Truehd when profile == AV_PROFILE_TRUEHD_ATMOS => AudioCodecVariant.Atmos,
        AVCodecID.Dts => profile switch
        {
            AV_PROFILE_DTS_HD_HRA => AudioCodecVariant.DtsHdHra,
            AV_PROFILE_DTS_HD_MA => AudioCodecVariant.DtsHdMa,
            AV_PROFILE_DTS_HD_MA_X => AudioCodecVariant.DtsX,
            AV_PROFILE_DTS_HD_MA_X_IMAX => AudioCodecVariant.DtsXImax,
            AV_PROFILE_DTS_ES => AudioCodecVariant.DtsEs,
            AV_PROFILE_DTS_96_24 => AudioCodecVariant.Dts96_24,
            AV_PROFILE_DTS_EXPRESS => AudioCodecVariant.DtsExpress,
            _ => AudioCodecVariant.None
        },
        _ => AudioCodecVariant.None
    };
}
