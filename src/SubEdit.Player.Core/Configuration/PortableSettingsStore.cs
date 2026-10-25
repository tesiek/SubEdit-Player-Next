using System.Text.Json;

namespace SubEdit.Player.Core.Configuration;

public sealed class PortableSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public PortableSettingsStore(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ConfigurationPath = Path.Combine(applicationDirectory, "SubEdit.Player.Next.json");
    }

    public string ConfigurationPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(ConfigurationPath))
        {
            return new AppSettings();
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigurationPath), JsonOptions));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public bool TrySave(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var temporaryPath = ConfigurationPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporaryPath, ConfigurationPath, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        var normalized = settings ?? new AppSettings();
        normalized.Window ??= new WindowSettings();
        normalized.Playback ??= new PlaybackSettings();
        normalized.InformationOsd ??= new InformationOsdSettings();
        normalized.Editor ??= new EditorSettings();
        normalized.Editing ??= new EditingSettings();
        normalized.SubtitlePlayback ??= new SubtitlePlaybackSettings();
        normalized.ShortSubtitleSearch ??= new ShortSubtitleSearchSettings();
        normalized.SubtitleGapSearch ??= new SubtitleGapSearchSettings();
        normalized.SubtitleCorrection ??= new SubtitleCorrectionSettings();
        normalized.SubtitleDisplayTime ??= new SubtitleDisplayTimeSettings();
        normalized.RapidSubtitleMerge ??= new RapidSubtitleMergeSettings();
        normalized.LongSubtitleLineSplit ??= new LongSubtitleLineSplitSettings();
        normalized.SubtitleLineLimit ??= new SubtitleLineLimitSettings();
        if (!Enum.IsDefined(normalized.TimeDisplayMode))
        {
            normalized.TimeDisplayMode = TimeDisplayMode.Elapsed;
        }
        if (normalized.SubtitleDisplayTime.BaseDurationSeconds <= 0d)
        {
            normalized.SubtitleDisplayTime.BaseDurationSeconds = 2.5d;
        }
        if (!double.IsFinite(normalized.RapidSubtitleMerge.MaximumFiftyCharacterDisplaySeconds) ||
            normalized.RapidSubtitleMerge.MaximumFiftyCharacterDisplaySeconds < 0.1d)
        {
            normalized.RapidSubtitleMerge.MaximumFiftyCharacterDisplaySeconds = 2d;
        }
        if (!double.IsFinite(normalized.RapidSubtitleMerge.MaximumFirstToLastEndSeconds) ||
            normalized.RapidSubtitleMerge.MaximumFirstToLastEndSeconds < 0.1d)
        {
            normalized.RapidSubtitleMerge.MaximumFirstToLastEndSeconds = 3d;
        }
        if (!double.IsFinite(normalized.RapidSubtitleMerge.MaximumFirstToLastStartSeconds) ||
            normalized.RapidSubtitleMerge.MaximumFirstToLastStartSeconds < 0.1d)
        {
            normalized.RapidSubtitleMerge.MaximumFirstToLastStartSeconds = 1d;
        }
        normalized.RapidSubtitleMerge.MaximumLines = Math.Max(2, normalized.RapidSubtitleMerge.MaximumLines);
        normalized.RapidSubtitleMerge.MaximumCharacters = Math.Max(1, normalized.RapidSubtitleMerge.MaximumCharacters);
        normalized.LongSubtitleLineSplit.MaximumLineLength = Math.Max(1, normalized.LongSubtitleLineSplit.MaximumLineLength);
        normalized.LongSubtitleLineSplit.MaximumLines = Math.Max(1, normalized.LongSubtitleLineSplit.MaximumLines);
        normalized.SubtitleLineLimit.MaximumLines = Math.Max(1, normalized.SubtitleLineLimit.MaximumLines);
        if (!double.IsFinite(normalized.ShortSubtitleSearch.MaximumDurationSeconds) ||
            normalized.ShortSubtitleSearch.MaximumDurationSeconds <= 0d)
        {
            normalized.ShortSubtitleSearch.MaximumDurationSeconds = 1d;
        }
        if (!double.IsFinite(normalized.SubtitleGapSearch.MinimumGapSeconds) ||
            normalized.SubtitleGapSearch.MinimumGapSeconds <= 0d)
        {
            normalized.SubtitleGapSearch.MinimumGapSeconds = 1d;
        }
        normalized.Playback.RecentVideoPaths = NormalizeRecentFiles(normalized.Playback.RecentVideoPaths);
        normalized.Playback.RecentSubtitlePaths = NormalizeRecentFiles(normalized.Playback.RecentSubtitlePaths);
        if (!Enum.IsDefined(normalized.Playback.VideoDecodingMode))
        {
            normalized.Playback.VideoDecodingMode = VideoDecodingMode.Automatic;
        }
        normalized.Volume = Math.Clamp(normalized.Volume, 0, 100);
        normalized.LastAudibleVolume = Math.Clamp(normalized.LastAudibleVolume, 1, 100);
        normalized.SubtitleFontName = string.IsNullOrWhiteSpace(normalized.SubtitleFontName) ? "Arial" : normalized.SubtitleFontName;
        normalized.SubtitleFontSize = Math.Clamp(normalized.SubtitleFontSize, 8, 72);
        normalized.MinimumSubtitleFontSize = Math.Clamp(normalized.MinimumSubtitleFontSize, 2, 50);
        normalized.SubtitleBackgroundOpacityPercent = Math.Clamp(normalized.SubtitleBackgroundOpacityPercent, 0, 100);
        normalized.SubtitleOutlineThickness = Math.Clamp(normalized.SubtitleOutlineThickness, 0, 100);
        normalized.SubtitleMarginLeft = Math.Clamp(normalized.SubtitleMarginLeft, -10000, 10000);
        normalized.SubtitleMarginRight = Math.Clamp(normalized.SubtitleMarginRight, -10000, 10000);
        normalized.SubtitleMarginTop = Math.Clamp(normalized.SubtitleMarginTop, -10000, 10000);
        normalized.SubtitleMarginBottom = Math.Clamp(normalized.SubtitleMarginBottom, -10000, 10000);
        normalized.InformationOsd.FontSize = Math.Clamp(normalized.InformationOsd.FontSize, 10, 30);
        normalized.InformationOsd.BackgroundOpacityPercent = Math.Clamp(
            normalized.InformationOsd.BackgroundOpacityPercent,
            0,
            100);
        normalized.InformationOsd.BorderThickness = Math.Clamp(normalized.InformationOsd.BorderThickness, 0, 100);
        normalized.InformationOsd.DisplayDurationSeconds = ClampNonNegative(
            normalized.InformationOsd.DisplayDurationSeconds,
            9999d,
            1.5d);
        if (!Enum.IsDefined(normalized.InformationOsd.Position))
        {
            normalized.InformationOsd.Position = InformationOsdPosition.TopRight;
        }
        if (!Enum.IsDefined(normalized.SubtitleFontSizeMode))
        {
            normalized.SubtitleFontSizeMode = SubtitleFontSizeMode.RelativeToWindow;
        }
        if (!Enum.IsDefined(normalized.SubtitleAlignment))
        {
            normalized.SubtitleAlignment = SubtitleAlignment.BottomCenter;
        }
        normalized.Editor.AutoSaveMinutes = Math.Clamp(normalized.Editor.AutoSaveMinutes, 1, 60);
        normalized.Editor.BackupFileCount = Math.Clamp(normalized.Editor.BackupFileCount, 1, 99);
        normalized.Editor.FontName = string.IsNullOrWhiteSpace(normalized.Editor.FontName) ? "Courier New" : normalized.Editor.FontName;
        normalized.Editor.FontSize = Math.Clamp(normalized.Editor.FontSize, 6, 22);
        normalized.Editing.ReactionSeconds = ClampNonNegative(normalized.Editing.ReactionSeconds, 60d, 0.25d);
        normalized.Editing.DefaultFiftyCharacterDisplaySeconds = ClampNonNegative(normalized.Editing.DefaultFiftyCharacterDisplaySeconds, 9999d, 3d);
        normalized.Editing.AdditionalDefaultDisplaySeconds = ClampNonNegative(normalized.Editing.AdditionalDefaultDisplaySeconds, 9999d, 2d);
        normalized.SubtitlePlayback.MinimumGapFrames = Math.Clamp(normalized.SubtitlePlayback.MinimumGapFrames, 1, 20);
        return normalized;
    }

    private static double ClampNonNegative(double value, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, 0d, maximum) : fallback;

    private static List<string> NormalizeRecentFiles(List<string>? paths) =>
        (paths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
}
