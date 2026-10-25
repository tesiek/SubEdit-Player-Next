namespace SubEdit.Player.Core.Diagnostics;

public sealed record DiagnosticProgramInfo(
    string? Name,
    string? ProductVersion,
    string? BuildDate,
    string? BuildCommit,
    string? BuildDirtyState,
    string? ProcessArchitecture,
    string? Runtime);

public sealed record DiagnosticLibrary(string Name, string? Version, string? Details = null);

public sealed record DiagnosticGpu(string? Name, string? DriverVersion);

public sealed class DiagnosticSystemInfo
{
    public DiagnosticSystemInfo(
        string? windows = null,
        string? version = null,
        string? build = null,
        string? cpu = null,
        ulong? installedMemoryBytes = null,
        IEnumerable<DiagnosticGpu>? gpus = null)
    {
        Windows = windows;
        Version = version;
        Build = build;
        Cpu = cpu;
        InstalledMemoryBytes = installedMemoryBytes;
        Gpus = Array.AsReadOnly(gpus?.ToArray() ?? Array.Empty<DiagnosticGpu>());
    }

    public string? Windows { get; }
    public string? Version { get; }
    public string? Build { get; }
    public string? Cpu { get; }
    public ulong? InstalledMemoryBytes { get; }
    public IReadOnlyList<DiagnosticGpu> Gpus { get; }
}

public sealed record DiagnosticMediaInfo(
    bool IsOpen,
    string? FileName = null,
    long? FileSizeBytes = null,
    string? Container = null,
    long? DurationMilliseconds = null,
    string? VideoCodec = null,
    int? Width = null,
    int? Height = null,
    string? Audio = null,
    string? DecodingMode = null,
    bool? IsHardwareAccelerated = null);

public sealed record ProgramDiagnosticsSnapshot
{
    public ProgramDiagnosticsSnapshot(
        DiagnosticProgramInfo program,
        IEnumerable<DiagnosticLibrary> libraries,
        DiagnosticMediaInfo media,
        DateTimeOffset createdAt,
        string? timeZoneId,
        DiagnosticSystemInfo? system = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(libraries);
        ArgumentNullException.ThrowIfNull(media);

        Program = program;
        Libraries = Array.AsReadOnly(libraries.ToArray());
        Media = media;
        CreatedAt = createdAt;
        TimeZoneId = timeZoneId;
        System = system;
    }

    public DiagnosticProgramInfo Program { get; }
    public IReadOnlyList<DiagnosticLibrary> Libraries { get; }
    public DiagnosticMediaInfo Media { get; init; }
    public DateTimeOffset CreatedAt { get; }
    public string? TimeZoneId { get; }
    public DiagnosticSystemInfo? System { get; init; }
}
