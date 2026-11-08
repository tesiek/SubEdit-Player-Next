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
        Assert.Equal(metadata["ProductVersion"], build.ProductVersion);
        Assert.Equal(metadata["BuildDate"], build.BuildDate);
        Assert.Matches("^[0-9]{6}$", build.BuildDate);
        Assert.Equal(metadata["BuildCommit"], build.BuildCommit);
        Assert.Equal(metadata["BuildDirtyState"], build.BuildDirtyState);
        Assert.Equal(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, build.InformationalVersion);
        var expectedAssemblyVersion = Version.Parse($"{build.ProductVersion}.0");
        Assert.Equal(expectedAssemblyVersion, assembly.GetName().Version);
        Assert.Equal(expectedAssemblyVersion.ToString(), assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        if (build.BuildCommit == "unknown")
        {
            Assert.Equal("UNKNOWN", build.BuildDirtyState);
            Assert.Equal($"{build.ProductVersion}+unknown", build.InformationalVersion);
        }
        else
        {
            Assert.Matches("^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$", build.BuildCommit);
            Assert.Contains(build.BuildDirtyState, new[] { "CLEAN", "DIRTY", "UNKNOWN" });
            Assert.Equal($"{build.ProductVersion}+{build.BuildCommit}.{build.BuildDirtyState.ToLowerInvariant()}", build.InformationalVersion);
        }
    }

    [Fact]
    public void ReleaseComponents_UseTheSameProductAndFileVersions()
    {
        var application = typeof(AboutDialog).Assembly;
        var productVersion = ApplicationBuildInfo.Current.ProductVersion;
        var fileVersion = Version.Parse($"{productVersion}.0");
        var outputDirectory = Path.GetDirectoryName(application.Location)!;

        foreach (var name in new[] { "SubEdit.Player.Core", "SubEdit.Player.UI", "SubEdit.Player.Next" })
        {
            var component = Assembly.Load(name);
            Assert.Equal(fileVersion, component.GetName().Version);
            var informationalVersion = component.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            Assert.Equal(productVersion, informationalVersion.Split('+', 2)[0]);
            AssertPeVersion(component.Location, fileVersion, informationalVersion);
        }

        AssertPeVersion(Path.Combine(outputDirectory, "SubEdit.Player.Next.exe"), fileVersion,
            ApplicationBuildInfo.Current.InformationalVersion);

        static void AssertPeVersion(string path, Version fileVersion, string informationalVersion)
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            Assert.Equal(fileVersion.ToString(), info.FileVersion);
            Assert.Equal(informationalVersion, info.ProductVersion);
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
