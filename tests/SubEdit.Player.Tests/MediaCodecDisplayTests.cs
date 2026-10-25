using Flyleaf.FFmpeg;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Playback;
using Xunit;
using static Flyleaf.FFmpeg.Raw;

namespace SubEdit.Player.Tests;

public class MediaCodecDisplayTests
{
    [Theory]
    [InlineData("ac3", AudioCodecVariant.None, "Dolby Digital (AC-3)", "AC-3")]
    [InlineData("eac3", AudioCodecVariant.None, "Dolby Digital Plus (E-AC-3)", "E-AC-3")]
    [InlineData("eac3", AudioCodecVariant.Atmos, "Dolby Digital Plus / Atmos (JOC)", "E-AC-3")]
    [InlineData("truehd", AudioCodecVariant.Atmos, "Dolby TrueHD / Atmos", "TrueHD")]
    [InlineData("dts", AudioCodecVariant.DtsHdHra, "DTS-HD HRA", "DTS-HD HRA")]
    [InlineData("dts", AudioCodecVariant.DtsHdMa, "DTS-HD Master Audio", "DTS-HD MA")]
    [InlineData("dts", AudioCodecVariant.DtsX, "DTS-HD Master Audio / DTS:X", "DTS:X")]
    [InlineData("dts", AudioCodecVariant.DtsXImax, "DTS-HD Master Audio / DTS:X IMAX", "DTS:X IMAX")]
    [InlineData("unknown_codec", AudioCodecVariant.None, "unknown_codec", "unknown_codec")]
    public void Audio_UsesSameIdentityInReportAndActiveTrackStatus(string codec, AudioCodecVariant variant, string detailed, string compact)
    {
        var snapshot = new VideoFileProperties("movie", audio: [new(new(7, Codec: codec), variant, Channels: 6)]);
        Assert.Contains($"Kodek: {detailed}", VideoFilePropertiesFormatter.FormatText(snapshot));
        Assert.Equal($"{compact} 6 ch", Form1.GetAudioTrackStatus([Track(7, codec, variant)], 7));
    }

    [Theory]
    [InlineData(AVCodecID.Eac3, AV_PROFILE_EAC3_DDP_ATMOS, AudioCodecVariant.Atmos)]
    [InlineData(AVCodecID.Truehd, AV_PROFILE_TRUEHD_ATMOS, AudioCodecVariant.Atmos)]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_MA_X, AudioCodecVariant.DtsX)]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_MA, AudioCodecVariant.DtsHdMa)]
    [InlineData(AVCodecID.Eac3, AV_PROFILE_UNKNOWN, AudioCodecVariant.None)]
    [InlineData(AVCodecID.Dts, 123456, AudioCodecVariant.None)]
    [InlineData(AVCodecID.Aac, AV_PROFILE_EAC3_DDP_ATMOS, AudioCodecVariant.None)]
    public void AdapterVariant_RequiresMatchingCodecAndExplicitProfile(AVCodecID codec, int profile, AudioCodecVariant expected)
        => Assert.Equal(expected, FlyleafAudioFormat.GetVariant(codec, profile));

    [Fact]
    public void ActiveTrack_SwitchChangesCodecAndDoesNotInferAtmosFromTitle()
    {
        var tracks = new[]
        {
            Track(4, "eac3") with { Name = "Dolby Atmos", Description = "Atmos", EffectiveChannelLayout = "5.1(side)" },
            Track(9, "dts", AudioCodecVariant.DtsHdMa) with { EffectiveChannelLayout = "stereo", Channels = 2 }
        };
        Assert.Equal("E-AC-3 5.1", Form1.GetAudioTrackStatus(tracks, 4));
        Assert.Equal("DTS-HD MA 2.0", Form1.GetAudioTrackStatus(tracks, 9));
        Assert.Equal("", Form1.GetAudioTrackStatus(tracks, 8));
        Assert.Equal("", Form1.GetAudioTrackStatus(tracks, null));
        Assert.Equal("", Form1.GetAudioTrackStatus([tracks[0] with { HasDetails = false }], 4));
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(6u)]
    [InlineData(8u)]
    [InlineData(10u)]
    public void StatusWithoutReadLayout_UsesCountNotGuessedSpeakerConfiguration(uint channels)
        => Assert.Equal($"AAC {channels} ch", Form1.GetAudioTrackStatus([Track(1, "aac") with { Channels = channels }], 1));

    [Fact]
    public void EffectiveLayout_IsNotRelabelledAsConfirmedSourceLayout()
    {
        var audio = new AudioStreamProperties(new(1), Channels: 6, EffectiveChannelLayout: "5.1(side)");
        Assert.Equal("6 kanałów", VideoFilePropertiesFormatter.Channels(audio));
        Assert.Equal("8 kanałów (7.1)", VideoFilePropertiesFormatter.Channels(audio with
        {
            Channels = 8, ChannelLayout = "7.1", IsChannelLayoutConfirmed = true
        }));
    }

    [Theory]
    [InlineData("h264", "H.264 / AVC", "H.264", 31, "3.1")]
    [InlineData("AVC", "H.264 / AVC", "H.264", 10, "1.0")]
    [InlineData("h264", "H.264 / AVC", "H.264", 9, "1b")]
    [InlineData("hevc", "H.265 / HEVC", "H.265", 153, "5.1")]
    [InlineData("h265", "H.265 / HEVC", "H.265", 120, "4.0")]
    [InlineData("hevc", "H.265 / HEVC", "H.265", 31, null)]
    [InlineData("h264", "H.264 / AVC", "H.264", -99, null)]
    [InlineData("new_codec", "new_codec", "new_codec", 31, null)]
    public void Video_DetailedAndCompactNamesShareIdentityAndUseCodecSpecificLevel(string codec, string detailed, string compact, int level, string? expected)
    {
        Assert.Equal(detailed, MediaCodecDisplay.VideoName(codec));
        Assert.Equal(compact, MediaCodecDisplay.VideoName(codec, detailed: false));
        Assert.Equal(expected, MediaCodecDisplay.VideoLevel(codec, level));
    }

    [Theory]
    [InlineData("movie.mkv", "matroska,webm", "Matroska (MKV)")]
    [InlineData("movie.MKV", "matroska,webm", "Matroska (MKV)")]
    [InlineData("movie.webm", "matroska,webm", "WebM")]
    [InlineData("movie.bin", "matroska,webm", "Matroska / WebM")]
    [InlineData("mislabelled.mkv", "mov,mp4,m4a,3gp,3g2,mj2", "QuickTime / MOV")]
    public void Container_RequiresDetectedDemuxerBeforeUsingExtension(string path, string demuxer, string expected)
        => Assert.Equal(expected, LoadedMediaMetadata.ContainerName(demuxer, path, "QuickTime / MOV"));

    [Theory]
    [InlineData(0L, "0 KB")]
    [InlineData(1023L, "0 KB")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1571165184L, "1 534 341 KB")]
    [InlineData(-1L, "—")]
    [InlineData(null, "—")]
    public void Size_IsIntegerKiBWithSpaceGrouping(long? bytes, string expected)
        => Assert.Contains($"Rozmiar: {expected}", VideoFilePropertiesFormatter.FormatText(new VideoFileProperties("film", bytes)));

    [Theory]
    [InlineData(2548000L, "2548 kb/s")]
    [InlineData(3193499L, "3193 kb/s")]
    [InlineData(3193500L, "3194 kb/s")]
    [InlineData(0L, "—")]
    [InlineData(null, "—")]
    public void Bitrate_IsWholeKbpsNotConfusedWithTotalVideoOrAudio(long? bits, string expected)
    {
        var snapshot = new VideoFileProperties("film", bitrate: bits, video: [new(new(0), Bitrate: null)]);
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        Assert.Equal(expected, Assert.Single(sections[0].Rows, row => row.Label == "Średni bitrate pliku").Value);
        Assert.Equal("—", Assert.Single(sections[1].Rows, row => row.Label == "Bitrate").Value);
    }

    [Theory]
    [InlineData("2548000", 2548000L)]
    [InlineData("2548000.4", 2548000L)]
    [InlineData("2.548e6", 2548000L)]
    [InlineData("-2", null)]
    [InlineData("0", null)]
    [InlineData("invalid", null)]
    [InlineData("9223372036854775808", null)]
    public void StreamBitrate_ReadsPositiveLoadedBpsOnly(string bps, long? expected)
        => Assert.Equal(expected, LoadedMediaMetadata.StreamBitrate(0, new Dictionary<string, string> { ["BPS-eng"] = bps }));

    [Fact]
    public void StreamBitrate_PrefersCodecParametersAndRejectsConflictingTags()
    {
        var metadata = new Dictionary<string, string> { ["BPS"] = "2548000", ["BPS-eng"] = "2400000" };
        Assert.Equal(3000000L, LoadedMediaMetadata.StreamBitrate(3000000, metadata));
        Assert.Null(LoadedMediaMetadata.StreamBitrate(0, metadata));
        Assert.Equal(2800000L, LoadedMediaMetadata.StreamBitrate(0, metadata, 2800000));
        Assert.Null(LoadedMediaMetadata.StreamBitrate(0, new Dictionary<string, string> { ["BITRATE"] = "2548000" }));
        Assert.Equal(2548000L, LoadedMediaMetadata.StreamBitrate(0, new Dictionary<string, string> { ["bps"] = "2548000" }));
    }

    [Fact]
    public void Report_FormatsProfilePixelsAndColorsAndRemovesVideoIdentityLanguageAndTitle()
    {
        var snapshot = new VideoFileProperties("film", video: [new(new(22, 1234, "h264", "High", "xyz", "video title"),
            PixelFormat: "yuv420p", ColorSpace: "bt709", ColorPrimaries: "bt709", ColorRange: "tv", Level: "3.1")],
            subtitles: [new(new(5, Language: "unknown-code", Codec: "subrip"))]);
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        var video = sections[1];
        Assert.Equal("WIDEO #1", video.Heading);
        Assert.DoesNotContain(video.Rows, row => row.Label is "Język" or "Tytuł");
        var report = VideoFilePropertiesFormatter.FormatText(sections);
        Assert.Contains("Kodek: H.264 / AVC", report);
        Assert.Contains("Profil: High@L3.1", report);
        Assert.Contains("Format pikseli: YUV 4:2:0", report);
        Assert.Contains("Przestrzeń barw: BT.709", report);
        Assert.Contains("Zakres barw: Ograniczony (Limited)", report);
        Assert.Contains("#1 — unknown-code | SRT", report);
        Assert.DoesNotContain("1234", report);
        Assert.Equal("High", MediaCodecDisplay.Profile("High", null));
        Assert.Null(MediaCodecDisplay.Profile(null, "3.1"));
        Assert.Equal("new_pixel_format", MediaCodecDisplay.PixelFormat("new_pixel_format"));
        Assert.Equal("new_color", MediaCodecDisplay.Color("new_color"));
    }

    private static PlaybackAudioTrack Track(int id, string codec, AudioCodecVariant variant = AudioCodecVariant.None)
        => new(id, "Track", true, null, null, codec, 48000, 6, 0, variant);

    [Theory]
    [InlineData("ac3", AudioCodecVariant.None, null, "AC-3")]
    [InlineData("eac3", AudioCodecVariant.None, "unknown", "E-AC-3")]
    [InlineData("eac3", AudioCodecVariant.Atmos, "Dolby Digital Plus + Dolby Atmos", "E-AC-3 JOC")]
    [InlineData("truehd", AudioCodecVariant.None, null, "TrueHD")]
    [InlineData("truehd", AudioCodecVariant.Atmos, "Dolby TrueHD + Dolby Atmos", "TrueHD / Atmos")]
    [InlineData("dts", AudioCodecVariant.None, "unknown", "DTS")]
    [InlineData("dts", AudioCodecVariant.DtsHdHra, null, "DTS-HD HRA")]
    [InlineData("dts", AudioCodecVariant.DtsHdMa, null, "DTS-HD MA")]
    [InlineData("dts", AudioCodecVariant.DtsX, null, "DTS:X")]
    [InlineData("dts", AudioCodecVariant.DtsXImax, null, "DTS:X IMAX")]
    [InlineData("aac", AudioCodecVariant.None, "LC", "AAC LC")]
    [InlineData("aac_latm", AudioCodecVariant.None, "he-aac", "HE-AAC")]
    [InlineData("aac", AudioCodecVariant.None, "HE-AACv2", "HE-AACv2")]
    [InlineData("aac", AudioCodecVariant.None, "xHE-AAC", "xHE-AAC")]
    [InlineData("aac", AudioCodecVariant.None, "Main", "AAC Main")]
    [InlineData("aac", AudioCodecVariant.None, "SSR", "AAC SSR")]
    [InlineData("aac", AudioCodecVariant.None, "LTP", "AAC LTP")]
    [InlineData("aac", AudioCodecVariant.None, "LD", "AAC LD")]
    [InlineData("aac", AudioCodecVariant.None, "ELD", "AAC ELD")]
    [InlineData("aac", AudioCodecVariant.None, "unknown", "AAC")]
    [InlineData("flac", AudioCodecVariant.None, null, "FLAC")]
    [InlineData("opus", AudioCodecVariant.None, null, "Opus")]
    [InlineData("pcm_s24le", AudioCodecVariant.None, null, "PCM (pcm_s24le)")]
    [InlineData("unknown_codec", AudioCodecVariant.None, null, "unknown_codec")]
    public void AudioFormat_UsesExistingCodecIdentityAndConfirmedVariant(string codec, AudioCodecVariant variant,
        string? profile, string expected)
        => Assert.Equal(expected, MediaCodecDisplay.AudioFormat(codec, variant, profile));

    [Fact]
    public void AudioFormatCodecProfile_AreDistinctAndTitlesCannotInventJoc()
    {
        const string profile = "Dolby Digital Plus + Dolby Atmos";
        var audio = new AudioStreamProperties(new(1, Codec: "eac3", Profile: profile), AudioCodecVariant.Atmos, Channels: 6);
        var rows = VideoFilePropertiesFormatter.GetSections(new("film", audio: [audio]))
            .Single(section => section.Heading == "AUDIO #1").Rows;
        Assert.Equal("Format", rows[0].Label);
        Assert.Equal("E-AC-3 JOC", rows[0].Value);
        Assert.Equal("Kodek", rows[1].Label);
        Assert.Equal("Dolby Digital Plus / Atmos (JOC)", rows[1].Value);
        Assert.Equal("Profil", rows[2].Label);
        Assert.Equal(profile, rows[2].Value);
        var report = VideoFilePropertiesFormatter.FormatText(new VideoFileProperties("Atmos.mkv", audio:
            [audio with { CodecVariant = AudioCodecVariant.None, Stream = audio.Stream with { Profile = null, Title = "Atmos JOC" } }]));
        Assert.Contains("Format: E-AC-3" + Environment.NewLine, report);
        Assert.DoesNotContain("Format: E-AC-3 JOC", report);
        Assert.DoesNotContain("Kodek: Dolby Digital Plus / Atmos", report);
    }
}
