using System.Reflection;

namespace SubEdit.Player.WinForms;

/// <summary>Build-time values, read once from this loaded assembly; no runtime repository access.</summary>
internal sealed record ApplicationBuildInfo(string ProductVersion, string BuildDate, string BuildCommit,
    string BuildDirtyState, string InformationalVersion)
{
    public static ApplicationBuildInfo Current { get; } = FromMetadata(
        typeof(ApplicationBuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>(),
        typeof(ApplicationBuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    internal static ApplicationBuildInfo FromMetadata(IEnumerable<AssemblyMetadataAttribute> metadata, string? informationalVersion)
    {
        var values = metadata.ToArray();
        string Read(string key) => values.FirstOrDefault(attribute =>
            string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))?.Value is { } value &&
            !string.IsNullOrWhiteSpace(value) ? value : "unknown";
        var state = Read("BuildDirtyState").ToUpperInvariant();
        return new(Read("ProductVersion"), Read("BuildDate"), Read("BuildCommit"),
            state is "CLEAN" or "DIRTY" ? state : "UNKNOWN",
            string.IsNullOrWhiteSpace(informationalVersion) ? "unknown" : informationalVersion);
    }
}
