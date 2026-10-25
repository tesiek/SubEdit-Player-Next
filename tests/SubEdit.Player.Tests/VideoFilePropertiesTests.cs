using System.Buffers.Binary;
using System.Globalization;
using Flyleaf.FFmpeg;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Playback;
using Xunit;
using static Flyleaf.FFmpeg.Raw;

namespace SubEdit.Player.Tests;

public class VideoFilePropertiesTests
{
    [Theory]
    [InlineData(AVCodecID.Ac3, AV_PROFILE_UNKNOWN, "ac3", "Dolby Digital (AC-3)")]
    [InlineData(AVCodecID.Eac3, AV_PROFILE_UNKNOWN, "eac3", "Dolby Digital Plus (E-AC-3)")]
    [InlineData(AVCodecID.Eac3, AV_PROFILE_EAC3_DDP_ATMOS, "eac3", "Dolby Digital Plus / Atmos (JOC)")]
    [InlineData(AVCodecID.Truehd, AV_PROFILE_UNKNOWN, "truehd", "Dolby TrueHD")]
    [InlineData(AVCodecID.Truehd, AV_PROFILE_TRUEHD_ATMOS, "truehd", "Dolby TrueHD / Atmos")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_UNKNOWN, "dts", "DTS")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS, "dts", "DTS")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_HRA, "dts", "DTS-HD HRA")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_MA, "dts", "DTS-HD Master Audio")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_MA_X, "dts", "DTS-HD Master Audio / DTS:X")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_HD_MA_X_IMAX, "dts", "DTS-HD Master Audio / DTS:X IMAX")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_ES, "dts", "DTS-ES")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_96_24, "dts", "DTS 96/24")]
    [InlineData(AVCodecID.Dts, AV_PROFILE_DTS_EXPRESS, "dts", "DTS Express")]
    [InlineData(AVCodecID.Aac, 1, "aac", "AAC")]
    [InlineData(AVCodecID.AacLatm, 1, "aac_latm", "AAC")]
    [InlineData(AVCodecID.Flac, AV_PROFILE_UNKNOWN, "flac", "FLAC")]
    [InlineData(AVCodecID.Opus, AV_PROFILE_UNKNOWN, "opus", "Opus")]
    [InlineData(AVCodecID.PcmS24le, AV_PROFILE_UNKNOWN, "pcm_s24le", "PCM (pcm_s24le)")]
    [InlineData(AVCodecID.Mp3, AV_PROFILE_UNKNOWN, "mp3", "MP3")]
    [InlineData(AVCodecID.None, AV_PROFILE_UNKNOWN, null, null)]
    public void AudioNames_UseExplicitCodecAndProfileOnly(AVCodecID codec, int profile, string? technical, string? expected)
    {
        Assert.Equal(expected, MediaCodecDisplay.AudioName(technical, FlyleafAudioFormat.GetVariant(codec, profile)));
    }

    [Theory]
    [InlineData(AVCodecID.Eac3)]
    [InlineData(AVCodecID.Truehd)]
    [InlineData(AVCodecID.Dts)]
    public void UnrecognizedProfile_DoesNotInventImmersiveOrHdVariant(AVCodecID codec)
    {
        var variant = FlyleafAudioFormat.GetVariant(codec, 123456);
        Assert.Equal(AudioCodecVariant.None, variant);
        var name = MediaCodecDisplay.AudioName(codec.ToString(), variant);
        Assert.NotNull(name);
        Assert.DoesNotContain("Atmos", name);
        Assert.DoesNotContain("DTS:X", name);
        Assert.DoesNotContain("DTS-HD", name);
    }

    [Theory]
    [InlineData(2, "2 kanały")]
    [InlineData(6, "6 kanałów")]
    [InlineData(8, "8 kanałów")]
    [InlineData(10, "10 kanałów")]
    public void UnconfirmedLayout_DoesNotExposeSyntheticSpeakerConfiguration(int channels, string expected)
    {
        var audio = new AudioStreamProperties(new(1), Channels: channels, ChannelLayout: "7.1.2");
        Assert.Equal(expected, VideoFilePropertiesFormatter.Channels(audio));
    }

    [Fact]
    public void ConfirmedLayout_RequiresBothProvenanceAndNonEmptyDescription()
    {
        var audio = new AudioStreamProperties(new(1), Channels: 6, ChannelLayout: "5.1(side)", IsChannelLayoutConfirmed: true);
        Assert.Equal("6 kanałów (5.1)", VideoFilePropertiesFormatter.Channels(audio));
        Assert.Equal("6 kanałów", VideoFilePropertiesFormatter.Channels(audio with { ChannelLayout = null }));
        Assert.Equal("—", VideoFilePropertiesFormatter.Channels(audio with { Channels = null }));
    }

    [Fact]
    public void Snapshot_DetachesCollectionsAndPreservesIndexVersusNativeId()
    {
        var tracks = new List<AudioStreamProperties> { new(new(3, 0x1100, "ac3")) };
        var chapters = new List<PlaybackChapter> { new(0, "Pierwszy", 0) };
        var snapshot = new VideoFileProperties("film.m2ts", audio: tracks, chapters: chapters);
        tracks.Clear();
        chapters.Clear();
        Assert.Single(snapshot.Audio);
        Assert.Single(snapshot.Chapters);
        Assert.Throws<NotSupportedException>(() => ((IList<AudioStreamProperties>)snapshot.Audio).Clear());
        var text = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Equal(3, snapshot.Audio[0].Stream.Index);
        Assert.Equal(0x1100, snapshot.Audio[0].Stream.NativeId);
        Assert.Contains("[AUDIO #1]", text);
        Assert.DoesNotContain("ID/index", text);
        Assert.DoesNotContain("ID natywne", text);
        Assert.Contains("00:00:00.000: Pierwszy", text);
    }

    [Fact]
    public void MissingMetadataAndEmptySections_UseHonestFallbacks()
    {
        var snapshot = new VideoFileProperties("empty", video: [new(new(0))]);
        var text = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Contains("Kontener: —", text);
        Assert.Contains("Czas trwania: —", text);
        Assert.Contains("FPS: —", text);
        Assert.Contains("Profil: —", text);
        Assert.Contains("HDR / SDR: —", text);
        foreach (var heading in new[] { "AUDIO", "NAPISY — 0 ŚCIEŻEK", "ROZDZIAŁY — 0" })
            Assert.Contains($"[{heading}]{Environment.NewLine}Brak", text);
        Assert.DoesNotContain("CFR", text);
        Assert.DoesNotContain("VFR", text);
        Assert.DoesNotContain("5.1", text);
        Assert.DoesNotContain("Przeplot", text);
    }

    [Fact]
    public void Report_WithFortySubtitlesAndNineChapters_IsCompleteAndMatchesUiSections()
    {
        var snapshot = LargeSnapshot();
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        Assert.Equal(5, sections.Count);
        Assert.StartsWith("OGÓLNE", sections[0].Heading);
        Assert.StartsWith("WIDEO #1", sections[1].Heading);
        Assert.StartsWith("AUDIO #1", sections[2].Heading);
        Assert.Equal(40, sections[3].Rows.Count);
        Assert.Equal(9, sections[4].Rows.Count);
        var text = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Equal(text, VideoFilePropertiesFormatter.FormatText(sections));
        Assert.Contains(snapshot.FileName, text);
        foreach (var subtitle in snapshot.Subtitles) Assert.Contains(subtitle.Stream.Title!, text);
        Assert.Contains("#40 — pol | SRT", text);
        Assert.Contains("| Wymuszona | SDH", text);
        Assert.DoesNotContain("język:", text);
        Assert.DoesNotContain("tytuł:", text);
        Assert.DoesNotContain("kodek:", text);
        Assert.DoesNotContain("ID/index", text);
        Assert.Contains("01:02:03.004: Rozdział #9", text);
        Assert.DoesNotContain("…", text);
    }

    [Fact]
    public void MultipleVideoAndAudioStreams_AreNumberedIndependentlyOfGlobalIndex()
    {
        var snapshot = new VideoFileProperties("multi",
            video: [new(new(7, 42)), new(new(9, 44))], audio: [new(new(4)), new(new(10))]);
        var text = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Contains("[WIDEO #1]", text);
        Assert.Contains("[WIDEO #2]", text);
        Assert.Contains("[AUDIO #1]", text);
        Assert.Contains("[AUDIO #2]", text);
        Assert.DoesNotContain("ID/index", text);
    }

    [Fact]
    public void InvalidAndZeroQuantities_AreNotPresentedAsKnownValues()
    {
        var snapshot = new VideoFileProperties("invalid", bitrate: 0, durationMilliseconds: -1,
            video: [new(new(0), Width: 0, Height: -1, FramesPerSecond: double.NaN, Bitrate: -1, BitDepth: 0)],
            audio: [new(new(1), SampleRate: 0)]);
        var text = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Contains("Średni bitrate pliku: —", text);
        Assert.Contains("Rozdzielczość: —", text);
        Assert.Contains("Próbkowanie: —", text);
        Assert.DoesNotContain("NaN", text);
    }

    [Fact]
    public void VideoFlags_AreRetainedInSnapshotButAbsentFromEveryVideoSection()
    {
        var snapshot = new VideoFileProperties("film", video:
            [new(new(0, IsDefault: true, IsForced: true)), new(new(1, IsDefault: false, IsForced: false))]);
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        foreach (var section in sections.Where(section => section.Heading.StartsWith("WIDEO #")))
            Assert.DoesNotContain(section.Rows, row => row.Label is "Domyślna" or "Wymuszona");
        Assert.True(snapshot.Video[0].Stream.IsDefault);
        Assert.True(snapshot.Video[0].Stream.IsForced);
        Assert.False(snapshot.Video[1].Stream.IsDefault);
        Assert.False(snapshot.Video[1].Stream.IsForced);
        var report = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.DoesNotContain("Domyślna", report);
        Assert.DoesNotContain("Wymuszona", report);
    }

    [Theory]
    [InlineData(true, true, "Tak")]
    [InlineData(false, false, "Nie")]
    [InlineData(null, null, "—")]
    [InlineData(true, false, "Tak")]
    [InlineData(false, true, "Nie")]
    public void AudioDefault_IsUnchangedAndForcedAppearsOnlyWhenTrue(bool? isDefault, bool? isForced, string defaultText)
    {
        var snapshot = new VideoFileProperties("film", audio: [new(new(1, IsDefault: isDefault, IsForced: isForced))]);
        var audio = Assert.Single(VideoFilePropertiesFormatter.GetSections(snapshot), section => section.Heading == "AUDIO #1");
        Assert.Equal(defaultText, Assert.Single(audio.Rows, row => row.Label == "Domyślna").Value);
        var report = VideoFilePropertiesFormatter.FormatText(snapshot);
        if (isForced == true)
        {
            Assert.Equal("Tak", Assert.Single(audio.Rows, row => row.Label == "Wymuszona").Value);
            Assert.Contains("Wymuszona: Tak", report);
        }
        else
        {
            Assert.DoesNotContain(audio.Rows, row => row.Label == "Wymuszona");
            Assert.DoesNotContain("Wymuszona", report);
        }
    }

    [Theory]
    [InlineData("Polish", null, false, false, null, "#1 — Polish | SRT")]
    [InlineData("Polish", "   ", null, null, false, "#1 — Polish | SRT")]
    [InlineData("English", "English #1", false, false, null, "#1 — English | SRT | English #1")]
    [InlineData("Portuguese", "Portuguese (Brazil)", false, false, null, "#1 — Portuguese | SRT | Portuguese (Brazil)")]
    [InlineData("English", "English #2 (SDH)", false, false, true, "#1 — English | SRT | English #2 (SDH)")]
    [InlineData("English", "English [sdh]", false, false, true, "#1 — English | SRT | English [sdh]")]
    [InlineData("English", null, true, true, true, "#1 — English | SRT | Domyślna | Wymuszona | SDH")]
    [InlineData("English", "SDHish", false, false, true, "#1 — English | SRT | SDHish | SDH")]
    [InlineData(null, null, false, false, false, "#1 — — | SRT")]
    public void SubtitleRow_IsCompactWithoutEmptyTitlesOrNegativeFlagsOrDuplicateSdh(string? language, string? title,
        bool? isDefault, bool? isForced, bool? sdh, string expected)
    {
        var snapshot = new VideoFileProperties("film", subtitles:
            [new(new(42, Codec: "subrip", Language: language, Title: title, IsDefault: isDefault, IsForced: isForced), sdh)]);
        var section = Assert.Single(VideoFilePropertiesFormatter.GetSections(snapshot), section => section.Heading.StartsWith("NAPISY"));
        Assert.Equal(("", expected), Assert.Single(section.Rows));
        Assert.Contains(expected + Environment.NewLine, VideoFilePropertiesFormatter.FormatText(snapshot));
        Assert.DoesNotContain("| |", expected);
        Assert.False(expected.EndsWith("|", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("language", "khk (Cyrl)", "Mongolian (Khalkha, Cyrillic)")]
    [InlineData("language-ietf", "khk-Cyrl", "Mongolian (Khalkha, Cyrillic)")]
    [InlineData("LANGUAGE_IETF", " KHK-CYRL ", "Mongolian (Khalkha, Cyrillic)")]
    [InlineData("language", "khk", "Mongolian (Khalkha)")]
    [InlineData("lang", "unknown-code", "unknown-code")]
    public void PropertiesLanguage_RecognizesAvailableKhalkhaCodeAndPreservesUnknownIdentifiers(string key, string code, string expected)
    {
        var metadata = new Dictionary<string, string> { ["language"] = "und", [key] = code };
        var language = FlyleafPlaybackEngine.ResolvePropertiesLanguage(metadata, null, null);
        Assert.Equal(expected, language);
        var snapshot = new VideoFileProperties("film", subtitles: [new(new(0, Language: language, Codec: "subrip"))]);
        Assert.Contains($"#1 — {expected} | SRT", VideoFilePropertiesFormatter.FormatText(snapshot));
    }

    [Fact]
    public void PropertiesLanguage_NoSourceDoesNotInventKhalkhaOrCyrillicAndKnownLanguageRemainsUnchanged()
    {
        Assert.Null(FlyleafPlaybackEngine.ResolvePropertiesLanguage(new() { ["language"] = "und" }, null, null));
        Assert.Equal("unknown-code", FlyleafPlaybackEngine.ResolvePropertiesLanguage(new(), "unknown-code", null));
        Assert.Equal("English", FlyleafPlaybackEngine.ResolvePropertiesLanguage(new() { ["language"] = "eng" }, null, "English"));
        Assert.Equal("English", FlyleafPlaybackEngine.ResolvePropertiesLanguage(new() { ["language-ietf"] = "en", ["language"] = "und" }, null, null));
        Assert.Equal("khk-Latn", LoadedMediaMetadata.PropertiesLanguageName("khk-Latn"));
        Assert.Equal("Mongolian (Khalkha, Cyrillic)", LoadedMediaMetadata.PropertiesLanguageName("khk_Cyrl"));
        Assert.Equal("unknown-code", LoadedMediaMetadata.PropertiesLanguageName("unknown-code"));
        Assert.Equal("unknown-code", LoadedMediaMetadata.PropertiesLanguageName("unknown-code", "Unknown"));
        var snapshot = new VideoFileProperties("film", subtitles: [new(new(0, Language: "khk (Cyrl)", Codec: "subrip"))]);
        Assert.Contains("#1 — Mongolian (Khalkha, Cyrillic) | SRT", VideoFilePropertiesFormatter.FormatText(snapshot));
    }

    internal static VideoFileProperties LargeSnapshot() => new(
        "Zażółć gęślą jaźń 🎬 " + new string('x', 400) + ".mkv", 1073741824, "Matroska (MKV)",
        7200000, 8000000,
        video: [new(new(0, 100, "h264", "High"), 1920, 1080, 23.976, PixelFormat: "yuv420p", BitDepth: 8,
            DisplayAspectRatioNumerator: 1920, DisplayAspectRatioDenominator: 1080,
            FrameRateNumerator: 24000, FrameRateDenominator: 1001)],
        audio: [new(new(1, 101, "eac3", Language: "pol", Title: "Polski & komentarz"), Channels: 6, SampleRate: 48000)],
        subtitles: Enumerable.Range(0, 40).Select(i => new SubtitleStreamProperties(
            new(i + 2, i + 102, "subrip", Language: "pol", Title: $"Pełny tytuł ścieżki #{i + 1} — 日本語 🎬 {new string('ż', 100)}",
                IsDefault: i == 0, IsForced: i == 39), i == 39 ? true : null)),
        chapters: Enumerable.Range(0, 9).Select(i => new PlaybackChapter(i, i == 8 ? null : $"Rozdział {i + 1}", i == 8 ? 3723004 : i * 10000)));

    [Theory]
    [InlineData(1280, 640, 1, 1, 0, 1, "2.00:1")]
    [InlineData(1920, 1080, 1, 1, 4, 3, "16:9")]
    [InlineData(720, 576, 16, 15, 0, 1, "4:3")]
    [InlineData(720, 576, 64, 45, 0, 1, "16:9")]
    [InlineData(720, 576, 0, 1, 16, 15, "4:3")]
    [InlineData(1920, 1081, 1, 1, 0, 1, "1.78:1")]
    [InlineData(0, 1080, 1, 1, 1, 1, "—")]
    [InlineData(1920, -1, 1, 1, 1, 1, "—")]
    [InlineData(1920, 1080, 0, 1, 0, 1, "—")]
    [InlineData(1920, 1080, -1, 1, 1, 0, "—")]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, 0, 1, "1.00:1")]
    public void SourceAspectRatio_UsesValidSarWithoutAssumingSquarePixels(int width, int height,
        int streamNum, int streamDen, int codecNum, int codecDen, string expected)
    {
        var dar = FlyleafPlaybackEngine.GetSourceDisplayAspectRatio(width, height,
            new AVRational { Num = streamNum, Den = streamDen }, new AVRational { Num = codecNum, Den = codecDen }, []);
        var video = new VideoStreamProperties(new(0), Width: width, Height: height,
            DisplayAspectRatioNumerator: dar.Numerator, DisplayAspectRatioDenominator: dar.Denominator);
        var rows = VideoRows(video);
        Assert.Equal(expected, Assert.Single(rows, row => row.Label == "Proporcje obrazu").Value);
        var resolutionIndex = rows.ToList().FindIndex(row => row.Label == "Rozdzielczość");
        Assert.Equal("Proporcje obrazu", rows[resolutionIndex + 1].Label);
        Assert.DoesNotContain(rows, row => row.Label is "CFR" or "VFR");
    }

    [Theory]
    [InlineData(1920, 1088, 0u, 8u, 0u, 0u, "16:9")]
    [InlineData(1300, 660, 10u, 10u, 10u, 10u, "2.00:1")]
    [InlineData(1920, 1080, 0u, 1080u, 0u, 0u, "—")]
    [InlineData(1920, 1080, 0u, 0u, uint.MaxValue, uint.MaxValue, "—")]
    public void SourceAspectRatio_RespectsLoadedCroppingAndRejectsInvalidVisibleDimensions(int width, int height,
        uint top, uint bottom, uint left, uint right, string expected)
    {
        var crop = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(crop, top);
        BinaryPrimitives.WriteUInt32LittleEndian(crop.AsSpan(4), bottom);
        BinaryPrimitives.WriteUInt32LittleEndian(crop.AsSpan(8), left);
        BinaryPrimitives.WriteUInt32LittleEndian(crop.AsSpan(12), right);
        var sar = new AVRational { Num = 1, Den = 1 };
        var dar = FlyleafPlaybackEngine.GetSourceDisplayAspectRatio(width, height, sar, default, crop);
        Assert.Equal(expected, Assert.Single(VideoRows(new(new(0), DisplayAspectRatioNumerator: dar.Numerator,
            DisplayAspectRatioDenominator: dar.Denominator)), row => row.Label == "Proporcje obrazu").Value);
        Assert.Equal(((long?)null, (long?)null), FlyleafPlaybackEngine.GetSourceDisplayAspectRatio(width, height, sar, default, crop.AsSpan(0, 15)));
    }

    [Theory]
    [InlineData(24, 1)]
    [InlineData(25, 1)]
    [InlineData(24000, 1001)]
    [InlineData(30000, 1001)]
    [InlineData(60000, 1001)]
    [InlineData(48000, 2002)]
    public void FrameRate_PreservesOriginalRationalWithoutReconstruction(int numerator, int denominator)
    {
        var expected = ((double)numerator / denominator).ToString("0.###", CultureInfo.CurrentCulture);
        if (numerator % denominator != 0) expected += $" ({numerator}/{denominator})";
        var video = new VideoStreamProperties(new(0), FramesPerSecond: 99,
            FrameRateNumerator: numerator, FrameRateDenominator: denominator);
        Assert.Equal(expected, Assert.Single(VideoRows(video), row => row.Label == "FPS").Value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 0)]
    [InlineData(-24, 1)]
    [InlineData(24, -1)]
    [InlineData(null, 1)]
    [InlineData(24, null)]
    public void FrameRate_InvalidOrIncompleteRationalIsUnknown(int? numerator, int? denominator)
        => Assert.Equal("—", Assert.Single(VideoRows(new(new(0), FramesPerSecond: 24,
            FrameRateNumerator: numerator, FrameRateDenominator: denominator)), row => row.Label == "FPS").Value);

    [Fact]
    public void FrameRate_LegacyDoubleDoesNotInventFraction()
        => Assert.Equal(23.976.ToString("0.###", CultureInfo.CurrentCulture),
            Assert.Single(VideoRows(new(new(0), FramesPerSecond: 23.976)), row => row.Label == "FPS").Value);

    [Theory]
    [InlineData(1, "1 kanał")]
    [InlineData(2, "2 kanały")]
    [InlineData(3, "3 kanały")]
    [InlineData(4, "4 kanały")]
    [InlineData(5, "5 kanałów")]
    [InlineData(12, "12 kanałów")]
    [InlineData(13, "13 kanałów")]
    [InlineData(14, "14 kanałów")]
    [InlineData(21, "21 kanałów")]
    [InlineData(22, "22 kanały")]
    [InlineData(24, "24 kanały")]
    [InlineData(112, "112 kanałów")]
    [InlineData(122, "122 kanały")]
    [InlineData(0, "—")]
    [InlineData(null, "—")]
    public void Channels_UsesPolishCountInflectionWithoutGuessingLayout(int? count, string expected)
        => Assert.Equal(expected, VideoFilePropertiesFormatter.Channels(new(new(1), Channels: count)));

    [Theory]
    [InlineData(1, "mono", "1 kanał (1.0)")]
    [InlineData(2, "stereo", "2 kanały (2.0)")]
    [InlineData(3, "2.1", "3 kanały (2.1)")]
    [InlineData(8, "7.1", "8 kanałów (7.1)")]
    public void Channels_ConfirmedSourceLayoutUsesParentheses(int count, string layout, string expected)
        => Assert.Equal(expected, VideoFilePropertiesFormatter.Channels(new(new(1), Channels: count,
            ChannelLayout: layout, IsChannelLayoutConfirmed: true)));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" \t ", false)]
    [InlineData("—", false)]
    [InlineData(" — ", false)]
    [InlineData("Polish E-AC3 224 kbps", true)]
    [InlineData(" Real title ", true)]
    public void AudioTitle_OmitsPlaceholderButPreservesRealTitle(string? title, bool visible)
    {
        var snapshot = new VideoFileProperties("film", audio: [new(new(1, Title: title))]);
        var rows = VideoFilePropertiesFormatter.GetSections(snapshot).Single(section => section.Heading == "AUDIO #1").Rows;
        if (visible)
        {
            Assert.Equal(title, Assert.Single(rows, row => row.Label == "Tytuł").Value);
            Assert.Contains("Tytuł: " + title, VideoFilePropertiesFormatter.FormatText(snapshot));
        }
        else Assert.DoesNotContain(rows, row => row.Label == "Tytuł");
    }

    [Fact]
    public void NewFields_UseSameCompleteUiAndCopyReport()
    {
        var snapshot = LargeSnapshot();
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        var report = VideoFilePropertiesFormatter.FormatText(snapshot);
        Assert.Equal(VideoFilePropertiesFormatter.FormatText(sections), report);
        Assert.Contains("Proporcje obrazu: 16:9", report);
        Assert.Contains("FPS: " + ((double)24000 / 1001).ToString("0.###", CultureInfo.CurrentCulture) + " (24000/1001)", report);
        Assert.Contains("Format: E-AC-3", report);
        Assert.Contains("Kanały: 6 kanałów", report);
        Assert.Equal(40, sections.Single(section => section.Heading.StartsWith("NAPISY")).Rows.Count);
        Assert.Equal(9, sections.Single(section => section.Heading.StartsWith("ROZDZIAŁY")).Rows.Count);
        Assert.DoesNotContain("odtwarzanie:", report);
        Assert.DoesNotContain("niepotwierdzony", report);
    }

    private static IReadOnlyList<(string Label, string Value)> VideoRows(VideoStreamProperties video)
        => VideoFilePropertiesFormatter.GetSections(new("film", video: [video]))
            .Single(section => section.Heading == "WIDEO #1").Rows;
}
