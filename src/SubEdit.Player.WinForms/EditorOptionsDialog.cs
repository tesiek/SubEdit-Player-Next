using SubEdit.Player.Core.Configuration;
using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;

internal sealed partial class EditorOptionsDialog : DialogWindow
{
    internal OptionsDialogLayout LayoutView { get; } = new();
    private readonly OptionsFit _fit;
    private readonly UiFontOwner _fonts;
    private readonly EditorSettings _editorInitial;
    private readonly bool _softwareInitial;
    private readonly Dictionary<string, object> _initial = [];
    internal CheckBox AutoSave { get; private set; } = null!;
    internal NumericUpDown Minutes { get; private set; } = null!;
    internal NumericUpDown Copies { get; private set; } = null!;
    private CheckBox _backup = null!, _invalid = null!, _unsorted = null!, _short = null!, _veryShort = null!, _caret = null!, _fullscreenCaret = null!;
    private ComboBox _font = null!;
    private NumericUpDown _fontSize = null!;
    internal RadioButton Software { get; private set; } = null!;
    public EditorOptionsDialog(
        EditorSettings settings,
        EditingSettings editingSettings,
        SubtitlePlaybackSettings subtitlePlaybackSettings,
        PlaybackSettings playbackSettings,
        InformationOsdSettings informationOsdSettings,
        string subtitleFontName,
        int subtitleFontSize,
        bool subtitleFontBold,
        SubtitleFontSizeMode subtitleFontSizeMode,
        int minimumSubtitleFontSize,
        int subtitleTextColorArgb,
        int subtitleBackgroundColorArgb,
        int subtitleBackgroundTransparencyPercent,
        int subtitleOutlineColorArgb,
        int subtitleOutlineThickness,
        SubtitleAlignment subtitleAlignment,
        int subtitleMarginLeft,
        int subtitleMarginRight,
        int subtitleMarginTop,
        int subtitleMarginBottom,
        bool positionSubtitlesRelativeToVideoFrame,
        int selectedTabIndex)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(editingSettings);
        ArgumentNullException.ThrowIfNull(subtitlePlaybackSettings);
        ArgumentNullException.ThrowIfNull(playbackSettings);
        ArgumentNullException.ThrowIfNull(informationOsdSettings);
        _editorInitial = settings;
        _softwareInitial = playbackSettings.VideoDecodingMode == VideoDecodingMode.Software;
        _initial["ReactionSeconds"] = editingSettings.ReactionSeconds;
        _initial["ApplyReactionToEndFrame"] = editingSettings.ApplyReactionToEndFrame;
        _initial["ApplyReactionWhenMeasuringSubtitleDelay"] = editingSettings.ApplyReactionWhenMeasuringSubtitleDelay;
        _initial["PreserveSubtitleDurationWhenInsertingStartFrame"] = editingSettings.PreserveSubtitleDurationWhenInsertingStartFrame;
        _initial["MoveCaretToNextLineAfterInsertingFrame"] = editingSettings.MoveCaretToNextLineAfterInsertingFrame;
        _initial["DefaultFiftyCharacterDisplaySeconds"] = editingSettings.DefaultFiftyCharacterDisplaySeconds;
        _initial["AdditionalDefaultDisplaySeconds"] = editingSettings.AdditionalDefaultDisplaySeconds;
        _initial["AdjustLineWidthToScreen"] = subtitlePlaybackSettings.AdjustLineWidthToScreen;
        _initial["InterpretMicroDvdControlCodes"] = subtitlePlaybackSettings.InterpretMicroDvdControlCodes;
        _initial["WarnOnUnsortedSubtitles"] = subtitlePlaybackSettings.WarnOnUnsortedSubtitles;
        _initial["ItalicLinesStartingWithSlash"] = subtitlePlaybackSettings.ItalicLinesStartingWithSlash;
        _initial["ItalicLinesStartingWithAsterisk"] = subtitlePlaybackSettings.ItalicLinesStartingWithAsterisk;
        _initial["ItalicLinesStartingWithHash"] = subtitlePlaybackSettings.ItalicLinesStartingWithHash;
        _initial["MinimumGapFrames"] = subtitlePlaybackSettings.MinimumGapFrames;
        _initial["UserSeekBackwardSeconds"] = playbackSettings.UserSeekBackwardSeconds;
        _initial["UserSeekForwardSeconds"] = playbackSettings.UserSeekForwardSeconds;
        _initial["PauseOnMinimize"] = playbackSettings.PauseOnMinimize;
        _initial["PauseWhenDialogDisplayed"] = playbackSettings.PauseWhenDialogDisplayed;
        _initial["PauseOnDeactivate"] = playbackSettings.PauseOnDeactivate;
        _initial["ExitFullscreenWhenPlaybackEnds"] = playbackSettings.ExitFullscreenWhenPlaybackEnds;
        _initial["SubtitleFontName"] = subtitleFontName;
        _initial["SubtitleFontSize"] = subtitleFontSize;
        _initial["SubtitleFontBold"] = subtitleFontBold;
        _initial["MinimumSubtitleFontSize"] = minimumSubtitleFontSize;
        _initial["SubtitleMarginLeft"] = subtitleMarginLeft;
        _initial["SubtitleMarginRight"] = subtitleMarginRight;
        _initial["SubtitleMarginTop"] = subtitleMarginTop;
        _initial["SubtitleMarginBottom"] = subtitleMarginBottom;
        _initial["PositionSubtitlesRelativeToVideoFrame"] = positionSubtitlesRelativeToVideoFrame;
        _initial["SubtitleRelative"] = subtitleFontSizeMode == SubtitleFontSizeMode.RelativeToWindow;
        _initial["SubtitleTextColor"] = subtitleTextColorArgb;
        _initial["SubtitleBackgroundColor"] = subtitleBackgroundColorArgb;
        _initial["SubtitleTransparency"] = subtitleBackgroundTransparencyPercent;
        _initial["SubtitleOutlineColor"] = subtitleOutlineColorArgb;
        _initial["SubtitleOutline"] = subtitleOutlineThickness;
        _initial["SubtitleAlignment"] = (int)subtitleAlignment - 1;
        _initial["OsdHidden"] = informationOsdSettings.Position == InformationOsdPosition.Hidden;
        _initial["OsdFontSize"] = informationOsdSettings.FontSize;
        _initial["OsdDuration"] = informationOsdSettings.DisplayDurationSeconds;
        _initial["OsdWindowed"] = informationOsdSettings.ShowInWindowedMode;
        _initial["OsdTextColor"] = informationOsdSettings.TextColorArgb;
        _initial["OsdBackgroundColor"] = informationOsdSettings.BackgroundColorArgb;
        _initial["OsdTransparency"] = informationOsdSettings.BackgroundTransparencyPercent;
        _initial["OsdOutlineColor"] = informationOsdSettings.BorderColorArgb;
        _initial["OsdOutline"] = informationOsdSettings.BorderThickness;

        Text = "Opcje";
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiDesignTokens.Current.DialogFont;
        BackColor = UiDesignTokens.Current.Background; ForeColor = UiDesignTokens.Current.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowIcon = ShowInTaskbar = false;
        Controls.Add(LayoutView);
        LayoutView.Register("Edytor", EditorPage());
        LayoutView.Register("Edycja", EditingPage());
        LayoutView.Register("Napisy", SubtitlesPage());
        LayoutView.Register("Wygląd napisów", AppearancePage(false));
        LayoutView.Register("Film", MoviePage());
        LayoutView.Register("OSD", AppearancePage(true));
        LayoutView.Register("Wideo", VideoPage());
        LayoutView.Tabs.SelectedIndex = Math.Clamp(selectedTabIndex, 0, 6);
        LayoutView.Actions.Configure(DialogActionProfile.Options,
            new(DialogActionRole.Apply, "Zastosuj"), new(DialogActionRole.Ok, "OK", DialogResult.OK), new(DialogActionRole.Cancel, "Anuluj", DialogResult.Cancel));
        LayoutView.Actions.Button(DialogActionRole.Apply).Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
        LayoutView.Actions.Button(DialogActionRole.Ok).Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
        AcceptButton = LayoutView.Actions.Button(DialogActionRole.Ok);
        CancelButton = LayoutView.Actions.Button(DialogActionRole.Cancel);
        _fit = new OptionsFit(this, LayoutView);
        _fonts = new UiFontOwner(this) { SynchronizeDescendantFonts = true };
        SetUiFont(UiDesignTokens.Current.DialogFont.SizeInPoints);
    }
    internal void SetUiFont(float points) => LayoutView.UpdateMetrics(() => _fonts.SetSize(points));
    public event EventHandler? ApplyRequested;
    public int SelectedTabIndex => LayoutView.Tabs.SelectedIndex;
    private static NumericUpDown Number(int value, int min, int max) => new() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TextAlign = HorizontalAlignment.Right };
    private T Read<T>(string key) => (T)Convert.ChangeType(_readAdditional[key](), typeof(T));
    private SettingsPageLayout EditorPage()
    {
        var s = _editorInitial;
        var page = new SettingsPageLayout();
        var misc = page.AddSection("Automatyczny zapis i kopie");
        Minutes = Number(s.AutoSaveMinutes, 1, 60);
        AutoSave = misc.AddCheckField("Zapisuj plik napisów co", Minutes, s.AutoSaveEnabled, unit: "minut (1-60)");
        Copies = Number(s.BackupFileCount, 1, 99);
        var backupRow = misc.AddAdaptiveRow();
        _backup = backupRow.AddCheckBox("Twórz kopię zapasową pliku z napisami (*.bak)", s.CreateBackupFile);
        backupRow.AddField("Maks. liczba kopii:", Copies);
        NativeOptionList.BindEnabled(Copies, _backup);
        var font = page.AddSection("Czcionka edytora");
        _font = new OptionsFontCombo();
        _font.Items.AddRange(SystemFontCatalog.GetFamilyNames().Append(s.FontName).Distinct().Cast<object>().ToArray());
        _font.SelectedItem = s.FontName;
        var fontRow = font.AddAdaptiveRow();
        fontRow.AddField("Nazwa:", _font, 240);
        _fontSize = Number(s.FontSize, 6, 22);
        fontRow.AddField("Wielkość (6-22):", _fontSize);
        var highlight = page.AddSection("Podświetlanie kolorami");
        _invalid = highlight.AddCheckBox("Zaznaczaj czerwoną czcionką linie o błędnym formacie", s.HighlightInvalidFormat);
        _unsorted = highlight.AddCheckBox("Zaznaczaj czerwonym tłem nieposortowane linie", s.HighlightUnsortedLines);
        _short = highlight.AddCheckBox("Rozjaśniaj krótko wyświetlane linie", s.HighlightShortLines);
        _veryShort = highlight.AddCheckBox("Zaznaczaj na zielono bardzo krótko wyświetlane linie", s.HighlightVeryShortLines);
        var cursor = page.AddSection("Zachowanie kursora");
        _caret = cursor.AddCheckBox("Po włączeniu edytora napisów przenoś kursor na aktualnie wyświetlany napis", s.MoveCaretToCurrentSubtitleOnEditorShow);
        _fullscreenCaret = cursor.AddCheckBox("Wychodząc z trybu pełnoekranowego ustaw kursor na aktualnym napisie w edytorze", s.MoveCaretToCurrentSubtitleOnFullScreenExit);
        return page;
    }
    private SettingsPageLayout VideoPage()
    {
        var page = new SettingsPageLayout();
        var decoding = page.AddSection("Dekodowanie filmu");
        decoding.AddText("Ustawienie sposobu dekodowania filmu:");
        decoding.AddRadioOption("Automatyczne", "Używa dekodowania sprzętowego (H/W), jeśli jest dostępne.\r\nW przeciwnym razie automatycznie używa dekodowania programowego (S/W).", !_softwareInitial);
        Software = decoding.AddRadioOption("Programowe (S/W)", "Zawsze używa dekodowania programowego.", _softwareInitial);
        decoding.AddText("Zmiana sposobu dekodowania zostanie zastosowana po ponownym otwarciu pliku wideo.");
        return page;
    }
    public EditorSettings CreateEditorSettings() => new()
    {
        AutoSaveEnabled = AutoSave.Checked, AutoSaveMinutes = (int)Minutes.Value, CreateBackupFile = _backup.Checked, BackupFileCount = (int)Copies.Value,
        FontName = _font.Text, FontSize = (int)_fontSize.Value, HighlightInvalidFormat = _invalid.Checked,
        HighlightUnsortedLines = _unsorted.Checked, HighlightShortLines = _short.Checked, HighlightVeryShortLines = _veryShort.Checked,
        MoveCaretToCurrentSubtitleOnEditorShow = _caret.Checked, MoveCaretToCurrentSubtitleOnFullScreenExit = _fullscreenCaret.Checked
    };
    public EditingSettings CreateEditingSettings() => new()
    {
        ReactionSeconds = Read<double>("ReactionSeconds"),
        ApplyReactionToEndFrame = Read<bool>("ApplyReactionToEndFrame"),
        ApplyReactionWhenMeasuringSubtitleDelay = Read<bool>("ApplyReactionWhenMeasuringSubtitleDelay"),
        PreserveSubtitleDurationWhenInsertingStartFrame = Read<bool>("PreserveSubtitleDurationWhenInsertingStartFrame"),
        MoveCaretToNextLineAfterInsertingFrame = Read<bool>("MoveCaretToNextLineAfterInsertingFrame"),
        DefaultFiftyCharacterDisplaySeconds = Read<double>("DefaultFiftyCharacterDisplaySeconds"),
        AdditionalDefaultDisplaySeconds = Read<double>("AdditionalDefaultDisplaySeconds"),
    };
    public SubtitlePlaybackSettings CreateSubtitlePlaybackSettings() => new()
    {
        AdjustLineWidthToScreen = Read<bool>("AdjustLineWidthToScreen"),
        InterpretMicroDvdControlCodes = Read<bool>("InterpretMicroDvdControlCodes"),
        WarnOnUnsortedSubtitles = Read<bool>("WarnOnUnsortedSubtitles"),
        ItalicLinesStartingWithSlash = Read<bool>("ItalicLinesStartingWithSlash"),
        ItalicLinesStartingWithAsterisk = Read<bool>("ItalicLinesStartingWithAsterisk"),
        ItalicLinesStartingWithHash = Read<bool>("ItalicLinesStartingWithHash"),
        MinimumGapFrames = Read<int>("MinimumGapFrames"),
    };
    public string SelectedSubtitleFontName => Read<string>("SubtitleFontName");
    public int SelectedSubtitleFontSize => Read<int>("SubtitleFontSize");
    public bool SelectedSubtitleFontBold => Read<bool>("SubtitleFontBold");
    public int SelectedMinimumSubtitleFontSize => Read<int>("MinimumSubtitleFontSize");
    public int SelectedSubtitleBackgroundTransparencyPercent => Read<int>("SubtitleTransparency");
    public int SelectedSubtitleOutlineThickness => Read<int>("SubtitleOutline");
    public bool SelectedPositionSubtitlesRelativeToVideoFrame => Read<bool>("PositionSubtitlesRelativeToVideoFrame");
    public int SelectedSubtitleMarginLeft => Read<int>("SubtitleMarginLeft");
    public int SelectedSubtitleMarginRight => Read<int>("SubtitleMarginRight");
    public int SelectedSubtitleMarginTop => Read<int>("SubtitleMarginTop");
    public int SelectedSubtitleMarginBottom => Read<int>("SubtitleMarginBottom");
    public int SelectedUserSeekBackwardSeconds => Read<int>("UserSeekBackwardSeconds");
    public int SelectedUserSeekForwardSeconds => Read<int>("UserSeekForwardSeconds");
    public bool SelectedPauseOnMinimize => Read<bool>("PauseOnMinimize");
    public bool SelectedPauseWhenDialogDisplayed => Read<bool>("PauseWhenDialogDisplayed");
    public bool SelectedPauseOnDeactivate => Read<bool>("PauseOnDeactivate");
    public bool SelectedExitFullscreenWhenPlaybackEnds => Read<bool>("ExitFullscreenWhenPlaybackEnds");
    public Color SelectedSubtitleTextColor => Color.FromArgb(Read<int>("SubtitleTextColor"));
    public Color SelectedSubtitleBackgroundColor => Color.FromArgb(Read<int>("SubtitleBackgroundColor"));
    public Color SelectedSubtitleOutlineColor => Color.FromArgb(Read<int>("SubtitleOutlineColor"));
    public SubtitleFontSizeMode SelectedSubtitleFontSizeMode => Read<bool>("SubtitleRelative") ? SubtitleFontSizeMode.RelativeToWindow : SubtitleFontSizeMode.Constant;
    public SubtitleAlignment SelectedSubtitleAlignment => (SubtitleAlignment)(Read<int>("SubtitleAlignment") + 1);
    public VideoDecodingMode SelectedVideoDecodingMode => Software.Checked ? VideoDecodingMode.Software : VideoDecodingMode.Automatic;
    public InformationOsdSettings CreateInformationOsdSettings() => new()
    {
        Position = Read<bool>("OsdHidden") ? InformationOsdPosition.Hidden : InformationOsdPosition.TopRight,
        FontSize = Read<int>("OsdFontSize"), DisplayDurationSeconds = Read<double>("OsdDuration"), ShowInWindowedMode = Read<bool>("OsdWindowed"),
        TextColorArgb = Read<int>("OsdTextColor"), BackgroundColorArgb = Read<int>("OsdBackgroundColor"), BackgroundTransparencyPercent = Read<int>("OsdTransparency"),
        BorderColorArgb = Read<int>("OsdOutlineColor"), BorderThickness = Read<int>("OsdOutline")
    };
    protected override void Dispose(bool disposing)
    {
        if (disposing) _fit.Dispose();
        base.Dispose(disposing);
        if (disposing) _fonts.Dispose();
    }
}
