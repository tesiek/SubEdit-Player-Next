using System.Text.Json.Serialization;

namespace SubEdit.Player.Core.Configuration;

public sealed class AppSettings
{
    public string Language { get; set; } = "pl-PL";
    public int Volume { get; set; } = 50;
    public int LastAudibleVolume { get; set; } = 50;
    [JsonConverter(typeof(JsonStringEnumConverter<TimeDisplayMode>))]
    public TimeDisplayMode TimeDisplayMode { get; set; } = TimeDisplayMode.Elapsed;
    public bool TransparentSubtitles { get; set; } = true;
    public string SubtitleFontName { get; set; } = "Arial";
    public int SubtitleFontSize { get; set; } = 30;
    public bool SubtitleFontBold { get; set; }
    public SubtitleFontSizeMode SubtitleFontSizeMode { get; set; } = SubtitleFontSizeMode.RelativeToWindow;
    public int MinimumSubtitleFontSize { get; set; } = 8;
    public int SubtitleTextColorArgb { get; set; } = unchecked((int)0xFFFFFFFF);
    public int SubtitleBackgroundColorArgb { get; set; } = unchecked((int)0xFF000000);
    public int SubtitleBackgroundOpacityPercent { get; set; } = 75;
    public int SubtitleOutlineColorArgb { get; set; } = unchecked((int)0xFF000000);
    public int SubtitleOutlineThickness { get; set; } = 2;
    public SubtitleAlignment SubtitleAlignment { get; set; } = SubtitleAlignment.BottomCenter;
    public int SubtitleMarginLeft { get; set; }
    public int SubtitleMarginRight { get; set; }
    public int SubtitleMarginTop { get; set; }
    public int SubtitleMarginBottom { get; set; } = 5;
    public bool PositionSubtitlesRelativeToVideoFrame { get; set; }
    public int LastOptionsTabIndex { get; set; }
    public string ActiveSubtitleFormat { get; set; } = "TmPlayerLong";
    public int LongSubtitleLineLength { get; set; } = 40;
    public EditorSettings Editor { get; set; } = new();
    public EditingSettings Editing { get; set; } = new();
    public SubtitlePlaybackSettings SubtitlePlayback { get; set; } = new();
    public ShortSubtitleSearchSettings ShortSubtitleSearch { get; set; } = new();
    public SubtitleGapSearchSettings SubtitleGapSearch { get; set; } = new();
    public SubtitleCorrectionSettings SubtitleCorrection { get; set; } = new();
    public SubtitleDisplayTimeSettings SubtitleDisplayTime { get; set; } = new();
    public RapidSubtitleMergeSettings RapidSubtitleMerge { get; set; } = new();
    public LongSubtitleLineSplitSettings LongSubtitleLineSplit { get; set; } = new();
    public SubtitleLineLimitSettings SubtitleLineLimit { get; set; } = new();
    public WindowSettings Window { get; set; } = new();
    public PlaybackSettings Playback { get; set; } = new();
    public InformationOsdSettings InformationOsd { get; set; } = new();
}

public enum TimeDisplayMode
{
    Elapsed,
    Remaining,
    Hidden
}

public enum SubtitleFontSizeMode
{
    Constant,
    RelativeToWindow
}

public enum SubtitleAlignment
{
    TopLeft = 1,
    TopCenter = 2,
    TopRight = 3,
    MiddleLeft = 4,
    MiddleCenter = 5,
    MiddleRight = 6,
    BottomLeft = 7,
    BottomCenter = 8,
    BottomRight = 9
}

public enum InformationOsdPosition
{
    TopRight,
    Hidden
}

public sealed class InformationOsdSettings
{
    public InformationOsdPosition Position { get; set; } = InformationOsdPosition.TopRight;
    public int FontSize { get; set; } = 18;
    public double DisplayDurationSeconds { get; set; } = 1.5d;
    public bool ShowInWindowedMode { get; set; } = true;
    public int TextColorArgb { get; set; } = unchecked((int)0xFFC0C0C0);
    public int BackgroundColorArgb { get; set; } = unchecked((int)0xFF202020);
    public int BackgroundOpacityPercent { get; set; } = 50;
    public int BorderColorArgb { get; set; } = unchecked((int)0xFF323232);
    public int BorderThickness { get; set; } = 1;
}

public sealed class EditorSettings
{
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveMinutes { get; set; } = 5;
    public bool CreateBackupFile { get; set; } = true;
    public int BackupFileCount { get; set; } = 1;
    public string FontName { get; set; } = "Courier New";
    public int FontSize { get; set; } = 10;
    public bool HighlightInvalidFormat { get; set; } = true;
    public bool HighlightUnsortedLines { get; set; } = true;
    public bool HighlightShortLines { get; set; } = true;
    public bool HighlightVeryShortLines { get; set; } = true;
    public bool MoveCaretToCurrentSubtitleOnEditorShow { get; set; }
    public bool MoveCaretToCurrentSubtitleOnFullScreenExit { get; set; }
}

public sealed class EditingSettings
{
    public double ReactionSeconds { get; set; } = 0.25d;
    public bool ApplyReactionToEndFrame { get; set; }
    public bool ApplyReactionWhenMeasuringSubtitleDelay { get; set; }
    public bool PreserveSubtitleDurationWhenInsertingStartFrame { get; set; }
    public bool MoveCaretToNextLineAfterInsertingFrame { get; set; }
    public double DefaultFiftyCharacterDisplaySeconds { get; set; } = 3d;
    public double AdditionalDefaultDisplaySeconds { get; set; } = 2d;
}

public sealed class SubtitlePlaybackSettings
{
    public bool AdjustLineWidthToScreen { get; set; } = true;
    public bool InterpretMicroDvdControlCodes { get; set; } = true;
    public bool WarnOnUnsortedSubtitles { get; set; } = true;
    public bool ItalicLinesStartingWithSlash { get; set; } = true;
    public bool ItalicLinesStartingWithAsterisk { get; set; } = true;
    public bool ItalicLinesStartingWithHash { get; set; } = true;
    public int MinimumGapFrames { get; set; } = 2;
}

public sealed class ShortSubtitleSearchSettings
{
    public double MaximumDurationSeconds { get; set; } = 1d;
    public bool AdjustForLineLength { get; set; } = true;
}

public sealed class SubtitleGapSearchSettings
{
    public double MinimumGapSeconds { get; set; } = 1d;
}

public sealed class SubtitleCorrectionSettings
{
    public bool LimitDisplayTime { get; set; } = true;
    public bool FixPunctuation { get; set; }
    public bool FixDialogueDashes { get; set; } = true;
    public bool AddSpaceAfterDialogueDash { get; set; } = true;
    public bool RemoveEmptyLines { get; set; } = true;
    public bool RemoveRepeatedLines { get; set; } = true;
    public bool RemoveDoubleSpaces { get; set; } = true;
    public bool TrimLineSpaces { get; set; } = true;
    public bool RemoveSingleDialogueDash { get; set; }
    public bool RemoveEllipsesFromSingleLines { get; set; }
    public bool RemoveSpacesBetweenDigits { get; set; }
    public bool RemoveMicroDvdColorInformation { get; set; }
}

public sealed class SubtitleDisplayTimeSettings
{
    public bool ChangeOnlyUndefinedEndTimes { get; set; }
    public bool DurationDependsOnTextLength { get; set; } = true;
    public bool BlockEndTime { get; set; } = true;
    public double BaseDurationSeconds { get; set; } = 2.5d;
    public double AdditionalDurationSeconds { get; set; }
}

public sealed class RapidSubtitleMergeSettings
{
    public double MaximumFiftyCharacterDisplaySeconds { get; set; } = 2d;
    public double MaximumFirstToLastEndSeconds { get; set; } = 3d;
    public double MaximumFirstToLastStartSeconds { get; set; } = 1d;
    public bool AddAsConsecutiveLines { get; set; }
    public int MaximumLines { get; set; } = 2;
    public bool LimitCharacters { get; set; } = true;
    public int MaximumCharacters { get; set; } = 110;
}

public sealed class LongSubtitleLineSplitSettings
{
    public int MaximumLineLength { get; set; } = 40;
    public bool LimitLineCount { get; set; } = true;
    public int MaximumLines { get; set; } = 3;
    public bool RemoveExistingSplitters { get; set; }
    public bool PreserveSplittersBeforeDialogues { get; set; }
}

public sealed class SubtitleLineLimitSettings
{
    public int MaximumLines { get; set; } = 3;
}

public sealed class PlaybackSettings
{
    public VideoDecodingMode VideoDecodingMode { get; set; } = VideoDecodingMode.Automatic;
    public int UserSeekBackwardSeconds { get; set; } = 3;
    public int UserSeekForwardSeconds { get; set; } = 3;
    public bool PauseOnMinimize { get; set; } = true;
    public bool PauseWhenDialogDisplayed { get; set; }
    public bool PauseOnDeactivate { get; set; }
    public bool ExitFullscreenWhenPlaybackEnds { get; set; }
    public string? LastVideoPath { get; set; }
    public string? LastSubtitlePath { get; set; }
    public List<string> RecentVideoPaths { get; set; } = [];
    public List<string> RecentSubtitlePaths { get; set; } = [];
    public long LastPositionMilliseconds { get; set; }
    public int? LastAudioTrackId { get; set; }
    public int? LastSubtitleTrackId { get; set; }
}

public enum VideoDecodingMode
{
    Automatic = 0,
    Software = 1
}

public sealed class WindowSettings
{
    public int Left { get; set; } = 80;
    public int Top { get; set; } = 80;
    public int Width { get; set; } = 1100;
    public int Height { get; set; } = 720;
    public int SplitterDistance { get; set; } = 300;
    public bool Maximized { get; set; }
}
