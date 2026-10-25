using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleMergeAndExportTests
{
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
