using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleMergeAndExportTests
{
    [Theory]
    [InlineData("\n", "\n")]
    [InlineData("\r\n", "\r\n")]
    [InlineData("\r", "\n")]
    public void SplitAndMerge_PreserveSrtBlockAndTimingRules(string inputBreak, string outputBreak)
    {
        const string firstTiming = " \t+0:00:01,2\t--> 00:00:01.45 X1:10  ";
        const string secondTiming = "\t00:00:05.0 -->\t00:00:04,9 X2:20";
        var input = string.Join(inputBreak, [
            "", "  9 ", firstTiming, "  Pierwszy  ", "Drugi", " \t ", "",
            secondTiming, "  Bez numeru  ", "", " \t "
        ]);

        Assert.True(SubtitleSplitProcessor.CanSplit(input, 5000, 25));
        Assert.False(SubtitleSplitProcessor.CanSplit(input, 5001, 25));
        var split = SubtitleSplitProcessor.Split(input, 5000, 25);
        Assert.Equal(new SubtitleSplitResult(
            string.Join(outputBreak, "1", firstTiming, "  Pierwszy  ", "Drugi"),
            string.Join(outputBreak, "1", "\t00:00:00.000 -->\t00:00:00.000 X2:20", "  Bez numeru  "),
            1, 1), split);

        var preview = SubtitleMergeProcessor.ParseForMerge(input, SubtitleLogicalFormat.Srt);
        Assert.Equal(new SubtitleMergeParsedCue[] {
            new(0, 1200, 1450, "[00:00:01,200] Pierwszy | Drugi"),
            new(1, 5000, 4900, "[00:00:05,000] Bez numeru")
        }, preview.Cues);
        Assert.Equal(preview.Cues.Select(cue => cue.DisplayText), preview.DisplayItems);

        const string activeTiming = "00:00:01.5 --> 00:00:02,000";
        var merge = SubtitleMergeProcessor.Merge(
            $"20\n{activeTiming}\nAktywny", input, SubtitleLogicalFormat.Srt, 1, 3000, 2, 25);
        Assert.Equal(new SubtitleMergeResult(string.Join(outputBreak, [
            "1", activeTiming, "Aktywny", "",
            "2", " \t-00:00:04,600\t--> -00:00:04,100 X1:10  ", "  Pierwszy  ", "Drugi", "",
            "3", "\t00:00:03.000 -->\t00:00:02.800 X2:20", "  Bez numeru  "
        ]), 2), merge);
    }

    [Fact]
    public void SplitAndMerge_AcceptCueWithoutText()
    {
        const string active = "21\r\n00:00:00,000 --> 00:00:01,000\r\nAktywny\r\n";
        const string withoutText = "00:00:01,000 --> 00:00:02,000";
        Assert.Equal(new SubtitleSplitResult("", "1\n00:00:00,000 --> 00:00:01,000", 0, 1),
            SubtitleSplitProcessor.Split(withoutText, 1000, 25));
        Assert.Equal(new SubtitleMergeResult(active, 0),
            SubtitleMergeProcessor.Merge(active, withoutText, SubtitleLogicalFormat.Srt, 1, 0, null, 25));
        Assert.Equal(new SubtitleMergeResult("1\r\n00:00:00,000 --> 00:00:01,000\r\nAktywny\r\n\r\n2\r\n00:00:00,000 --> 00:00:01,000", 1),
            SubtitleMergeProcessor.Merge(active, withoutText, SubtitleLogicalFormat.Srt, 0, 0, null, 25));
    }

    [Theory]
    [InlineData("\n", "00:60:00,000 --> 00:00:04,000")]
    [InlineData("\r\n", "00:00:03,000 -> 00:00:04,000")]
    [InlineData("\r", "00:00:03,000 --> 00:00:60,000")]
    public void SplitAndMerge_RefuseMixedSrtAtFirstInvalidTimingLine(string lineBreak, string badTiming)
    {
        var mixed = string.Join(lineBreak, [
            "1", "00:00:01,000 --> 00:00:02,000", "Pierwszy", "",
            "2", badTiming, "Tekst do odzyskania", "",
            "3", "00:00:06,000 --> 00:00:07,000", "Ostatni", "",
            "4", "Kolejny niedokończony blok"
        ]);
        const string valid = "1\n00:00:02,000 --> 00:00:03,000\nPoprawny";

        Assert.True(SubtitleSplitProcessor.CanSplit(mixed, 5000, 25));
        Assert.Equal(new SubtitleSplitResult(mixed, "", 0, 0) { InvalidSrtLine = 6 },
            SubtitleSplitProcessor.Split(mixed, 5000, 25));

        var preview = SubtitleMergeProcessor.ParseForMerge(mixed, SubtitleLogicalFormat.Srt);
        Assert.Equal(6, preview.InvalidSrtLine);
        Assert.Empty(preview.Cues);
        Assert.Empty(preview.DisplayItems);
        Assert.Equal(new SubtitleMergeResult(mixed, 0) { InvalidSrtLine = 6 },
            SubtitleMergeProcessor.Merge(mixed, valid, SubtitleLogicalFormat.Srt, 0, 0, null, 25));
        Assert.Equal(new SubtitleMergeResult(valid, 0) { InvalidSrtLine = 6, IsImportedSrtError = true },
            SubtitleMergeProcessor.Merge(valid, mixed, SubtitleLogicalFormat.Srt, 0, 0, null, 25));
    }

    [Theory]
    [InlineData("2\nNiezapisany szkic", 6)]
    [InlineData("Niezapisany szkic", 5)]
    [InlineData("2\n", 5)]
    public void Split_RefusesUnfinishedBlockEvenWithoutAnyCueAfterSplitPoint(string draft, int line)
    {
        var input = "1\n00:00:01,000 --> 00:00:02,000\nPierwszy\n\n" + draft;
        Assert.True(SubtitleSplitProcessor.CanSplit(input, 5000, 25));
        Assert.Equal(new SubtitleSplitResult(input, "", 0, 0) { InvalidSrtLine = line },
            SubtitleSplitProcessor.Split(input, 5000, 25));
    }

    [Fact]
    public void SrtSubtitleExporter_DetectsNegativeTimestampsAndThrowsInsteadOfClamping()
    {
        // Roboczy SRT z ujemnym czasem
        var workingSrt = "1\r\n-00:00:10,000 --> 00:00:05,000\r\nWiersz z ujemnym startem\r\n\r\n2\r\n00:00:10,000 --> 00:00:15,000\r\nWiersz poprawny";
        var doc = SubtitleDocument.Parse(workingSrt, 25.0);

        Assert.True(SrtSubtitleExporter.HasNegativeTimestamps(doc));
        Assert.Throws<InvalidOperationException>(() => SrtSubtitleExporter.Export(doc));
    }

    [Fact]
    public void SrtSubtitleExporter_AllowsValidNonNegativeTimestamps()
    {
        var validSrt = "1\r\n00:00:10,000 --> 00:00:15,000\r\nWiersz poprawny";
        var doc = SubtitleDocument.Parse(validSrt, 25.0);

        Assert.False(SrtSubtitleExporter.HasNegativeTimestamps(doc));
        var exported = SrtSubtitleExporter.Export(doc);
        Assert.Contains("00:00:10,000 --> 00:00:15,000", exported);
    }

    [Fact]
    public void SubtitleMergeProcessor_SrtMergeDoesNotClampNegativeShifts()
    {
        const string activeSrt = "1\r\n00:00:01,000 --> 00:00:03,000\r\nAktywny blok";
        const string importedSrt = "1\r\n00:00:10,000 --> 00:00:12,000\r\nImportowany blok";

        // Przesunięcie kotwicy (selectedCue 0) do pozycji ujemnej -5000ms
        // delta = -5000 - 10000 = -15000ms -> start = -5000ms, end = -3000ms
        var result = SubtitleMergeProcessor.Merge(
            activeSrt,
            importedSrt,
            SubtitleLogicalFormat.Srt,
            selectedCueIndex: 0,
            positionMilliseconds: -5000,
            scale: null,
            framesPerSecond: 25.0);

        Assert.Equal(1, result.ImportedCueCount);
        Assert.Contains("-00:00:05,000 --> -00:00:03,000", result.MergedText);
    }

    [Fact]
    public void SubtitleMergeProcessor_TmPlayerMergeDoesNotClampNegativeShifts()
    {
        const string activeTm = "00:00:01:Aktywny";
        const string importedTm = "00:00:10:Importowany";

        // pozycja -5000 ms (-5 sekund), anchor start = 10s, delta = -15s -> wynik -5s ("00:00:-5:")
        var result = SubtitleMergeProcessor.Merge(
            activeTm,
            importedTm,
            SubtitleLogicalFormat.TmPlayer,
            selectedCueIndex: 0,
            positionMilliseconds: -5000,
            scale: null,
            framesPerSecond: 25.0);

        Assert.Equal(1, result.ImportedCueCount);
        Assert.Contains("00:00:-5:Importowany", result.MergedText);
    }
}
