using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleFormatDetectionAndCorrectionTests
{
    [Theory]
    [InlineData("{-600}{-575}Wers ujemny MicroDVD", true)]
    [InlineData("[-252][-237]Wers ujemny MPL2", true)]
    [InlineData("00:00:-24:Wers ujemny TMPlayer", true)]
    [InlineData("-1:00:-4:Wers ujemny TMPlayer", true)]
    [InlineData("-00:00:24,000 --> -00:00:20,000", true)]
    public void IsRecognizedSubtitleLine_RecognizesSignedSubtitleLines(string line, bool expected)
    {
        var recognized = SubtitleTimingFormatter.IsRecognizedSubtitleLine(line);
        Assert.Equal(expected, recognized);
    }

    [Fact]
    public void Detect_DetectsSignedFormatsCorrectly()
    {
        const string microDvdDoc = "{-100}{-50}Tekst 1\r\n{-40}{-10}Tekst 2";
        const string mpl2Doc = "[-100][-50]Tekst 1\r\n[-40][-10]Tekst 2";
        const string tmPlayerDoc = "-1:00:-4:Tekst 1\r\n00:00:-24:Tekst 2";

        Assert.Equal(SubtitleTimingFormat.MicroDvd, SubtitleTimingFormatter.Detect(microDvdDoc));
        Assert.Equal(SubtitleTimingFormat.Mpl2, SubtitleTimingFormatter.Detect(mpl2Doc));
        Assert.Equal(SubtitleTimingFormat.TmPlayerShort, SubtitleTimingFormatter.Detect(tmPlayerDoc));
    }

    [Fact]
    public void SubtitleFormatConverter_ConvertBetweenFormats_PreservesSignedValues()
    {
        // 25 fps: -50 sekund = -1250 klatek w MicroDVD = -500 deci w MPL2 = -50s w TMPlayer
        const string microDvd = "{-1250}{-1200}Wers 1\r\n{-1150}{-1100}Wers 2";

        var mpl2 = SubtitleFormatConverter.Convert(microDvd, SubtitleTimingFormat.MicroDvd, SubtitleTimingFormat.Mpl2, 25.0);
        Assert.Equal("[-500][-480]Wers 1\r\n[-460][-440]Wers 2", mpl2);

        var restoredMicroDvd = SubtitleFormatConverter.Convert(mpl2, SubtitleTimingFormat.Mpl2, SubtitleTimingFormat.MicroDvd, 25.0);
        Assert.Equal(microDvd, restoredMicroDvd);

        var tmPlayer = SubtitleFormatConverter.Convert(mpl2, SubtitleTimingFormat.Mpl2, SubtitleTimingFormat.TmPlayerLong, 25.0);
        Assert.Equal("00:00:-50:Wers 1\r\n00:00:-46:Wers 2", tmPlayer);

        var tmToMicroDvd = SubtitleFormatConverter.Convert(tmPlayer, SubtitleTimingFormat.TmPlayerLong, SubtitleTimingFormat.MicroDvd, 25.0);
        Assert.Equal("{-1250}{-1150}Wers 1\r\n{-1150}{-1075}Wers 2", tmToMicroDvd);
    }

    [Fact]
    public void SubtitleSplitProcessor_PreservesExactSplitBoundaryRules()
    {
        // Start < D -> FirstPart; Start >= D -> Remainder; crossing -> całe w FirstPart
        // D = 5000ms (5.0s, 125 klatek przy 25 fps)
        const string doc = "{50}{100}W całości w pierwszej części\r\n" +
                           "{120}{140}Przecina granicę (120 < 125 < 140)\r\n" +
                           "{125}{150}Na granicy podziału\r\n" +
                           "{200}{250}W całości w reszcie";

        var result = SubtitleSplitProcessor.Split(doc, 5000, 25.0);

        Assert.Equal(2, result.FirstPartCueCount);
        Assert.Equal(2, result.RemainderCueCount);

        Assert.Contains("{50}{100}W całości w pierwszej części", result.FirstPartText);
        Assert.Contains("{120}{140}Przecina granicę", result.FirstPartText);

        // W remainder: czasy są przesunięte o -D (-125 klatek)
        // {125}{150} -> {0}{25}
        // {200}{250} -> {75}{125}
        Assert.Contains("{0}{25}Na granicy podziału", result.RemainderText);
        Assert.Contains("{75}{125}W całości w reszcie", result.RemainderText);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_RemoveMicroDvdColorInformation_RemovesOnlyColorCodes_PreservesOtherBraces()
    {
        const string input =
            "{100}{150}{Y:i}{c:$0000FF}Kwestia pochyła i niebieska\r\n" +
            "{160}{200}{Y:b}{C:$00FF00}Kwestia pogrubiona i zielona\r\n" +
            "{210}{250}{F:Arial}{c:$FFFFFF}Kwestia z czcionką i kolorem\r\n" +
            "{260}{300}{śmiech} Dzień dobry {oklaski} wszystkim\r\n" +
            "{310}{350}{c:$12abCD}{C:$34CD56}Dwa kolory";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveMicroDvdColorInformation = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 2,
            firstLine: 0,
            lastLine: 4,
            treatUnrecognizedAsText: false);

        const string expected =
            "{100}{150}{Y:i}Kwestia pochyła i niebieska\r\n" +
            "{160}{200}{Y:b}Kwestia pogrubiona i zielona\r\n" +
            "{210}{250}{F:Arial}Kwestia z czcionką i kolorem\r\n" +
            "{260}{300}{śmiech} Dzień dobry {oklaski} wszystkim\r\n" +
            "{310}{350}Dwa kolory";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_RemoveMicroDvdColorInformation_Disabled_PreservesColorCodes()
    {
        const string input = "{100}{150}{Y:i}{c:$0000FF}Kwestia pochyła i niebieska";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveMicroDvdColorInformation = false
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 2,
            firstLine: 0,
            lastLine: 0,
            treatUnrecognizedAsText: false);

        Assert.Equal(input, result);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerToMicroDvd_GroupsAdjacentIdenticalTimestampsIntoSingleCue()
    {
        const string input =
            "00:00:10:Linia pierwsza\r\n" +
            "00:00:10:Linia druga\r\n" +
            "00:00:15:Następny napis";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0);

        const string expected =
            "{250}{375}Linia pierwsza|Linia druga\r\n" +
            "{375}{450}Następny napis";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerToMicroDvd_PreservesMultilineTextOrderForThreeLines()
    {
        const string input =
            "00:00:10:Linia pierwsza\r\n" +
            "00:00:10:Linia druga\r\n" +
            "00:00:10:Linia trzecia";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0);

        // Ostatni cue (brak następnego o większym czasie) -> domyślny czas trwania +3s (13s = 325 klatek)
        const string expected = "{250}{325}Linia pierwsza|Linia druga|Linia trzecia";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerToMicroDvd_TimestampChangeTerminatesGroup()
    {
        const string input =
            "00:00:10:Grupa 1 linia 1\r\n" +
            "00:00:10:Grupa 1 linia 2\r\n" +
            "00:00:12:Pojedynczy napis\r\n" +
            "00:00:15:Grupa 2 linia 1\r\n" +
            "00:00:15:Grupa 2 linia 2";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0);

        const string expected =
            "{250}{300}Grupa 1 linia 1|Grupa 1 linia 2\r\n" +
            "{300}{375}Pojedynczy napis\r\n" +
            "{375}{450}Grupa 2 linia 1|Grupa 2 linia 2";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerToMicroDvd_RepeatedTimestampLaterCreatesSeparateCue()
    {
        const string input =
            "00:00:10:Pierwszy 1\r\n" +
            "00:00:10:Pierwszy 2\r\n" +
            "00:00:12:Środek\r\n" +
            "00:00:10:Powtórzony 10s";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0);

        const string expected =
            "{250}{300}Pierwszy 1|Pierwszy 2\r\n" +
            "{300}{375}Środek\r\n" +
            "{250}{325}Powtórzony 10s";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerToMpl2_GroupsAdjacentIdenticalTimestampsIntoSingleCue()
    {
        const string input =
            "00:00:10:Linia pierwsza\r\n" +
            "00:00:10:Linia druga\r\n" +
            "00:00:15:Następny napis";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.Mpl2,
            framesPerSecond: 25.0);

        const string expected =
            "[100][150]Linia pierwsza|Linia druga\r\n" +
            "[150][180]Następny napis";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayerShortToTmPlayerLong_MaintainsSeparateLinesWithUpdatedTimestamps()
    {
        const string input =
            "0:00:10:Linia 1\r\n" +
            "0:00:10:Linia 2\r\n" +
            "0:00:15:Linia 3";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerShort,
            SubtitleTimingFormat.TmPlayerLong,
            framesPerSecond: 25.0);

        const string expected =
            "00:00:10:Linia 1\r\n" +
            "00:00:10:Linia 2\r\n" +
            "00:00:15:Linia 3";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleFormatConverter_Convert_TmPlayer_RespectsLineRange()
    {
        const string input =
            "00:00:05:Przed zakresem\r\n" +
            "00:00:10:Linia 1\r\n" +
            "00:00:10:Linia 2\r\n" +
            "00:00:20:Po zakresie";

        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.TmPlayerLong,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0,
            firstLine: 1,
            lastLine: 2);

        const string expected =
            "00:00:05:Przed zakresem\r\n" +
            "{250}{500}Linia 1|Linia 2\r\n" +
            "00:00:20:Po zakresie";

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void SubtitleDocument_ParseAndExportSrt_TmPlayerMultiline_ProducesValidMultilineSrtBlocks()
    {
        const string input =
            "00:00:10:Linia pierwsza\r\n" +
            "00:00:10:Linia druga\r\n" +
            "00:00:15:Kolejny blok linia 1\r\n" +
            "00:00:15:Kolejny blok linia 2";

        var doc = SubtitleDocument.Parse(input, framesPerSecond: 25.0);
        Assert.Equal(2, doc.Cues.Count);
        Assert.Equal(2, doc.Cues[0].Lines.Count);
        Assert.Equal(2, doc.Cues[1].Lines.Count);

        var exportedSrt = SrtSubtitleExporter.Export(doc);
        Assert.Contains("Linia pierwsza\r\nLinia druga", exportedSrt);
        Assert.Contains("Kolejny blok linia 1\r\nKolejny blok linia 2", exportedSrt);
    }
}
