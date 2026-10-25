using System.Reflection;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class ApplicationBuildInfoTests
{
    [Fact]
    public void Current_UsesGeneratedAssemblyValuesAndCachesTheResult()
    {
        var build = ApplicationBuildInfo.Current;
        var assembly = typeof(AboutDialog).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(value => value.Key, value => value.Value);
        Assert.Same(build, ApplicationBuildInfo.Current);
        Assert.Equal("1.0.0", build.ProductVersion);
        Assert.Equal(metadata["ProductVersion"], build.ProductVersion);
        Assert.Equal(metadata["BuildDate"], build.BuildDate);
        Assert.Matches("^[0-9]{6}$", build.BuildDate);
        Assert.Equal(metadata["BuildCommit"], build.BuildCommit);
        Assert.Equal(metadata["BuildDirtyState"], build.BuildDirtyState);
        Assert.Equal(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, build.InformationalVersion);
        Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.Equal("1.0.0.0", assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        if (build.BuildCommit == "unknown")
        {
            Assert.Equal("UNKNOWN", build.BuildDirtyState);
            Assert.Equal("1.0.0+unknown", build.InformationalVersion);
        }
        else
        {
            Assert.Matches("^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$", build.BuildCommit);
            Assert.Contains(build.BuildDirtyState, new[] { "CLEAN", "DIRTY", "UNKNOWN" });
            Assert.Equal($"1.0.0+{build.BuildCommit}.{build.BuildDirtyState.ToLowerInvariant()}", build.InformationalVersion);
        }
    }

    [Theory]
    [InlineData("CLEAN")]
    [InlineData("DIRTY")]
    [InlineData("UNKNOWN")]
    public void Metadata_KeepsCommitAndStateSeparateFromProductVersion(string state)
    {
        const string revision = "0123456789abcdef0123456789abcdef01234567";
        var build = ApplicationBuildInfo.FromMetadata(
            [new("ProductVersion", "2.3.4"), new("BuildDate", "261018"), new("BuildCommit", revision), new("BuildDirtyState", state)],
            $"2.3.4+{revision}.{state.ToLowerInvariant()}");
        Assert.Equal("2.3.4", build.ProductVersion);
        Assert.Equal("261018", build.BuildDate);
        Assert.Equal(revision, build.BuildCommit);
        Assert.Equal(state, build.BuildDirtyState);
    }

    [Fact]
    public void MissingOrBlankMetadata_ReturnsExplicitUnknownWithoutInventingVersionOrCommit()
    {
        var build = ApplicationBuildInfo.FromMetadata([new("BuildDate", " "), new("BuildCommit", "")], null);
        Assert.Equal(new ApplicationBuildInfo("unknown", "unknown", "unknown", "UNKNOWN", "unknown"), build);
        Assert.Equal(build, ApplicationBuildInfo.FromMetadata([], ""));
    }

    [Fact]
    public void BuildDate_PreservesExistingCaseInsensitiveMetadataContract()
    {
        var build = ApplicationBuildInfo.FromMetadata([new("builddate", "261003"), new("builddirtystate", "invalid")], null);
        Assert.Equal("261003", build.BuildDate);
        Assert.Equal("UNKNOWN", build.BuildDirtyState);
    }
}
