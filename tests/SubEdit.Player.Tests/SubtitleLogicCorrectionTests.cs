using System.Globalization;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class SubtitleLogicCorrectionTests
{
    // --------------------------------------------------------------------------------------------
    // BŁĄD 1: SubtitleDisplayTimeProcessor - ChangeOnlyUndefinedEndTimes & End == 0 vs missing End
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void ChangeDisplayTimes_WhenChangeOnlyUndefinedEndTimesIsTrue_UpdatesMissingEnd_PreservesExplicitZeroEnd()
    {
        // Given: MicroDVD subtitles with missing End, explicit zero End, and negative start with explicit zero End
        const string inputMicroDvd =
            "{100}{}Missing End\r\n" +
            "{200}{0}Explicit Zero\r\n" +
            "{-100}{0}Negative With Zero";

        var settings = new SubtitleDisplayTimeSettings
        {
            ChangeOnlyUndefinedEndTimes = true,
            DurationDependsOnTextLength = false,
            BaseDurationSeconds = 3.0d,
            BlockEndTime = false
        };

        // When
        var result = SubtitleDisplayTimeProcessor.Change(
            inputMicroDvd,
            settings,
            clearEndTimes: false,
            framesPerSecond: 25.0d,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 2);

        // Then:
        // {100}{} has start = 4s (100 frames), duration 3s -> end = 7s (175 frames) -> {100}{175}Missing End
        // {200}{0} had explicit end = 0 -> must NOT be changed!
        // {-100}{0} had explicit end = 0 -> must NOT be changed!
        var lines = result.Split("\r\n");
        Assert.Equal("{100}{175}Missing End", lines[0]);
        Assert.Equal("{200}{0}Explicit Zero", lines[1]);
        Assert.Equal("{-100}{0}Negative With Zero", lines[2]);
    }

    [Fact]
    public void ChangeDisplayTimes_Mpl2_WhenChangeOnlyUndefinedEndTimesIsTrue_UpdatesMissingEnd_PreservesExplicitZeroEnd()
    {
        // Given: MPL2 subtitles with missing End and explicit zero End
        const string inputMpl2 =
            "[10][]Missing End\r\n" +
            "[20][0]Explicit Zero\r\n" +
            "[-10][0]Negative With Zero";

        var settings = new SubtitleDisplayTimeSettings
        {
            ChangeOnlyUndefinedEndTimes = true,
            DurationDependsOnTextLength = false,
            BaseDurationSeconds = 2.0d,
            BlockEndTime = false
        };

        // When
        var result = SubtitleDisplayTimeProcessor.Change(
            inputMpl2,
            settings,
            clearEndTimes: false,
            framesPerSecond: 25.0d,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 2);

        // Then:
        // [10][] has start = 1.0s (10 tenths), +2.0s = 3.0s (30 tenths) -> [10][30]Missing End
        // [20][0] has explicit end -> preserved
        // [-10][0] has explicit end -> preserved
        var lines = result.Split("\r\n");
        Assert.Equal("[10][30]Missing End", lines[0]);
        Assert.Equal("[20][0]Explicit Zero", lines[1]);
        Assert.Equal("[-10][0]Negative With Zero", lines[2]);
    }

    // --------------------------------------------------------------------------------------------
    // BŁĄD 2: SubtitleFormatConverter - Missing End semantics preserved MicroDVD <-> MPL2
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void Convert_MicroDvdToMpl2_PreservesMissingEndAndExplicitZero()
    {
        const string input =
            "{100}{}Missing End\r\n" +
            "{100}{0}Explicit Zero\r\n" +
            "{-50}{}Negative Missing\r\n" +
            "{-50}{0}Negative Explicit Zero";

        // 25 fps:
        // 100 frames = 4.0s = 40 tenths
        // -50 frames = -2.0s = -20 tenths
        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.MicroDvd,
            SubtitleTimingFormat.Mpl2,
            framesPerSecond: 25.0d);

        var lines = converted.Split("\r\n");
        Assert.Equal("[40][]Missing End", lines[0]);
        Assert.Equal("[40][0]Explicit Zero", lines[1]);
        Assert.Equal("[-20][]Negative Missing", lines[2]);
        Assert.Equal("[-20][0]Negative Explicit Zero", lines[3]);
    }

    [Fact]
    public void Convert_Mpl2ToMicroDvd_PreservesMissingEndAndExplicitZero()
    {
        const string input =
            "[40][]Missing End\r\n" +
            "[40][0]Explicit Zero\r\n" +
            "[-20][]Negative Missing\r\n" +
            "[-20][0]Negative Explicit Zero";

        // 25 fps:
        // 40 tenths = 4.0s = 100 frames
        // -20 tenths = -2.0s = -50 frames
        var converted = SubtitleFormatConverter.Convert(
            input,
            SubtitleTimingFormat.Mpl2,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0d);

        var lines = converted.Split("\r\n");
        Assert.Equal("{100}{}Missing End", lines[0]);
        Assert.Equal("{100}{0}Explicit Zero", lines[1]);
        Assert.Equal("{-50}{}Negative Missing", lines[2]);
        Assert.Equal("{-50}{0}Negative Explicit Zero", lines[3]);
    }

    [Fact]
    public void Convert_RoundTrip_MicroDvdAndMpl2_PreservesMissingEnd()
    {
        const string original = "{100}{}Missing End\r\n{250}{300}With End";

        var mpl2 = SubtitleFormatConverter.Convert(
            original,
            SubtitleTimingFormat.MicroDvd,
            SubtitleTimingFormat.Mpl2,
            framesPerSecond: 25.0d);

        Assert.Equal("[40][]Missing End\r\n[100][120]With End", mpl2);

        var backToMicroDvd = SubtitleFormatConverter.Convert(
            mpl2,
            SubtitleTimingFormat.Mpl2,
            SubtitleTimingFormat.MicroDvd,
            framesPerSecond: 25.0d);

        Assert.Equal(original, backToMicroDvd);
    }

    // --------------------------------------------------------------------------------------------
    // BŁĄD 3: SubtitleSortProcessor - Renumbers SRT blocks sequentially after sorting
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void Sort_SrtOutOfOrder_SortsChronologicallyAndRenumbersSequentially()
    {
        const string unsortedSrt =
            "1\r\n" +
            "00:00:10,000 --> 00:00:12,000\r\nTreci tekst\r\n\r\n" +
            "2\r\n" +
            "00:00:02,000 --> 00:00:04,000\r\nPrvi tekst\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nDrugi tekst\r\n";

        var lines = unsortedSrt.Replace("\r\n", "\n").Split('\n');
        var result = SubtitleSortProcessor.Sort(unsortedSrt, 25.0d, 0, lines.Length - 1);

        const string expected =
            "1\r\n" +
            "00:00:02,000 --> 00:00:04,000\r\nPrvi tekst\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nDrugi tekst\r\n\r\n" +
            "3\r\n" +
            "00:00:10,000 --> 00:00:12,000\r\nTreci tekst\r\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sort_SrtWithSignedTimestamps_SortsChronologicallyAndRenumbers()
    {
        const string unsortedSignedSrt =
            "1\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nPositive start\r\n\r\n" +
            "2\r\n" +
            "-00:00:04,000 --> -00:00:02,000\r\nNegative later\r\n\r\n" +
            "3\r\n" +
            "-00:00:10,000 --> -00:00:08,000\r\nNegative earlier\r\n";

        var lines = unsortedSignedSrt.Replace("\r\n", "\n").Split('\n');
        var result = SubtitleSortProcessor.Sort(unsortedSignedSrt, 25.0d, 0, lines.Length - 1);

        const string expected =
            "1\r\n" +
            "-00:00:10,000 --> -00:00:08,000\r\nNegative earlier\r\n\r\n" +
            "2\r\n" +
            "-00:00:04,000 --> -00:00:02,000\r\nNegative later\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nPositive start\r\n";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sort_SrtSelectionOnly_RenumbersOnlySortedRangeWithCorrectBaseIndex()
    {
        const string srt =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\nBlock 1\r\n\r\n" +
            "2\r\n" +
            "00:00:10,000 --> 00:00:12,000\r\nBlock 2 (out of order)\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nBlock 3 (out of order)\r\n\r\n" +
            "4\r\n" +
            "00:00:20,000 --> 00:00:22,000\r\nBlock 4\r\n";

        // Sort only lines corresponding to Block 2 and Block 3 (line 4 to 10)
        var result = SubtitleSortProcessor.Sort(srt, 25.0d, firstLine: 4, lastLine: 10);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\nBlock 1\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:07,000\r\nBlock 3 (out of order)\r\n\r\n" +
            "3\r\n" +
            "00:00:10,000 --> 00:00:12,000\r\nBlock 2 (out of order)\r\n\r\n" +
            "4\r\n" +
            "00:00:20,000 --> 00:00:22,000\r\nBlock 4\r\n";

        Assert.Equal(expected, result);
    }

    // --------------------------------------------------------------------------------------------
    // BŁĄD 4: SubtitleTextEditor - missing End is not marked as very short
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void DetermineLineColor_WhenMissingEnd_ReturnsZeroColorNotVeryShort()
    {
        // MicroDVD missing End
        var microDvdColor = SubtitleTextEditor.DetermineLineColor(
            line: "{100}{}Tekst z brakujacym czasem konca",
            isSrtDocument: false,
            framesPerSecond: 25.0d,
            characterCount: 33,
            nextStart: null,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        // MPL2 missing End
        var mpl2Color = SubtitleTextEditor.DetermineLineColor(
            line: "[40][]Tekst z brakujacym czasem konca",
            isSrtDocument: false,
            framesPerSecond: 25.0d,
            characterCount: 33,
            nextStart: null,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        // Missing End lines must NOT be highlighted as very short
        Assert.Equal(0, microDvdColor);
        Assert.Equal(0, mpl2Color);
        Assert.NotEqual(SubtitleTextEditor.VeryShortColorIndex, microDvdColor);
        Assert.NotEqual(SubtitleTextEditor.VeryShortColorIndex, mpl2Color);
    }

    [Fact]
    public void DetermineLineColor_WhenExplicitZeroEnd_ReturnsVeryShortColorIndex()
    {
        // MicroDVD explicit zero with start > end (duration <= 0)
        var microDvdZeroColor = SubtitleTextEditor.DetermineLineColor(
            line: "{100}{0}Tekst z jawnym zerem",
            isSrtDocument: false,
            framesPerSecond: 25.0d,
            characterCount: 22,
            nextStart: null,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        // MPL2 explicit zero with start > end (duration <= 0)
        var mpl2ZeroColor = SubtitleTextEditor.DetermineLineColor(
            line: "[40][0]Tekst z jawnym zerem",
            isSrtDocument: false,
            framesPerSecond: 25.0d,
            characterCount: 22,
            nextStart: null,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        // Explicit End == 0 gives duration <= 0, which should be flagged as VeryShortColorIndex
        Assert.Equal(SubtitleTextEditor.VeryShortColorIndex, microDvdZeroColor);
        Assert.Equal(SubtitleTextEditor.VeryShortColorIndex, mpl2ZeroColor);
    }

    [Fact]
    public void SubtitleDocument_TryGetCueTiming_AccuratelyDifferentiatesMissingEndFromExplicitZero()
    {
        // MicroDVD missing vs explicit zero
        var microDvdMissingSuccess = SubtitleDocument.TryGetCueTiming("{100}{}Text", 25.0d, out var start1, out var end1, out var hasEnd1);
        var microDvdZeroSuccess = SubtitleDocument.TryGetCueTiming("{100}{0}Text", 25.0d, out var start2, out var end2, out var hasEnd2);

        Assert.True(microDvdMissingSuccess);
        Assert.Equal(4000, start1);
        Assert.Equal(0, end1);
        Assert.False(hasEnd1);

        Assert.True(microDvdZeroSuccess);
        Assert.Equal(4000, start2);
        Assert.Equal(0, end2);
        Assert.True(hasEnd2);

        // MPL2 missing vs explicit zero
        var mpl2MissingSuccess = SubtitleDocument.TryGetCueTiming("[10][]Text", 25.0d, out var start3, out var end3, out var hasEnd3);
        var mpl2ZeroSuccess = SubtitleDocument.TryGetCueTiming("[10][0]Text", 25.0d, out var start4, out var end4, out var hasEnd4);

        Assert.True(mpl2MissingSuccess);
        Assert.Equal(1000, start3);
        Assert.Equal(0, end3);
        Assert.False(hasEnd3);

        Assert.True(mpl2ZeroSuccess);
        Assert.Equal(1000, start4);
        Assert.Equal(0, end4);
        Assert.True(hasEnd4);

        // SRT timing line always has end
        var srtSuccess = SubtitleDocument.TryGetCueTiming("00:00:01,000 --> 00:00:03,000", 25.0d, out var startSrt, out var endSrt, out var hasEndSrt);
        Assert.True(srtSuccess);
        Assert.Equal(1000, startSrt);
        Assert.Equal(3000, endSrt);
        Assert.True(hasEndSrt);
    }

    // --------------------------------------------------------------------------------------------
    // Regresja optymalizacji ComputeLineColors (O(N) nextStart resolution & all format semantics)
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void ComputeLineColors_MicroDvd_ExplicitEnd_CalculatesColorsProperly()
    {
        var lines = new[]
        {
            "{100}{200}Pierwszy napis",
            "{205}{300}Drugi napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(2, colors.Count);
        // Both cues have reasonable duration (100 frames = 4000ms for 14 chars -> intensity low)
        Assert.True(colors[0] >= 0);
        Assert.True(colors[1] >= 0);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[1]);
    }

    [Fact]
    public void ComputeLineColors_MicroDvd_MissingEnd_YieldsZeroColor()
    {
        var lines = new[]
        {
            "{100}{}Napis z brakujacym koncem",
            "{205}{300}Drugi napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(0, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.VeryShortColorIndex, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[0]);
    }

    [Fact]
    public void ComputeLineColors_MicroDvd_ExplicitZeroEnd_YieldsVeryShortColor()
    {
        var lines = new[]
        {
            "{100}{0}Napis z zerem jawnym",
            "{205}{300}Drugi napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(SubtitleTextEditor.VeryShortColorIndex, colors[0]);
    }

    [Fact]
    public void ComputeLineColors_Mpl2_MissingAndExplicitZeroEnds_HandledCorrectly()
    {
        var lines = new[]
        {
            "[10][]Napis bez konca",
            "[20][0]Napis z zerem",
            "[30][50]Poprawny napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(0, colors[0]);
        Assert.Equal(SubtitleTextEditor.VeryShortColorIndex, colors[1]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[2]);
    }

    [Fact]
    public void ComputeLineColors_TmPlayer_NextStartDeterminesEnd_LastCueYieldsZero()
    {
        var lines = new[]
        {
            "00:00:10:Pierwszy napis",
            "00:00:12:Drugi napis",
            "00:00:20:Ostatni napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        // Lines 0 and 1 have next cue start as their end
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[1]);

        // Last cue in TMPlayer has no nextStart, so DetermineLineColor returns 0
        Assert.Equal(0, colors[2]);
    }

    [Fact]
    public void ComputeLineColors_Srt_CalculatesColorsForTimingLinesAndZeroForTextLines()
    {
        var lines = new[]
        {
            "1",
            "00:00:01,000 --> 00:00:04,000",
            "Pierwszy wiersz tekstu",
            "Drugi wiersz tekstu",
            "",
            "2",
            "00:00:05,000 --> 00:00:08,000",
            "Trzeci tekst"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(8, colors.Count);
        // Sequence number lines and text lines in SRT return 0
        Assert.Equal(0, colors[0]);
        Assert.Equal(0, colors[2]);
        Assert.Equal(0, colors[3]);
        Assert.Equal(0, colors[4]);
        Assert.Equal(0, colors[5]);
        Assert.Equal(0, colors[7]);

        // Timing lines calculate colors based on multi-line text lengths
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[1]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[6]);
    }

    [Fact]
    public void ComputeLineColors_SignedTimestamps_HandledCorrectly()
    {
        var lines = new[]
        {
            "{-100}{100}Ujemny start",
            "{101}{200}Normalny napis"
        };

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(2, colors.Count);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[1]);
    }

    [Fact]
    public void ComputeLineColors_DocumentWithoutTimings_MarksAsInvalidFormatWhenRequested()
    {
        var lines = new[]
        {
            "Zwykly wiersz 1",
            "Zwykly wiersz 2",
            "Zwykly wiersz 3"
        };

        var colorsHighlighted = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        var colorsNotHighlighted = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: false,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.All(colorsHighlighted, c => Assert.Equal(SubtitleTextEditor.InvalidFormatColorIndex, c));
        Assert.All(colorsNotHighlighted, c => Assert.Equal(0, c));
    }

    [Fact]
    public void ComputeLineColors_LongBlockOfUntimedTextAndInterleavedLines_PreservesNextStartResolution()
    {
        var lines = new List<string>
        {
            "{100}{200}Poczatkowy napis"
        };

        for (var i = 0; i < 50; i++)
        {
            lines.Add($"Linia nietymowanego tekstu {i}");
        }

        lines.Add("{1000}{1200}Koncowy napis");

        var colors = SubtitleTextEditor.ComputeLineColors(
            lines,
            framesPerSecond: 25.0d,
            highlightInvalidFormat: true,
            highlightVeryShortLines: true,
            highlightShortLines: true);

        Assert.Equal(52, colors.Count);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[0]);
        Assert.NotEqual(SubtitleTextEditor.InvalidFormatColorIndex, colors[51]);
        for (var i = 1; i <= 50; i++)
        {
            Assert.Equal(SubtitleTextEditor.InvalidFormatColorIndex, colors[i]);
        }
    }

    // --------------------------------------------------------------------------------------------
    // BUG-FMT-04: SRT renumbering after deleting blocks
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_RemovesEmptyMiddleBlock_RenumbersRemainingBlocks()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy\r\n\r\n" +
            "2\r\n" +
            "00:00:03,000 --> 00:00:04,000\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveEmptyLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_RemovesFirstBlock_RenumbersFromOne()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n\r\n" +
            "2\r\n" +
            "00:00:03,000 --> 00:00:04,000\r\n" +
            "Drugi\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveEmptyLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:03,000 --> 00:00:04,000\r\n" +
            "Drugi\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_RemovesMultipleBlocks_RenumbersSequentially()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy\r\n\r\n" +
            "2\r\n" +
            "00:00:03,000 --> 00:00:04,000\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci\r\n\r\n" +
            "4\r\n" +
            "00:00:07,000 --> 00:00:08,000\r\n\r\n" +
            "5\r\n" +
            "00:00:09,000 --> 00:00:10,000\r\n" +
            "Piąty";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveEmptyLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 20,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Trzeci\r\n\r\n" +
            "3\r\n" +
            "00:00:09,000 --> 00:00:10,000\r\n" +
            "Piąty";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_RemovesDuplicateBlock_RenumbersRemainingBlocks()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Tekst powtórzony\r\n\r\n" +
            "2\r\n" +
            "00:00:02,000 --> 00:00:04,000\r\n" +
            "Tekst powtórzony\r\n\r\n" +
            "3\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Kolejny napis";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveRepeatedLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:04,000\r\n" +
            "Tekst powtórzony\r\n\r\n" +
            "2\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Kolejny napis";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_NoDeletedBlocks_PreservesExistingNumbers()
    {
        const string input =
            "5\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy .\r\n\r\n" +
            "9\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Drugi .";

        var settings = new SubtitleCorrectionSettings
        {
            FixPunctuation = true,
            RemoveEmptyLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "5\r\n" +
            "00:00:01,000 --> 00:00:02,000\r\n" +
            "Pierwszy.\r\n\r\n" +
            "9\r\n" +
            "00:00:05,000 --> 00:00:06,000\r\n" +
            "Drugi.";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_PreservesTimingAndMultilineTextFormatting()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,500\r\n" +
            "Linia pierwsza\r\n" +
            "Linia druga\r\n\r\n" +
            "2\r\n" +
            "00:00:03,000 --> 00:00:04,000\r\n\r\n" +
            "3\r\n" +
            "00:00:05,123 --> 00:00:07,890\r\n" +
            "Trzeci blok linia 1\r\n" +
            "Trzeci blok linia 2";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveEmptyLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 15,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:02,500\r\n" +
            "Linia pierwsza\r\n" +
            "Linia druga\r\n\r\n" +
            "2\r\n" +
            "00:00:05,123 --> 00:00:07,890\r\n" +
            "Trzeci blok linia 1\r\n" +
            "Trzeci blok linia 2";

        Assert.Equal(expected, result);
    }

    // --------------------------------------------------------------------------------------------
    // BUG-FMT-06: Preserve original SRT END separator (. or ,) on end time modification
    // --------------------------------------------------------------------------------------------

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_LimitDisplayTime_PreservesCommaSeparator()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:05,000\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04,000 --> 00:00:06,000\r\n" +
            "Tekst 2";

        var settings = new SubtitleCorrectionSettings
        {
            LimitDisplayTime = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1, // 40 ms gap -> max end = 4000 - 40 = 3960 ms
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:03,960\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04,000 --> 00:00:06,000\r\n" +
            "Tekst 2";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_LimitDisplayTime_PreservesDotSeparator()
    {
        const string input =
            "1\r\n" +
            "00:00:01.000 --> 00:00:05.000\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06.000\r\n" +
            "Tekst 2";

        var settings = new SubtitleCorrectionSettings
        {
            LimitDisplayTime = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01.000 --> 00:00:03.960\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06.000\r\n" +
            "Tekst 2";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_LimitDisplayTime_MixedSeparators_PreservesEachSeparator()
    {
        const string input =
            "1\r\n" +
            "00:00:01,000 --> 00:00:05.000\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06,000\r\n" +
            "Tekst 2";

        var settings = new SubtitleCorrectionSettings
        {
            LimitDisplayTime = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01,000 --> 00:00:03.960\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06,000\r\n" +
            "Tekst 2";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_LimitDisplayTime_PreservesTrailingDataAfterEnd()
    {
        const string input =
            "1\r\n" +
            "00:00:01.000 --> 00:00:05.000  X1:0 Y1:0\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06.000\r\n" +
            "Tekst 2";

        var settings = new SubtitleCorrectionSettings
        {
            LimitDisplayTime = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01.000 --> 00:00:03.960  X1:0 Y1:0\r\n" +
            "Tekst 1\r\n\r\n" +
            "2\r\n" +
            "00:00:04.000 --> 00:00:06.000\r\n" +
            "Tekst 2";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SubtitleCorrectionProcessor_Correct_Srt_RemoveRepeatedLines_PreservesDotSeparatorOnMerge()
    {
        const string input =
            "1\r\n" +
            "00:00:01.000 --> 00:00:02.000\r\n" +
            "Powtórzony\r\n\r\n" +
            "2\r\n" +
            "00:00:02.000 --> 00:00:04.500\r\n" +
            "Powtórzony\r\n\r\n" +
            "3\r\n" +
            "00:00:06.000 --> 00:00:07.000\r\n" +
            "Kolejny";

        var settings = new SubtitleCorrectionSettings
        {
            RemoveRepeatedLines = true
        };

        var result = SubtitleCorrectionProcessor.Correct(
            input,
            settings,
            framesPerSecond: 25.0,
            minimumGapFrames: 1,
            firstLine: 0,
            lastLine: 10,
            treatUnrecognizedAsText: false);

        const string expected =
            "1\r\n" +
            "00:00:01.000 --> 00:00:04.500\r\n" +
            "Powtórzony\r\n\r\n" +
            "2\r\n" +
            "00:00:06.000 --> 00:00:07.000\r\n" +
            "Kolejny";

        Assert.Equal(expected, result);
    }
}
