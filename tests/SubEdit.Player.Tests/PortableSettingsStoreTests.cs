using System.Text.Json;
using SubEdit.Player.Core.Configuration;
using Xunit;

namespace SubEdit.Player.Tests;

public sealed class PortableSettingsStoreTests : IDisposable
{
    private readonly string _testDirectory;

    public PortableSettingsStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "subedit_settings_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup must not mask the test result.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup must not mask the test result.
        }
    }

    [Fact]
    public void Load_WhenConfigurationFileDoesNotExist_ReturnsDefaultSettingsWithInitializedSections()
    {
        var store = new PortableSettingsStore(_testDirectory);

        Assert.Equal(Path.Combine(_testDirectory, "SubEdit.Player.Next.json"), store.ConfigurationPath);
        Assert.False(File.Exists(store.ConfigurationPath));

        AssertUsableDefaults(store.Load());
    }

    [Fact]
    public void TrySave_And_Load_PreservesSettingsRoundTrip()
    {
        var store = new PortableSettingsStore(_testDirectory);
        var videoPath = Path.Combine(_testDirectory, "Zażółć gęślą jaźń 🎬.mkv");
        var settings = new AppSettings
        {
            Volume = 73,
            Window = new WindowSettings { Width = 1280, Maximized = true },
            Editor = new EditorSettings { AutoSaveMinutes = 12, FontSize = 14 },
            Playback = new PlaybackSettings
            {
                LastVideoPath = videoPath,
                VideoDecodingMode = VideoDecodingMode.Software
            },
            InformationOsd = new InformationOsdSettings { DisplayDurationSeconds = 2.75d }
        };

        Assert.True(store.TrySave(settings));

        var loaded = new PortableSettingsStore(_testDirectory).Load();
        Assert.Equal(73, loaded.Volume);
        Assert.Equal(1280, loaded.Window.Width);
        Assert.True(loaded.Window.Maximized);
        Assert.Equal(12, loaded.Editor.AutoSaveMinutes);
        Assert.Equal(14, loaded.Editor.FontSize);
        Assert.Equal(videoPath, loaded.Playback.LastVideoPath);
        Assert.Equal(VideoDecodingMode.Software, loaded.Playback.VideoDecodingMode);
        Assert.Equal(2.75d, loaded.InformationOsd.DisplayDurationSeconds);
    }

    [Fact]
    public void Load_WhenJsonIsCorruptOrMalformed_ReturnsDefaultSettingsWithoutThrowing()
    {
        var store = new PortableSettingsStore(_testDirectory);
        File.WriteAllText(store.ConfigurationPath, "{ bad json");

        AssertUsableDefaults(store.Load());
    }

    [Fact]
    public void Load_WhenValuesAreOutOfBounds_NormalizesAndClampsCorrectly()
    {
        var store = new PortableSettingsStore(_testDirectory);
        File.WriteAllText(store.ConfigurationPath, """
            {
              "Volume": 150,
              "LastAudibleVolume": 0,
              "SubtitleFontName": null,
              "Window": null,
              "Editor": { "AutoSaveMinutes": 0, "BackupFileCount": 999 },
              "Editing": { "ReactionSeconds": 80 },
              "InformationOsd": { "DisplayDurationSeconds": -1 },
              "Playback": {
                "VideoDecodingMode": 999,
                "RecentVideoPaths": [" ", "first.mkv", "FIRST.MKV", "second.mkv", "third.mkv", "fourth.mkv", "fifth.mkv", "sixth.mkv"],
                "RecentSubtitlePaths": null
              }
            }
            """);

        var loaded = store.Load();

        Assert.Equal(100, loaded.Volume);
        Assert.Equal(1, loaded.LastAudibleVolume);
        Assert.Equal("Arial", loaded.SubtitleFontName);
        Assert.NotNull(loaded.Window);
        Assert.Equal(1100, loaded.Window.Width);
        Assert.Equal(1, loaded.Editor.AutoSaveMinutes);
        Assert.Equal(99, loaded.Editor.BackupFileCount);
        Assert.Equal(60d, loaded.Editing.ReactionSeconds);
        Assert.Equal(0d, loaded.InformationOsd.DisplayDurationSeconds);
        Assert.Equal(VideoDecodingMode.Automatic, loaded.Playback.VideoDecodingMode);
        Assert.Equal(new[] { "first.mkv", "second.mkv", "third.mkv", "fourth.mkv", "fifth.mkv" }, loaded.Playback.RecentVideoPaths);
        Assert.Empty(loaded.Playback.RecentSubtitlePaths);
    }

    [Fact]
    public void TrySave_UsesAtomicTemporaryFileAndReplacesTarget()
    {
        var store = new PortableSettingsStore(_testDirectory);

        Assert.True(store.TrySave(new AppSettings { Volume = 25 }));
        Assert.True(File.Exists(store.ConfigurationPath));
        Assert.False(File.Exists(store.ConfigurationPath + ".tmp"));

        Assert.True(store.TrySave(new AppSettings { Volume = 82 }));

        Assert.True(File.Exists(store.ConfigurationPath));
        Assert.Equal(82, new PortableSettingsStore(_testDirectory).Load().Volume);
        Assert.False(File.Exists(store.ConfigurationPath + ".tmp"));
    }

    [Fact]
    public void Load_WhenTimeDisplayModeIsMissing_DefaultsToElapsedAndSavesNormally()
    {
        var store = new PortableSettingsStore(_testDirectory);
        File.WriteAllText(store.ConfigurationPath, """{"Volume":73}""");

        var settings = store.Load();

        Assert.Equal(TimeDisplayMode.Elapsed, settings.TimeDisplayMode);
        Assert.Equal(73, settings.Volume);
        Assert.True(store.TrySave(settings));
        using var json = JsonDocument.Parse(File.ReadAllText(store.ConfigurationPath));
        Assert.Equal("Elapsed", json.RootElement.GetProperty("TimeDisplayMode").GetString());
    }

    [Fact]
    public void TrySave_And_Load_PreservesEveryTimeDisplayModeAsReadableJson()
    {
        var store = new PortableSettingsStore(_testDirectory);
        foreach (var mode in Enum.GetValues<TimeDisplayMode>())
        {
            Assert.True(store.TrySave(new AppSettings { TimeDisplayMode = mode }));

            using var json = JsonDocument.Parse(File.ReadAllText(store.ConfigurationPath));
            Assert.Equal(mode.ToString(), json.RootElement.GetProperty("TimeDisplayMode").GetString());
            Assert.False(json.RootElement.TryGetProperty("ShowFullScreenTimeCounter", out _));
            Assert.Equal(mode, new PortableSettingsStore(_testDirectory).Load().TimeDisplayMode);
        }
    }

    private static void AssertUsableDefaults(AppSettings settings)
    {
        Assert.NotNull(settings);
        Assert.Equal("pl-PL", settings.Language);
        Assert.Equal(50, settings.Volume);
        Assert.Equal(TimeDisplayMode.Elapsed, settings.TimeDisplayMode);
        Assert.Equal("Arial", settings.SubtitleFontName);
        Assert.NotNull(settings.Window);
        Assert.NotNull(settings.Playback);
        Assert.NotNull(settings.Editor);
        Assert.NotNull(settings.Editing);
        Assert.NotNull(settings.InformationOsd);
        Assert.NotNull(settings.SubtitlePlayback);
        Assert.Empty(settings.Playback.RecentVideoPaths);
        Assert.Empty(settings.Playback.RecentSubtitlePaths);
    }
}
