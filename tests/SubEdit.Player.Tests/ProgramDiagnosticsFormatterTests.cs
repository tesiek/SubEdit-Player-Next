using System.Globalization;
using SubEdit.Player.Core.Diagnostics;
using Xunit;

namespace SubEdit.Player.Tests;

public class ProgramDiagnosticsFormatterTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 18, 12, 34, 56, TimeSpan.FromHours(2));

    private static ProgramDiagnosticsSnapshot Empty() => new(
        new DiagnosticProgramInfo(null, null, null, null, null, null, null),
        [], new DiagnosticMediaInfo(false), CreatedAt, null);

    [Fact]
    public void FullReport_HasExactSectionsLabelsAndValues()
    {
        var snapshot = new ProgramDiagnosticsSnapshot(
            new("SubEdit-Player Next", "1.0.0", "261018", "abc123", "CLEAN", "X64", ".NET 10.0.0"),
            [new("Flyleaf", "3.11.8"), new("FFmpeg", "9.0", "vendored")],
            new(true, @"C:\private\movie.mkv", 1571164160, "Matroska", 3723004,
                "H.264", 1920, 1080, "AAC 2.0", "Automatic", true),
            CreatedAt, "Europe/Warsaw",
            new("Windows 11", "24H2", "26100", "Example CPU", 17179869184,
                [new("Example GPU", "32.0.1")]));

        const string expected = """
            [PROGRAM]
            Nazwa: SubEdit-Player Next
            Wersja: 1.0.0
            Build Date: 261018
            Commit: abc123
            Stan buildu: CLEAN
            Architektura: X64
            .NET Runtime: .NET 10.0.0

            [BIBLIOTEKI]
            Flyleaf: 3.11.8
            FFmpeg: 9.0 [vendored]

            [SYSTEM I SPRZĘT]
            Windows: Windows 11
            Wersja: 24H2
            Build: 26100
            CPU: Example CPU
            RAM: 16 GiB (17179869184 bajtów)
            GPU #1: Example GPU
            Sterownik GPU #1: 32.0.1

            [AKTUALNY FILM]
            Nazwa: movie.mkv
            Rozmiar: 1571164160 bajtów
            Kontener: Matroska
            Czas trwania: 01:02:03.004
            Wideo: H.264
            Rozdzielczość: 1920 × 1080
            Audio: AAC 2.0
            Tryb dekodowania: Automatic
            Akceleracja sprzętowa: Tak

            [RAPORT]
            Utworzono: 2026-10-18 12:34:56 +02:00
            Strefa czasowa: Europe/Warsaw
            """;
        Assert.Equal(Normalize(expected), Normalize(ProgramDiagnosticsFormatter.Format(snapshot)));
    }

    [Fact]
    public void EmptyReport_ExplicitlyShowsUnavailableDataAndClosedMedia()
    {
        const string expected = """
            [PROGRAM]
            Nazwa: niedostępne
            Wersja: niedostępne
            Build Date: niedostępne
            Commit: niedostępne
            Stan buildu: UNKNOWN
            Architektura: niedostępne
            .NET Runtime: niedostępne

            [BIBLIOTEKI]
            niedostępne

            [SYSTEM I SPRZĘT]
            Windows: niedostępne
            Wersja: niedostępne
            Build: niedostępne
            CPU: niedostępne
            RAM: niedostępne
            GPU #1: niedostępne
            Sterownik GPU #1: niedostępne

            [AKTUALNY FILM]
            Brak otwartego pliku.

            [RAPORT]
            Utworzono: 2026-10-18 12:34:56 +02:00
            Strefa czasowa: niedostępne
            """;
        Assert.Equal(Normalize(expected), Normalize(ProgramDiagnosticsFormatter.Format(Empty())));
    }

    [Theory]
    [InlineData("CLEAN", "CLEAN")]
    [InlineData(" clean ", "CLEAN")]
    [InlineData("DIRTY", "DIRTY")]
    [InlineData("dirty", "DIRTY")]
    [InlineData("UNKNOWN", "UNKNOWN")]
    [InlineData("invalid", "UNKNOWN")]
    [InlineData(null, "UNKNOWN")]
    [InlineData("", "UNKNOWN")]
    public void BuildState_IsNormalizedWithoutChangingProductVersion(string? state, string expected)
    {
        var snapshot = new ProgramDiagnosticsSnapshot(
            new("Player", "2.3.4", "261018", "abc123", state, "X64", "10.0"),
            [], new(false), CreatedAt, null);
        var lines = Lines(ProgramDiagnosticsFormatter.Format(snapshot));
        Assert.Contains("Stan buildu: " + expected, lines);
        Assert.Contains("Wersja: 2.3.4", lines);
        Assert.Contains("Commit: abc123", lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    [InlineData("unknown")]
    [InlineData(" UNKNOWN ")]
    public void MissingCommit_IsExplicitlyUnavailable(string? commit)
    {
        var snapshot = new ProgramDiagnosticsSnapshot(
            new(null, null, null, commit, null, null, null), [], new(false), CreatedAt, null);
        Assert.Contains("Commit: niedostępne", Lines(ProgramDiagnosticsFormatter.Format(snapshot)));
    }

    [Fact]
    public void LoadingSystem_KeepsAllOtherSectionsAndAvailableSystemValues()
    {
        var snapshot = Empty() with { System = new(cpu: "Known CPU") };
        var loading = ProgramDiagnosticsFormatter.Format(snapshot, loadingSystem: true);
        Assert.Equal(ProgramDiagnosticsFormatter.Format(snapshot),
            loading.Replace("Pobieranie danych systemowych..." + Environment.NewLine, ""));
        Assert.Contains("CPU: Known CPU", Lines(loading));
        Assert.DoesNotContain("Pobieranie danych systemowych...", ProgramDiagnosticsFormatter.Format(snapshot));
    }

    [Fact]
    public void MultipleGpus_HaveIndependentNamesDriverVersionsAndStableNumbers()
    {
        var snapshot = Empty() with
        {
            System = new(gpus: [new("Integrated", "1.2"), new("Discrete", null), new(null, "3.4")])
        };
        var lines = Lines(ProgramDiagnosticsFormatter.Format(snapshot));
        Assert.Contains("GPU #1: Integrated", lines);
        Assert.Contains("Sterownik GPU #1: 1.2", lines);
        Assert.Contains("GPU #2: Discrete", lines);
        Assert.Contains("Sterownik GPU #2: niedostępne", lines);
        Assert.Contains("GPU #3: niedostępne", lines);
        Assert.Contains("Sterownik GPU #3: 3.4", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("GPU #4:", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenMediaWithoutMetadata_KeepsEveryMediaField()
    {
        var report = ProgramDiagnosticsFormatter.Format(Empty() with { Media = new(true) });
        var media = report.Split("[AKTUALNY FILM]")[1].Split("[RAPORT]")[0];
        var expected = new[] { "Nazwa", "Rozmiar", "Kontener", "Czas trwania", "Wideo",
            "Rozdzielczość", "Audio", "Tryb dekodowania", "Akceleracja sprzętowa" };
        Assert.Equal(expected.Select(label => label + ": niedostępne"), Lines(media));
        Assert.DoesNotContain("Brak otwartego pliku.", report);
    }

    [Fact]
    public void ClosedMedia_DoesNotExposeStaleMetadata()
    {
        var report = ProgramDiagnosticsFormatter.Format(Empty() with
        {
            Media = new(false, @"C:\private\secret.mkv", Container: "stale-container")
        });
        Assert.Contains("Brak otwartego pliku.", report);
        Assert.DoesNotContain("secret", report);
        Assert.DoesNotContain("stale-container", report);
    }

    [Theory]
    [InlineData(0L, "0 bajtów")]
    [InlineData(1571164160L, "1571164160 bajtów")]
    [InlineData(long.MaxValue, "9223372036854775807 bajtów")]
    [InlineData(-1L, "niedostępne")]
    [InlineData(null, "niedostępne")]
    public void FileSize_UsesExactUngroupedBytes(long? bytes, string expected)
    {
        var snapshot = Empty() with { Media = new(true, FileSizeBytes: bytes) };
        Assert.Contains("Rozmiar: " + expected, Lines(ProgramDiagnosticsFormatter.Format(snapshot)));
    }

    [Theory]
    [InlineData(0L, "00:00:00.000")]
    [InlineData(1L, "00:00:00.001")]
    [InlineData(86399999L, "23:59:59.999")]
    [InlineData(86400000L, "24:00:00.000")]
    [InlineData(360001007L, "100:00:01.007")]
    [InlineData(long.MaxValue, "2562047788015:12:55.807")]
    [InlineData(-1L, "niedostępne")]
    [InlineData(null, "niedostępne")]
    public void Duration_PreservesMillisecondsAndHoursBeyondOneDay(long? milliseconds, string expected)
    {
        var snapshot = Empty() with { Media = new(true, DurationMilliseconds: milliseconds) };
        Assert.Contains("Czas trwania: " + expected, Lines(ProgramDiagnosticsFormatter.Format(snapshot)));
    }

    [Theory]
    [InlineData("Automatic", true, "Tak")]
    [InlineData("Automatic", false, "Nie")]
    [InlineData("Automatic", null, "niedostępne")]
    [InlineData("Software", true, "Tak")]
    [InlineData("Software", false, "Nie")]
    [InlineData("Software", null, "niedostępne")]
    [InlineData(null, true, "Tak")]
    public void HardwareAcceleration_IsIndependentOfRequestedDecodingMode(string? mode, bool? hardware, string expected)
    {
        var snapshot = Empty() with { Media = new(true, DecodingMode: mode, IsHardwareAccelerated: hardware) };
        var lines = Lines(ProgramDiagnosticsFormatter.Format(snapshot));
        Assert.Contains("Tryb dekodowania: " + (mode ?? "niedostępne"), lines);
        Assert.Contains("Akceleracja sprzętowa: " + expected, lines);
    }

    [Fact]
    public void Libraries_UseOneLinePerLibraryAndOnlyNonemptyDetails()
    {
        var snapshot = new ProgramDiagnosticsSnapshot(Empty().Program,
            [new("Flyleaf", "3.11.8"), new("FFmpeg", null, "vendored"),
             new("Scintilla5.NET", "7.0.0", " \r\n "), new("Lexilla", "unknown", "native")],
            new(false), CreatedAt, null);
        var libraries = ProgramDiagnosticsFormatter.Format(snapshot)
            .Split("[BIBLIOTEKI]")[1].Split("[SYSTEM I SPRZĘT]")[0];
        Assert.Equal(new[] { "Flyleaf: 3.11.8", "FFmpeg: niedostępne [vendored]",
            "Scintilla5.NET: 7.0.0", "Lexilla: niedostępne [native]" }, Lines(libraries));
    }

    [Theory]
    [InlineData(@"C:\Users\Private\movie.mkv", "movie.mkv")]
    [InlineData(@"\\server\Private\movie.mkv", "movie.mkv")]
    [InlineData("/home/private/movie.mkv", "movie.mkv")]
    [InlineData(@"C:\Private/mixed\movie.mkv", "movie.mkv")]
    [InlineData("C:movie.mkv", "movie.mkv")]
    [InlineData("movie.mkv", "movie.mkv")]
    [InlineData(@"C:\Private\", "niedostępne")]
    [InlineData(null, "niedostępne")]
    public void MovieName_StripsBothPathSeparatorsRegardlessOfHostPlatform(string? path, string expected)
    {
        var report = ProgramDiagnosticsFormatter.Format(Empty() with { Media = new(true, path) });
        Assert.Contains("Nazwa: " + expected, Lines(report));
        Assert.DoesNotContain("Private", report);
        Assert.DoesNotContain("private", report);
        Assert.DoesNotContain("server", report);
        Assert.DoesNotContain("C:", report);
    }

    [Theory]
    [InlineData(@"C:\Users\Private\library.dll")]
    [InlineData("/home/private/library.so")]
    [InlineData(@"..\private\library.dll")]
    public void Versions_DoNotExposePathsMistakenForVersionMetadata(string path)
    {
        var snapshot = new ProgramDiagnosticsSnapshot(
            new("Player", path, null, null, null, null, null),
            [new("Library", path)], new(false), CreatedAt, null, new(gpus: [new("GPU", path)]));
        var report = ProgramDiagnosticsFormatter.Format(snapshot);
        Assert.Contains("Wersja: niedostępne", Lines(report));
        Assert.Contains("Library: niedostępne", Lines(report));
        Assert.Contains("Sterownik GPU #1: niedostępne", Lines(report));
        Assert.DoesNotContain(path, report);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void ExternalStrings_CannotInjectReportLinesOrSections(string newline)
    {
        var injected = "safe" + newline + "[INJECTED]";
        var snapshot = new ProgramDiagnosticsSnapshot(
            new(injected, injected, injected, injected, injected, injected, injected),
            [new(injected, injected, injected)],
            new(true, injected, Container: injected, VideoCodec: injected, Audio: injected, DecodingMode: injected),
            CreatedAt, injected, new(injected, injected, injected, injected, gpus: [new(injected, injected)]));
        var report = ProgramDiagnosticsFormatter.Format(snapshot);
        var lines = Lines(report);
        Assert.Equal(new[] { "[PROGRAM]", "[BIBLIOTEKI]", "[SYSTEM I SPRZĘT]", "[AKTUALNY FILM]", "[RAPORT]" },
            lines.Where(line => line.StartsWith('[')));
        Assert.Equal(31, lines.Length);
        Assert.DoesNotContain('\r', report.Replace(Environment.NewLine, ""));
        Assert.DoesNotContain('\n', report.Replace(Environment.NewLine, ""));
        Assert.DoesNotContain('\u0085', report);
        Assert.DoesNotContain('\u2028', report);
        Assert.DoesNotContain('\u2029', report);
        Assert.Contains("CPU: safe", report);
        Assert.Contains("Tryb dekodowania: safe", report);
    }

    [Fact]
    public void Snapshot_DetachesListsAndExposesReadOnlyCollections()
    {
        var library = new DiagnosticLibrary("Library", "1.0");
        var gpu = new DiagnosticGpu("GPU", "2.0");
        var libraries = new List<DiagnosticLibrary> { library };
        var gpus = new List<DiagnosticGpu> { gpu };
        var system = new DiagnosticSystemInfo(gpus: gpus);
        var snapshot = new ProgramDiagnosticsSnapshot(Empty().Program, libraries, new(false), CreatedAt, null, system);
        var report = ProgramDiagnosticsFormatter.Format(snapshot);
        libraries[0] = new("Changed", "9.0");
        gpus.Clear();
        Assert.Equal(library, Assert.Single(snapshot.Libraries));
        Assert.Equal(gpu, Assert.Single(system.Gpus));
        Assert.Throws<NotSupportedException>(() => ((IList<DiagnosticLibrary>)snapshot.Libraries)[0] = new("Other", null));
        Assert.Throws<NotSupportedException>(() => ((IList<DiagnosticGpu>)system.Gpus).Clear());
        Assert.Equal(report, ProgramDiagnosticsFormatter.Format(snapshot));
    }

    [Fact]
    public void Snapshot_DetachesArraysAndDeferredEnumerables()
    {
        DiagnosticLibrary[] libraries = [new("Library", "1.0")];
        DiagnosticGpu[] gpus = [new("GPU", "2.0")];
        var system = new DiagnosticSystemInfo(gpus: gpus);
        var snapshot = new ProgramDiagnosticsSnapshot(Empty().Program,
            libraries.Select(item => item), new(false), CreatedAt, null, system);
        libraries[0] = new("Changed", null);
        gpus[0] = new("Changed", null);
        Assert.Equal("Library", Assert.Single(snapshot.Libraries).Name);
        Assert.Equal("GPU", Assert.Single(system.Gpus).Name);
        Assert.Empty(new DiagnosticSystemInfo().Gpus);
    }

    [Fact]
    public void UpdatingSnapshot_PreservesOneTimestampAndLeavesOriginalUnchanged()
    {
        var original = Empty();
        var updated = original with { System = new(cpu: "CPU"), Media = new(true, "movie.mkv") };
        var first = ProgramDiagnosticsFormatter.Format(original, loadingSystem: true);
        var second = ProgramDiagnosticsFormatter.Format(updated);
        Assert.Equal(CreatedAt, updated.CreatedAt);
        Assert.Equal(original.TimeZoneId, updated.TimeZoneId);
        Assert.Same(original.Libraries, updated.Libraries);
        Assert.Null(original.System);
        Assert.False(original.Media.IsOpen);
        Assert.True(updated.Media.IsOpen);
        Assert.Equal(first.Split("[RAPORT]")[1], second.Split("[RAPORT]")[1]);
        Assert.Single(Lines(second), line => line.StartsWith("Utworzono:", StringComparison.Ordinal));
        Assert.Equal(second, ProgramDiagnosticsFormatter.Format(updated));
    }

    [Fact]
    public void NumericOutputAndTimestamp_AreIndependentOfCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
            var snapshot = new ProgramDiagnosticsSnapshot(Empty().Program, [],
                new(true, FileSizeBytes: 1571164160, DurationMilliseconds: 90000001),
                new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.FromHours(-7)), "Custom/Zone",
                new(installedMemoryBytes: 1342177280));
            var lines = Lines(ProgramDiagnosticsFormatter.Format(snapshot));
            Assert.Contains("RAM: 1.25 GiB (1342177280 bajtów)", lines);
            Assert.Contains("Rozmiar: 1571164160 bajtów", lines);
            Assert.Contains("Czas trwania: 25:00:00.001", lines);
            Assert.Contains("Utworzono: 2001-02-03 04:05:06 -07:00", lines);
            Assert.Contains("Strefa czasowa: Custom/Zone", lines);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(null, 1080)]
    [InlineData(1920, null)]
    [InlineData(0, 1080)]
    [InlineData(1920, -1)]
    public void IncompleteResolution_IsUnavailable(int? width, int? height)
    {
        var report = ProgramDiagnosticsFormatter.Format(Empty() with { Media = new(true, Width: width, Height: height) });
        Assert.Contains("Rozdzielczość: niedostępne", Lines(report));
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n").TrimEnd();
    private static string[] Lines(string value) => value.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
