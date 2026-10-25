using System.Globalization;

namespace SubEdit.Player.Core.Playback;

public enum AudioCodecVariant
{
    None, Atmos, DtsHdHra, DtsHdMa, DtsX, DtsXImax, DtsEs, Dts96_24, DtsExpress
}

/// <summary>Shared detailed/compact names. Variants must be confirmed by the adapter, never inferred from titles.</summary>
public static class MediaCodecDisplay
{
    public static string? AudioName(string? codec, AudioCodecVariant variant = AudioCodecVariant.None, bool detailed = true)
    {
        var name = codec?.Trim();
        return name?.ToLowerInvariant() switch
        {
            "ac3" or "ac-3" => detailed ? "Dolby Digital (AC-3)" : "AC-3",
            "eac3" or "e-ac-3" or "e-ac3" => variant == AudioCodecVariant.Atmos
                ? detailed ? "Dolby Digital Plus / Atmos (JOC)" : "E-AC-3"
                : detailed ? "Dolby Digital Plus (E-AC-3)" : "E-AC-3",
            "truehd" => !detailed ? "TrueHD" : variant == AudioCodecVariant.Atmos ? "Dolby TrueHD / Atmos" : "Dolby TrueHD",
            "dts" or "dca" => variant switch
            {
                AudioCodecVariant.DtsHdHra => "DTS-HD HRA",
                AudioCodecVariant.DtsHdMa => detailed ? "DTS-HD Master Audio" : "DTS-HD MA",
                AudioCodecVariant.DtsX => detailed ? "DTS-HD Master Audio / DTS:X" : "DTS:X",
                AudioCodecVariant.DtsXImax => detailed ? "DTS-HD Master Audio / DTS:X IMAX" : "DTS:X IMAX",
                AudioCodecVariant.DtsEs => "DTS-ES",
                AudioCodecVariant.Dts96_24 => "DTS 96/24",
                AudioCodecVariant.DtsExpress => "DTS Express",
                _ => "DTS"
            },
            "aac" or "aac_latm" => "AAC",
            "flac" => "FLAC",
            "opus" => "Opus",
            "mp3" => "MP3",
            _ when name?.StartsWith("pcm_", StringComparison.OrdinalIgnoreCase) == true => detailed ? $"PCM ({name})" : "PCM",
            _ => name
        };
    }

    public static string? AudioFormat(string? codec, AudioCodecVariant variant = AudioCodecVariant.None, string? profile = null)
    {
        var name = AudioName(codec, variant, detailed: false);
        return codec?.Trim().ToLowerInvariant() switch
        {
            "eac3" or "e-ac-3" or "e-ac3" when variant == AudioCodecVariant.Atmos => "E-AC-3 JOC",
            "truehd" when variant == AudioCodecVariant.Atmos => "TrueHD / Atmos",
            "aac" or "aac_latm" => profile?.Trim().ToLowerInvariant() switch
            {
                "lc" => "AAC LC", "main" => "AAC Main", "ssr" => "AAC SSR", "ltp" => "AAC LTP",
                "ld" => "AAC LD", "eld" => "AAC ELD", "he-aac" => "HE-AAC",
                "he-aacv2" => "HE-AACv2", "xhe-aac" => "xHE-AAC", _ => "AAC"
            },
            var raw when raw?.StartsWith("pcm_", StringComparison.Ordinal) == true => $"PCM ({codec})",
            _ => name
        };
    }

    public static string? VideoName(string? codec, bool detailed = true) => MediaInspector.NormalizeVideoCodecName(codec) switch
    {
        "H.264" => detailed ? "H.264 / AVC" : "H.264",
        "H.265" => detailed ? "H.265 / HEVC" : "H.265",
        "MPEG2" => "MPEG2",
        var name when name.Equals("mpeg4", StringComparison.OrdinalIgnoreCase) => "MPEG-4",
        var name when name.Equals("av1", StringComparison.OrdinalIgnoreCase) => "AV1",
        var name when name.Equals("vp9", StringComparison.OrdinalIgnoreCase) => "VP9",
        var name when name.Equals("vp8", StringComparison.OrdinalIgnoreCase) => "VP8",
        var name => name
    };

    public static string? VideoLevel(string? codec, int level)
    {
        var name = VideoName(codec, detailed: false);
        if (name == "H.264")
        {
            if (level == 9) return "1b";
            if (level is 10 or 11 or 12 or 13 or 20 or 21 or 22 or 30 or 31 or 32 or 40 or 41 or 42 or 50 or 51 or 52 or 60 or 61 or 62)
                return (level / 10d).ToString("0.0", CultureInfo.InvariantCulture);
        }
        if (name == "H.265" && level is 30 or 60 or 63 or 90 or 93 or 120 or 123 or 150 or 153 or 156 or 180 or 183 or 186)
            return (level / 30d).ToString("0.0", CultureInfo.InvariantCulture);
        return null;
    }

    public static string? Profile(string? profile, string? level) => string.IsNullOrWhiteSpace(profile) ? null
        : string.IsNullOrWhiteSpace(level) ? profile : $"{profile}@L{level}";

    public static string? SubtitleName(string? codec) => codec?.Trim().ToLowerInvariant() switch
    {
        "subrip" or "srt" => "SRT",
        "ass" => "ASS", "ssa" => "SSA", "webvtt" => "WebVTT",
        "hdmv_pgs_subtitle" => "PGS", "dvd_subtitle" => "VobSub", "dvb_subtitle" => "DVB",
        "mov_text" => "Timed Text", "text" => "Tekst", _ => codec
    };

    public static string? PixelFormat(string? format) => format?.Trim().ToLowerInvariant() switch
    {
        "yuv420p" or "yuvj420p" or "nv12" => "YUV 4:2:0",
        "yuv422p" or "yuvj422p" or "nv16" => "YUV 4:2:2",
        "yuv444p" or "yuvj444p" or "nv24" => "YUV 4:4:4",
        "p010le" or "p010be" => "YUV 4:2:0 (10 bit)",
        "rgb24" or "bgr24" => "RGB (24 bit)",
        "gray" or "gray8" => "Skala szarości (8 bit)",
        var name when name?.StartsWith("yuv420p", StringComparison.Ordinal) == true => $"YUV 4:2:0 ({format})",
        var name when name?.StartsWith("yuv422p", StringComparison.Ordinal) == true => $"YUV 4:2:2 ({format})",
        var name when name?.StartsWith("yuv444p", StringComparison.Ordinal) == true => $"YUV 4:4:4 ({format})",
        _ => format
    };

    public static string? Color(string? color) => color?.Trim().ToLowerInvariant() switch
    {
        "bt709" => "BT.709", "bt470bg" => "BT.470 BG", "smpte170m" => "SMPTE 170M",
        "bt2020" => "BT.2020", "bt2020nc" => "BT.2020 (non-constant)", "bt2020c" => "BT.2020 (constant)",
        "smpte2084" => "PQ (SMPTE ST 2084)", "arib-std-b67" => "HLG (ARIB STD-B67)",
        "tv" => "Ograniczony (Limited)", "pc" => "Pełny (Full)", _ => color
    };

    public static string? EffectiveLayout(string? layout) => layout?.Trim().ToLowerInvariant() switch
    {
        "mono" => "1.0", "stereo" => "2.0",
        "5.1(side)" => "5.1", "7.1(wide)" or "7.1(wide-side)" => "7.1",
        _ => layout
    };
}
