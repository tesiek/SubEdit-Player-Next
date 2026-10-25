using System.Runtime.InteropServices;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class EditorOptionsDialog : SubEditDialog
{
    private const int LogicalClientWidth = 650;

    private readonly CheckBox _autoSave;
    private readonly NumericUpDown _autoSaveMinutes;
    private readonly CheckBox _createBackup;
    private readonly NumericUpDown _backupCount;
    private readonly ComboBox _fontName;
    private readonly NumericUpDown _fontSize;
    private readonly CheckBox _highlightInvalidFormat;
    private readonly CheckBox _highlightUnsortedLines;
    private readonly CheckBox _highlightShortLines;
    private readonly CheckBox _highlightVeryShortLines;
    private readonly CheckBox _moveCaretToCurrentSubtitle;
    private readonly CheckBox _moveCaretToCurrentSubtitleOnFullScreenExit;
    private NumericUpDown _reactionSeconds = null!;
    private CheckBox _applyReactionToEnd = null!;
    private CheckBox _applyReactionWhenMeasuringDelay = null!;
    private CheckBox _preserveSubtitleDuration = null!;
    private CheckBox _moveCaretToNextLine = null!;
    private NumericUpDown _defaultFiftyCharacterDisplaySeconds = null!;
    private NumericUpDown _additionalDefaultDisplaySeconds = null!;
    private CheckBox _adjustLineWidthToScreen = null!;
    private CheckBox _interpretMicroDvdControlCodes = null!;
    private CheckBox _warnOnUnsortedSubtitles = null!;
    private CheckBox _italicLinesStartingWithSlash = null!;
    private CheckBox _italicLinesStartingWithAsterisk = null!;
    private CheckBox _italicLinesStartingWithHash = null!;
    private NumericUpDown _minimumSubtitleGapFrames = null!;
    private ComboBox _subtitleFontName = null!;
    private NumericUpDown _subtitleFontSize = null!;
    private CheckBox _subtitleFontBold = null!;
    private RadioButton _subtitleFontSizeConstant = null!;
    private RadioButton _subtitleFontSizeRelativeToWindow = null!;
    private NumericUpDown _minimumSubtitleFontSize = null!;
    private Color _subtitleTextColor;
    private Color _subtitleBackgroundColor;
    private NumericUpDown _subtitleBackgroundOpacityPercent = null!;
    private Color _subtitleOutlineColor;
    private NumericUpDown _subtitleOutlineThickness = null!;
    private Panel _subtitleAppearancePreview = null!;
    private readonly RadioButton[] _subtitleAlignmentButtons = new RadioButton[9];
    private NumericUpDown _subtitleMarginLeft = null!;
    private NumericUpDown _subtitleMarginRight = null!;
    private NumericUpDown _subtitleMarginTop = null!;
    private NumericUpDown _subtitleMarginBottom = null!;
    private CheckBox _positionSubtitlesRelativeToVideoFrame = null!;
    private NumericUpDown _userSeekBackwardSeconds = null!;
    private NumericUpDown _userSeekForwardSeconds = null!;
    private CheckBox _pauseOnMinimize = null!;
    private CheckBox _pauseWhenDialogDisplayed = null!;
    private CheckBox _pauseOnDeactivate = null!;
    private CheckBox _exitFullscreenWhenPlaybackEnds = null!;
    private RadioButton _videoDecodingAutoRadio = null!;
    private RadioButton _videoDecodingSoftwareRadio = null!;
    private RadioButton _informationOsdTopRight = null!;
    private RadioButton _informationOsdHidden = null!;
    private NumericUpDown _informationOsdFontSize = null!;
    private NumericUpDown _informationOsdDurationSeconds = null!;
    private CheckBox _showInformationOsdInWindowedMode = null!;
    private Color _informationOsdTextColor;
    private Color _informationOsdBackgroundColor;
    private NumericUpDown _informationOsdBackgroundOpacityPercent = null!;
    private Color _informationOsdBorderColor;
    private NumericUpDown _informationOsdBorderThickness = null!;
    private Panel _informationOsdAppearancePreview = null!;
    private readonly TabControl _tabs;
    private readonly FlowLayoutPanel _dialogButtons;
    private readonly TableLayoutPanel _rootLayout;

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
        int subtitleBackgroundOpacityPercent,
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

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Opcje";
        ClientSize = new Size(650, 640);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            DrawMode = TabDrawMode.OwnerDrawFixed
        };
        _tabs.DrawItem += DrawOptionsTab;
        var tabs = _tabs;
        var editorTab = new TabPage("Edytor") { Padding = new Padding(12) };
        tabs.TabPages.Add(editorTab);
        tabs.TabPages.Add(CreateEditingTab(editingSettings));
        tabs.TabPages.Add(CreateSubtitlesTab(subtitlePlaybackSettings));
        tabs.TabPages.Add(CreateSubtitleAppearanceTab(
            subtitleFontName,
            subtitleFontSize,
            subtitleFontBold,
            subtitleFontSizeMode,
            minimumSubtitleFontSize,
            subtitleTextColorArgb,
            subtitleBackgroundColorArgb,
            subtitleBackgroundOpacityPercent,
            subtitleOutlineColorArgb,
            subtitleOutlineThickness,
            subtitleAlignment,
            subtitleMarginLeft,
            subtitleMarginRight,
            subtitleMarginTop,
            subtitleMarginBottom,
            positionSubtitlesRelativeToVideoFrame));
        tabs.TabPages.Add(CreateMovieTab(playbackSettings));
        tabs.TabPages.Add(CreateAppearanceTab(informationOsdSettings));
        tabs.TabPages.Add(CreateVideoTab(playbackSettings));
        tabs.SelectedIndex = Math.Clamp(selectedTabIndex, 0, tabs.TabPages.Count - 1);

        var miscellaneous = new GroupBox
        {
            Text = "Różne",
            Location = new Point(12, 12),
            Size = new Size(578, 88)
        };
        _autoSave = new CheckBox
        {
            Text = "Zapisuj plik napisów co",
            Location = new Point(12, 24),
            AutoSize = true,
            Checked = settings.AutoSaveEnabled
        };
        _autoSaveMinutes = CreateNumber(settings.AutoSaveMinutes, 1, 60, new Point(492, 21));
        var minutesLabel = CreateTrailingNumericLabel("minut (1-60)", 21);
        _createBackup = new CheckBox
        {
            Text = "Twórz kopię zapasową pliku z napisami (*.bak)",
            Location = new Point(12, 51),
            AutoSize = true,
            Checked = settings.CreateBackupFile
        };
        var backupLabel = CreateTrailingNumericLabel("Maks. liczba kopii:", 51);
        _backupCount = CreateNumber(settings.BackupFileCount, 1, 99, new Point(492, 51));
        _backupCount.TextChanged += (_, _) =>
        {
            if (decimal.TryParse(_backupCount.Text, out var value) && value > _backupCount.Maximum)
            {
                _backupCount.Value = _backupCount.Maximum;
            }
        };
        _autoSave.CheckedChanged += (_, _) => _autoSaveMinutes.Enabled = _autoSave.Checked;
        _createBackup.CheckedChanged += (_, _) => _backupCount.Enabled = _createBackup.Checked;
        _autoSaveMinutes.Enabled = _autoSave.Checked;
        _backupCount.Enabled = _createBackup.Checked;
        miscellaneous.Controls.AddRange([_autoSave, _autoSaveMinutes, minutesLabel, _createBackup, backupLabel, _backupCount]);

        var font = new GroupBox
        {
            Text = "Czcionka edytora",
            Location = new Point(12, 111),
            Size = new Size(578, 74)
        };
        var fontLabel = new Label { Text = "Nazwa:", Location = new Point(12, 31), AutoSize = true };
        _fontName = new ComboBox
        {
            Location = new Point(66, 27),
            Size = new Size(290, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Sorted = true
        };
        foreach (var familyName in SystemFontCatalog.GetFamilyNames())
        {
            _fontName.Items.Add(familyName);
        }
        if (!_fontName.Items.Contains(settings.FontName))
        {
            _fontName.Items.Add(settings.FontName);
        }
        _fontName.SelectedItem = settings.FontName;
        var fontSizeLabel = CreateTrailingNumericLabel("Wielkość (6-22):", 27);
        _fontSize = CreateNumber(settings.FontSize, 6, 22, new Point(492, 27));
        font.Controls.AddRange([fontLabel, _fontName, fontSizeLabel, _fontSize]);

        var highlighting = new GroupBox
        {
            Text = "Podświetlanie kolorami",
            Location = new Point(12, 196),
            Size = new Size(578, 123)
        };
        _highlightInvalidFormat = CreateCheckBox("Zaznaczaj czerwoną czcionką linie o błędnym formacie", 23, settings.HighlightInvalidFormat);
        _highlightUnsortedLines = CreateCheckBox("Zaznaczaj czerwonym tłem nieposortowane linie", 47, settings.HighlightUnsortedLines);
        _highlightShortLines = CreateCheckBox("Rozjaśniaj krótko wyświetlane linie", 71, settings.HighlightShortLines);
        _highlightVeryShortLines = CreateCheckBox("Zaznaczaj na zielono bardzo krótko wyświetlane linie", 95, settings.HighlightVeryShortLines);
        highlighting.Controls.AddRange([_highlightInvalidFormat, _highlightUnsortedLines, _highlightShortLines, _highlightVeryShortLines]);

        var cursor = new GroupBox
        {
            Text = "Kursor",
            Location = new Point(12, 330),
            Size = new Size(578, 74)
        };
        _moveCaretToCurrentSubtitle = new CheckBox
        {
            Text = "Po włączeniu edytora napisów przenoś kursor na aktualnie wyświetlany napis",
            Location = new Point(12, 20),
            AutoSize = true,
            Checked = settings.MoveCaretToCurrentSubtitleOnEditorShow
        };
        _moveCaretToCurrentSubtitleOnFullScreenExit = new CheckBox
        {
            Text = "Wychodząc z trybu pełnoekranowego ustaw kursor na aktualnym napisie w edytorze",
            Location = new Point(12, 44),
            AutoSize = true,
            Checked = settings.MoveCaretToCurrentSubtitleOnFullScreenExit
        };
        cursor.Controls.AddRange([_moveCaretToCurrentSubtitle, _moveCaretToCurrentSubtitleOnFullScreenExit]);
        ArrangeEditorTab(
            editorTab,
            miscellaneous,
            minutesLabel,
            backupLabel,
            font,
            fontLabel,
            fontSizeLabel,
            highlighting,
            cursor);

        var apply = CreateDialogButton("Zastosuj");
        var ok = CreateDialogButton("OK");
        ok.DialogResult = DialogResult.OK;
        var cancel = CreateDialogButton("Anuluj");
        cancel.DialogResult = DialogResult.Cancel;
        apply.Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
        ok.Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);

        _dialogButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = new Padding(0, 8, 0, 0),
            Padding = Padding.Empty,
            WrapContents = false
        };
        var buttons = _dialogButtons;
        buttons.Controls.AddRange([cancel, ok, apply]);

        _rootLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        var root = _rootLayout;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(tabs, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        var desiredClientWidth = LogicalToDeviceUnits(LogicalClientWidth);
        var workingArea = Screen.GetWorkingArea(this);
        var outerMargin = LogicalToDeviceUnits(12);
        var nonClientWidth = Width - ClientSize.Width;
        var nonClientHeight = Height - ClientSize.Height;
        var availableClientWidth = Math.Max(1, workingArea.Width - nonClientWidth - (2 * outerMargin));
        var availableClientHeight = Math.Max(1, workingArea.Height - nonClientHeight - (2 * outerMargin));

        ClientSize = new Size(
            Math.Min(desiredClientWidth, availableClientWidth),
            availableClientHeight);
        PerformLayout();

        ClientSize = new Size(
            ClientSize.Width,
            Math.Min(CalculatePreferredClientHeight(), availableClientHeight));
    }

    private int CalculatePreferredClientHeight()
    {
        _rootLayout.PerformLayout();
        _tabs.PerformLayout();

        var selectedPageHeight = _tabs.SelectedTab?.ClientSize.Height ?? 0;
        var tabChromeHeight = Math.Max(0, _tabs.Height - selectedPageHeight);
        var maximumPageHeight = 0;

        foreach (TabPage page in _tabs.TabPages)
        {
            page.PerformLayout();
            var contentBottom = page.Padding.Top;
            foreach (Control control in page.Controls)
            {
                control.PerformLayout();
                contentBottom = Math.Max(contentBottom, control.Bottom);
            }

            maximumPageHeight = Math.Max(maximumPageHeight, contentBottom + page.Padding.Bottom);
        }

        var buttonsHeight = _dialogButtons.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
        return _rootLayout.Padding.Vertical
            + tabChromeHeight
            + maximumPageHeight
            + _dialogButtons.Margin.Vertical
            + buttonsHeight;
    }

    public event EventHandler? ApplyRequested;

    public EditorSettings CreateEditorSettings() => new()
    {
        AutoSaveEnabled = _autoSave.Checked,
        AutoSaveMinutes = (int)_autoSaveMinutes.Value,
        CreateBackupFile = _createBackup.Checked,
        BackupFileCount = (int)_backupCount.Value,
        FontName = _fontName.SelectedItem?.ToString() ?? "Courier New",
        FontSize = (int)_fontSize.Value,
        HighlightInvalidFormat = _highlightInvalidFormat.Checked,
        HighlightUnsortedLines = _highlightUnsortedLines.Checked,
        HighlightShortLines = _highlightShortLines.Checked,
        HighlightVeryShortLines = _highlightVeryShortLines.Checked,
        MoveCaretToCurrentSubtitleOnEditorShow = _moveCaretToCurrentSubtitle.Checked,
        MoveCaretToCurrentSubtitleOnFullScreenExit = _moveCaretToCurrentSubtitleOnFullScreenExit.Checked
    };

    public EditingSettings CreateEditingSettings() => new()
    {
        ReactionSeconds = (double)_reactionSeconds.Value,
        ApplyReactionToEndFrame = _applyReactionToEnd.Checked,
        ApplyReactionWhenMeasuringSubtitleDelay = _applyReactionWhenMeasuringDelay.Checked,
        PreserveSubtitleDurationWhenInsertingStartFrame = _preserveSubtitleDuration.Checked,
        MoveCaretToNextLineAfterInsertingFrame = _moveCaretToNextLine.Checked,
        DefaultFiftyCharacterDisplaySeconds = (double)_defaultFiftyCharacterDisplaySeconds.Value,
        AdditionalDefaultDisplaySeconds = (double)_additionalDefaultDisplaySeconds.Value
    };

    public SubtitlePlaybackSettings CreateSubtitlePlaybackSettings() => new()
    {
        AdjustLineWidthToScreen = _adjustLineWidthToScreen.Checked,
        InterpretMicroDvdControlCodes = _interpretMicroDvdControlCodes.Checked,
        WarnOnUnsortedSubtitles = _warnOnUnsortedSubtitles.Checked,
        ItalicLinesStartingWithSlash = _italicLinesStartingWithSlash.Checked,
        ItalicLinesStartingWithAsterisk = _italicLinesStartingWithAsterisk.Checked,
        ItalicLinesStartingWithHash = _italicLinesStartingWithHash.Checked,
        MinimumGapFrames = (int)_minimumSubtitleGapFrames.Value
    };

    public string SelectedSubtitleFontName => _subtitleFontName.SelectedItem?.ToString() ?? "Arial";

    public int SelectedSubtitleFontSize => (int)_subtitleFontSize.Value;

    public bool SelectedSubtitleFontBold => _subtitleFontBold.Checked;

    public SubtitleFontSizeMode SelectedSubtitleFontSizeMode => _subtitleFontSizeConstant.Checked
        ? SubtitleFontSizeMode.Constant
        : SubtitleFontSizeMode.RelativeToWindow;

    public int SelectedMinimumSubtitleFontSize => (int)_minimumSubtitleFontSize.Value;

    public Color SelectedSubtitleTextColor => _subtitleTextColor;

    public Color SelectedSubtitleBackgroundColor => _subtitleBackgroundColor;

    public int SelectedSubtitleBackgroundOpacityPercent => (int)_subtitleBackgroundOpacityPercent.Value;

    public Color SelectedSubtitleOutlineColor => _subtitleOutlineColor;

    public int SelectedSubtitleOutlineThickness => (int)_subtitleOutlineThickness.Value;

    public SubtitleAlignment SelectedSubtitleAlignment
    {
        get
        {
            var selectedIndex = Array.FindIndex(_subtitleAlignmentButtons, button => button.Checked);
            return selectedIndex >= 0 ? (SubtitleAlignment)(selectedIndex + 1) : SubtitleAlignment.BottomCenter;
        }
    }

    public int SelectedSubtitleMarginLeft => (int)_subtitleMarginLeft.Value;

    public int SelectedSubtitleMarginRight => (int)_subtitleMarginRight.Value;

    public int SelectedSubtitleMarginTop => (int)_subtitleMarginTop.Value;

    public int SelectedSubtitleMarginBottom => (int)_subtitleMarginBottom.Value;

    public bool SelectedPositionSubtitlesRelativeToVideoFrame => _positionSubtitlesRelativeToVideoFrame.Checked;

    public int SelectedUserSeekBackwardSeconds => (int)_userSeekBackwardSeconds.Value;

    public int SelectedUserSeekForwardSeconds => (int)_userSeekForwardSeconds.Value;

    public bool SelectedPauseOnMinimize => _pauseOnMinimize.Checked;

    public bool SelectedPauseWhenDialogDisplayed => _pauseWhenDialogDisplayed.Checked;

    public bool SelectedPauseOnDeactivate => _pauseOnDeactivate.Checked;

    public bool SelectedExitFullscreenWhenPlaybackEnds => _exitFullscreenWhenPlaybackEnds.Checked;

    public VideoDecodingMode SelectedVideoDecodingMode =>
        _videoDecodingSoftwareRadio.Checked
            ? VideoDecodingMode.Software
            : VideoDecodingMode.Automatic;

    public InformationOsdSettings CreateInformationOsdSettings() => new()
    {
        Position = _informationOsdHidden.Checked
            ? InformationOsdPosition.Hidden
            : InformationOsdPosition.TopRight,
        FontSize = (int)_informationOsdFontSize.Value,
        DisplayDurationSeconds = (double)_informationOsdDurationSeconds.Value,
        ShowInWindowedMode = _showInformationOsdInWindowedMode.Checked,
        TextColorArgb = _informationOsdTextColor.ToArgb(),
        BackgroundColorArgb = _informationOsdBackgroundColor.ToArgb(),
        BackgroundOpacityPercent = (int)_informationOsdBackgroundOpacityPercent.Value,
        BorderColorArgb = _informationOsdBorderColor.ToArgb(),
        BorderThickness = (int)_informationOsdBorderThickness.Value
    };

    public int SelectedTabIndex => _tabs.SelectedIndex;

    private void DrawOptionsTab(object? sender, DrawItemEventArgs eventArgs)
    {
        var page = _tabs.TabPages[eventArgs.Index];
        var state = eventArgs.State.HasFlag(DrawItemState.Selected)
            ? System.Windows.Forms.VisualStyles.TabItemState.Selected
            : eventArgs.State.HasFlag(DrawItemState.HotLight)
                ? System.Windows.Forms.VisualStyles.TabItemState.Hot
                : System.Windows.Forms.VisualStyles.TabItemState.Normal;
        TabRenderer.DrawTabItem(eventArgs.Graphics, eventArgs.Bounds, state);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            page.Text,
            _tabs.Font,
            eventArgs.Bounds,
            page.Enabled ? SystemColors.ControlText : SystemColors.GrayText,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding);
    }

    private void ArrangeEditorTab(
        TabPage tab,
        GroupBox miscellaneous,
        Label minutesLabel,
        Label backupLabel,
        GroupBox font,
        Label fontLabel,
        Label fontSizeLabel,
        GroupBox highlighting,
        GroupBox cursor)
    {
        var miscellaneousLayout = CreateGroupLayout(
            miscellaneous,
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(miscellaneousLayout);
        AddAutoRow(miscellaneousLayout);
        PrepareLeftAligned(_autoSave);
        PrepareRightAligned(minutesLabel);
        PrepareRightAligned(_autoSaveMinutes);
        PrepareLeftAligned(_createBackup);
        PrepareRightAligned(backupLabel);
        PrepareRightAligned(_backupCount);
        miscellaneousLayout.Controls.Add(_autoSave, 0, 0);
        miscellaneousLayout.Controls.Add(minutesLabel, 1, 0);
        miscellaneousLayout.Controls.Add(_autoSaveMinutes, 2, 0);
        miscellaneousLayout.Controls.Add(_createBackup, 0, 1);
        miscellaneousLayout.Controls.Add(backupLabel, 1, 1);
        miscellaneousLayout.Controls.Add(_backupCount, 2, 1);

        var fontLayout = CreateGroupLayout(
            font,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 67F),
            new ColumnStyle(SizeType.Percent, 33F),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(fontLayout);
        PrepareLeftAligned(fontLabel);
        PrepareFill(_fontName);
        PrepareRightAligned(fontSizeLabel);
        PrepareRightAligned(_fontSize);
        fontLayout.Controls.Add(fontLabel, 0, 0);
        fontLayout.Controls.Add(_fontName, 1, 0);
        fontLayout.Controls.Add(fontSizeLabel, 3, 0);
        fontLayout.Controls.Add(_fontSize, 4, 0);

        var highlightingLayout = CreateSingleColumnGroupLayout(highlighting);
        AddFullWidthRows(
            highlightingLayout,
            _highlightInvalidFormat,
            _highlightUnsortedLines,
            _highlightShortLines,
            _highlightVeryShortLines);

        var cursorLayout = CreateSingleColumnGroupLayout(cursor);
        AddFullWidthRows(
            cursorLayout,
            _moveCaretToCurrentSubtitle,
            _moveCaretToCurrentSubtitleOnFullScreenExit);

        ArrangeTabSections(tab, miscellaneous, font, highlighting, cursor);
    }

    private void ArrangeSubtitleAppearanceTab(
        TabPage tab,
        GroupBox font,
        Label fontNameLabel,
        Label fontSizeLabel,
        GroupBox size,
        Label minimumLabel,
        GroupBox colors,
        Button textColorButton,
        Button backgroundColorButton,
        Label backgroundOpacityLabel,
        Button outlineColorButton,
        Label outlineThicknessLabel,
        GroupBox placement)
    {
        var fontLayout = CreateGroupLayout(
            font,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 67F),
            new ColumnStyle(SizeType.Percent, 33F),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(fontLayout);
        AddAutoRow(fontLayout);
        PrepareLeftAligned(fontNameLabel);
        PrepareFill(_subtitleFontName);
        PrepareRightAligned(fontSizeLabel);
        PrepareRightAligned(_subtitleFontSize);
        PrepareLeftAligned(_subtitleFontBold);
        fontLayout.Controls.Add(fontNameLabel, 0, 0);
        fontLayout.Controls.Add(_subtitleFontName, 1, 0);
        fontLayout.Controls.Add(fontSizeLabel, 3, 0);
        fontLayout.Controls.Add(_subtitleFontSize, 4, 0);
        fontLayout.Controls.Add(_subtitleFontBold, 1, 1);
        fontLayout.SetColumnSpan(_subtitleFontBold, 4);

        var sizeLayout = CreateGroupLayout(
            size,
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(sizeLayout);
        AddAutoRow(sizeLayout);
        AddAutoRow(sizeLayout);
        PrepareLeftAligned(_subtitleFontSizeConstant);
        PrepareLeftAligned(_subtitleFontSizeRelativeToWindow);
        PrepareLeftAligned(minimumLabel);
        PrepareRightAligned(_minimumSubtitleFontSize);
        sizeLayout.Controls.Add(_subtitleFontSizeConstant, 0, 0);
        sizeLayout.SetColumnSpan(_subtitleFontSizeConstant, 2);
        sizeLayout.Controls.Add(_subtitleFontSizeRelativeToWindow, 0, 1);
        sizeLayout.SetColumnSpan(_subtitleFontSizeRelativeToWindow, 2);
        sizeLayout.Controls.Add(minimumLabel, 0, 2);
        sizeLayout.Controls.Add(_minimumSubtitleFontSize, 1, 2);

        ArrangeColorGroup(
            colors,
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            _subtitleBackgroundOpacityPercent,
            outlineColorButton,
            outlineThicknessLabel,
            _subtitleOutlineThickness,
            _subtitleAppearancePreview);

        placement.Controls.Clear();
        var placementLayout = CreateGroupLayout(
            placement,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 100F));
        AddAutoRow(placementLayout);
        AddAutoRow(placementLayout);

        var alignmentLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 3,
            Margin = new Padding(3),
            Anchor = AnchorStyles.Left
        };
        for (var column = 0; column < 3; column++)
        {
            alignmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        for (var row = 0; row < 3; row++)
        {
            alignmentLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        for (var index = 0; index < _subtitleAlignmentButtons.Length; index++)
        {
            var button = _subtitleAlignmentButtons[index];
            button.Anchor = AnchorStyles.None;
            button.Margin = new Padding(3);
            alignmentLayout.Controls.Add(button, index % 3, index / 3);
        }

        var marginsLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 2,
            Dock = DockStyle.Left,
            Margin = new Padding(12, 3, 3, 3)
        };
        marginsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        marginsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        marginsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        marginsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        marginsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        marginsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        AddMarginPair(marginsLayout, "Lewo", _subtitleMarginLeft, "Góra", _subtitleMarginTop, 0);
        AddMarginPair(marginsLayout, "Prawo", _subtitleMarginRight, "Dół", _subtitleMarginBottom, 1);

        PrepareLeftAligned(_positionSubtitlesRelativeToVideoFrame);
        placementLayout.Controls.Add(alignmentLayout, 0, 0);
        placementLayout.Controls.Add(marginsLayout, 1, 0);
        placementLayout.Controls.Add(_positionSubtitlesRelativeToVideoFrame, 0, 1);
        placementLayout.SetColumnSpan(_positionSubtitlesRelativeToVideoFrame, 2);

        ArrangeTabSections(tab, font, size, colors, placement);
    }

    private void ArrangeEditingTab(
        TabPage tab,
        GroupBox reaction,
        Label reactionLabel,
        GroupBox frameInsertion,
        GroupBox duration,
        Label defaultDurationLabel,
        Label additionalDurationLabel)
    {
        var reactionLayout = CreateGroupLayout(
            reaction,
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(reactionLayout);
        AddAutoRow(reactionLayout);
        AddAutoRow(reactionLayout);
        PrepareLeftAligned(reactionLabel);
        PrepareRightAligned(_reactionSeconds);
        PrepareLeftAligned(_applyReactionToEnd);
        PrepareLeftAligned(_applyReactionWhenMeasuringDelay);
        reactionLayout.Controls.Add(reactionLabel, 0, 0);
        reactionLayout.Controls.Add(_reactionSeconds, 1, 0);
        reactionLayout.Controls.Add(_applyReactionToEnd, 0, 1);
        reactionLayout.SetColumnSpan(_applyReactionToEnd, 2);
        reactionLayout.Controls.Add(_applyReactionWhenMeasuringDelay, 0, 2);
        reactionLayout.SetColumnSpan(_applyReactionWhenMeasuringDelay, 2);

        var frameInsertionLayout = CreateSingleColumnGroupLayout(frameInsertion);
        AddFullWidthRows(frameInsertionLayout, _preserveSubtitleDuration, _moveCaretToNextLine);

        var durationLayout = CreateGroupLayout(
            duration,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(durationLayout);
        PrepareRightAligned(defaultDurationLabel);
        PrepareRightAligned(_defaultFiftyCharacterDisplaySeconds);
        PrepareRightAligned(additionalDurationLabel);
        PrepareRightAligned(_additionalDefaultDisplaySeconds);
        durationLayout.Controls.Add(defaultDurationLabel, 0, 0);
        durationLayout.Controls.Add(_defaultFiftyCharacterDisplaySeconds, 1, 0);
        durationLayout.Controls.Add(additionalDurationLabel, 2, 0);
        durationLayout.Controls.Add(_additionalDefaultDisplaySeconds, 3, 0);

        ArrangeTabSections(tab, reaction, frameInsertion, duration);
    }

    private void ArrangeSubtitlesTab(TabPage tab, GroupBox display, Label italicLabel, Label gapLabel)
    {
        var displayLayout = CreateGroupLayout(
            display,
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize));
        for (var row = 0; row < 5; row++)
        {
            AddAutoRow(displayLayout);
        }

        PrepareLeftAligned(_adjustLineWidthToScreen);
        PrepareLeftAligned(_interpretMicroDvdControlCodes);
        PrepareLeftAligned(_warnOnUnsortedSubtitles);
        PrepareLeftAligned(italicLabel);
        PrepareLeftAligned(_italicLinesStartingWithSlash);
        PrepareLeftAligned(_italicLinesStartingWithAsterisk);
        PrepareLeftAligned(_italicLinesStartingWithHash);
        PrepareLeftAligned(gapLabel);
        PrepareRightAligned(_minimumSubtitleGapFrames);

        displayLayout.Controls.Add(_adjustLineWidthToScreen, 0, 0);
        displayLayout.SetColumnSpan(_adjustLineWidthToScreen, 5);
        displayLayout.Controls.Add(_interpretMicroDvdControlCodes, 0, 1);
        displayLayout.SetColumnSpan(_interpretMicroDvdControlCodes, 5);
        displayLayout.Controls.Add(_warnOnUnsortedSubtitles, 0, 2);
        displayLayout.SetColumnSpan(_warnOnUnsortedSubtitles, 5);
        displayLayout.Controls.Add(italicLabel, 0, 3);
        displayLayout.Controls.Add(_italicLinesStartingWithSlash, 1, 3);
        displayLayout.Controls.Add(_italicLinesStartingWithAsterisk, 2, 3);
        displayLayout.Controls.Add(_italicLinesStartingWithHash, 3, 3);
        displayLayout.Controls.Add(gapLabel, 0, 4);
        displayLayout.SetColumnSpan(gapLabel, 4);
        displayLayout.Controls.Add(_minimumSubtitleGapFrames, 4, 4);

        ArrangeTabSections(tab, display);
    }

    private void ArrangeMovieTab(
        TabPage tab,
        GroupBox userSeek,
        Label backwardLabel,
        Label forwardLabel,
        GroupBox pause,
        GroupBox fullScreen)
    {
        var seekLayout = CreateGroupLayout(
            userSeek,
            new ColumnStyle(SizeType.Percent, 100F),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(seekLayout);
        AddAutoRow(seekLayout);
        PrepareRightAligned(backwardLabel);
        PrepareRightAligned(_userSeekBackwardSeconds);
        PrepareRightAligned(forwardLabel);
        PrepareRightAligned(_userSeekForwardSeconds);
        seekLayout.Controls.Add(backwardLabel, 0, 0);
        seekLayout.Controls.Add(_userSeekBackwardSeconds, 1, 0);
        seekLayout.Controls.Add(forwardLabel, 0, 1);
        seekLayout.Controls.Add(_userSeekForwardSeconds, 1, 1);

        var pauseLayout = CreateSingleColumnGroupLayout(pause);
        AddFullWidthRows(pauseLayout, _pauseOnMinimize, _pauseWhenDialogDisplayed, _pauseOnDeactivate);

        var fullScreenLayout = CreateSingleColumnGroupLayout(fullScreen);
        AddFullWidthRows(fullScreenLayout, _exitFullscreenWhenPlaybackEnds);

        ArrangeTabSections(tab, userSeek, pause, fullScreen);
    }

    private void ArrangeAppearanceTab(
        TabPage tab,
        GroupBox informationOsd,
        Label osdFontSizeLabel,
        Label osdDurationLabel,
        GroupBox osdColors,
        Button textColorButton,
        Button backgroundColorButton,
        Label backgroundOpacityLabel,
        Button borderColorButton,
        Label borderThicknessLabel)
    {
        var osdLayout = CreateGroupLayout(
            informationOsd,
            new ColumnStyle(SizeType.Percent, 35F),
            new ColumnStyle(SizeType.Percent, 65F),
            new ColumnStyle(SizeType.AutoSize));
        AddAutoRow(osdLayout);
        AddAutoRow(osdLayout);
        AddAutoRow(osdLayout);
        PrepareLeftAligned(_informationOsdTopRight);
        PrepareLeftAligned(_informationOsdHidden);
        PrepareRightAligned(osdFontSizeLabel);
        PrepareRightAligned(_informationOsdFontSize);
        PrepareRightAligned(osdDurationLabel);
        PrepareRightAligned(_informationOsdDurationSeconds);
        PrepareLeftAligned(_showInformationOsdInWindowedMode);
        osdLayout.Controls.Add(_informationOsdTopRight, 0, 0);
        osdLayout.Controls.Add(osdFontSizeLabel, 1, 0);
        osdLayout.Controls.Add(_informationOsdFontSize, 2, 0);
        osdLayout.Controls.Add(_informationOsdHidden, 0, 1);
        osdLayout.Controls.Add(osdDurationLabel, 1, 1);
        osdLayout.Controls.Add(_informationOsdDurationSeconds, 2, 1);
        osdLayout.Controls.Add(_showInformationOsdInWindowedMode, 0, 2);
        osdLayout.SetColumnSpan(_showInformationOsdInWindowedMode, 3);

        ArrangeColorGroup(
            osdColors,
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            _informationOsdBackgroundOpacityPercent,
            borderColorButton,
            borderThicknessLabel,
            _informationOsdBorderThickness,
            _informationOsdAppearancePreview);

        ArrangeTabSections(tab, informationOsd, osdColors);
    }

    private static void ArrangeColorGroup(
        GroupBox group,
        Button textColorButton,
        Button backgroundColorButton,
        Label backgroundOpacityLabel,
        NumericUpDown backgroundOpacity,
        Button borderColorButton,
        Label borderThicknessLabel,
        NumericUpDown borderThickness,
        Panel preview)
    {
        var layout = CreateGroupLayout(
            group,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 5; row++)
        {
            AddAutoRow(layout);
        }

        PrepareColorButton(textColorButton);
        PrepareColorButton(backgroundColorButton);
        PrepareRightAligned(backgroundOpacityLabel);
        PrepareRightAligned(backgroundOpacity);
        PrepareColorButton(borderColorButton);
        PrepareRightAligned(borderThicknessLabel);
        PrepareRightAligned(borderThickness);
        preview.Dock = DockStyle.Fill;
        preview.Margin = new Padding(12, 3, 3, 3);
        preview.MinimumSize = new Size(300, 138);

        layout.Controls.Add(textColorButton, 0, 0);
        layout.SetColumnSpan(textColorButton, 2);
        layout.Controls.Add(backgroundColorButton, 0, 1);
        layout.SetColumnSpan(backgroundColorButton, 2);
        layout.Controls.Add(backgroundOpacityLabel, 0, 2);
        layout.Controls.Add(backgroundOpacity, 1, 2);
        layout.Controls.Add(borderColorButton, 0, 3);
        layout.SetColumnSpan(borderColorButton, 2);
        layout.Controls.Add(borderThicknessLabel, 0, 4);
        layout.Controls.Add(borderThickness, 1, 4);
        layout.Controls.Add(preview, 2, 0);
        layout.SetRowSpan(preview, 5);
    }

    private static void AddMarginPair(
        TableLayoutPanel layout,
        string firstText,
        NumericUpDown firstValue,
        string secondText,
        NumericUpDown secondValue,
        int row)
    {
        var firstLabel = new Label { Text = firstText, AutoSize = true };
        var secondLabel = new Label { Text = secondText, AutoSize = true };
        PrepareRightAligned(firstLabel);
        PrepareRightAligned(firstValue);
        PrepareRightAligned(secondLabel);
        PrepareRightAligned(secondValue);
        layout.Controls.Add(firstLabel, 0, row);
        layout.Controls.Add(firstValue, 1, row);
        layout.Controls.Add(secondLabel, 2, row);
        layout.Controls.Add(secondValue, 3, row);
    }

    private static void ArrangeTabSections(TabPage tab, params GroupBox[] sections)
    {
        tab.AutoScroll = true;
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = sections.Length,
            Dock = DockStyle.Top,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < sections.Length; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var section = sections[row];
            section.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            section.Margin = new Padding(0, 0, 0, row == sections.Length - 1 ? 0 : 8);
            layout.Controls.Add(section, 0, row);
        }

        tab.Controls.Add(layout);
    }

    private static TableLayoutPanel CreateSingleColumnGroupLayout(GroupBox group) =>
        CreateGroupLayout(group, new ColumnStyle(SizeType.Percent, 100F));

    private static TableLayoutPanel CreateGroupLayout(GroupBox group, params ColumnStyle[] columnStyles)
    {
        group.MinimumSize = new Size(0, group.Height);
        group.AutoSize = true;
        group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        group.Padding = new Padding(9, 18, 9, 7);
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columnStyles.Length,
            RowCount = 0,
            Dock = DockStyle.Top,
            GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        foreach (var columnStyle in columnStyles)
        {
            layout.ColumnStyles.Add(columnStyle);
        }

        group.Controls.Add(layout);
        return layout;
    }

    private static void AddFullWidthRows(TableLayoutPanel layout, params Control[] controls)
    {
        foreach (var control in controls)
        {
            var row = layout.RowCount;
            AddAutoRow(layout);
            PrepareLeftAligned(control);
            layout.Controls.Add(control, 0, row);
        }
    }

    private static void AddAutoRow(TableLayoutPanel layout)
    {
        layout.RowCount++;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private static void PrepareLeftAligned(Control control)
    {
        control.Anchor = AnchorStyles.Left;
        control.Margin = new Padding(3);
        if (control is Label label)
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
        }
    }

    private static void PrepareRightAligned(Control control)
    {
        control.Anchor = AnchorStyles.Right;
        control.Margin = new Padding(3);
        if (control is Label label)
        {
            label.AutoSize = true;
            label.TextAlign = ContentAlignment.MiddleRight;
        }
    }

    private static void PrepareFill(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3);
    }

    private static void PrepareColorButton(Button button)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(3);
        button.MinimumSize = new Size(190, 0);
    }

    private static Button CreateDialogButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(75, 27),
        Margin = new Padding(6, 0, 0, 0)
    };

    private TabPage CreateSubtitleAppearanceTab(
        string subtitleFontName,
        int subtitleFontSize,
        bool subtitleFontBold,
        SubtitleFontSizeMode subtitleFontSizeMode,
        int minimumSubtitleFontSize,
        int subtitleTextColorArgb,
        int subtitleBackgroundColorArgb,
        int subtitleBackgroundOpacityPercent,
        int subtitleOutlineColorArgb,
        int subtitleOutlineThickness,
        SubtitleAlignment subtitleAlignment,
        int subtitleMarginLeft,
        int subtitleMarginRight,
        int subtitleMarginTop,
        int subtitleMarginBottom,
        bool positionSubtitlesRelativeToVideoFrame)
    {
        var tab = new TabPage("Napisy - wygląd") { Padding = new Padding(12) };
        var font = new GroupBox
        {
            Text = "Czcionka napisów",
            Location = new Point(12, 12),
            Size = new Size(578, 80)
        };
        var fontNameLabel = new Label { Text = "Nazwa:", Location = new Point(12, 30), AutoSize = true };
        _subtitleFontName = new ComboBox
        {
            Location = new Point(66, 26),
            Size = new Size(278, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Sorted = true
        };
        foreach (var familyName in SystemFontCatalog.GetFamilyNames())
        {
            _subtitleFontName.Items.Add(familyName);
        }
        if (!_subtitleFontName.Items.Contains(subtitleFontName))
        {
            _subtitleFontName.Items.Add(subtitleFontName);
        }
        _subtitleFontName.SelectedItem = subtitleFontName;
        var fontSizeLabel = new Label { Text = "Wielkość (8-72):", Location = new Point(362, 30), AutoSize = true };
        _subtitleFontSize = CreateNumber(subtitleFontSize, 8, 72, new Point(478, 26));
        _subtitleFontBold = new CheckBox
        {
            Text = "Pogrubiona",
            Location = new Point(66, 56),
            AutoSize = true,
            Checked = subtitleFontBold
        };
        font.Controls.AddRange([fontNameLabel, _subtitleFontName, fontSizeLabel, _subtitleFontSize, _subtitleFontBold]);

        var size = new GroupBox
        {
            Text = "Rozmiar czcionki napisów",
            Location = new Point(12, 104),
            Size = new Size(578, 112)
        };
        _subtitleFontSizeConstant = new RadioButton
        {
            Text = "Stała wielkość",
            Location = new Point(12, 22),
            AutoSize = true,
            Checked = subtitleFontSizeMode == SubtitleFontSizeMode.Constant
        };
        _subtitleFontSizeRelativeToWindow = new RadioButton
        {
            Text = "Zależny od wielkości okna",
            Location = new Point(12, 52),
            AutoSize = true,
            Checked = subtitleFontSizeMode != SubtitleFontSizeMode.Constant
        };
        var minimumLabel = new Label
        {
            Text = "Minimalny rozmiar czcionki (2-50):",
            Location = new Point(12, 80),
            AutoSize = true
        };
        _minimumSubtitleFontSize = CreateNumber(minimumSubtitleFontSize, 2, 50, new Point(272, 76));
        void UpdateMinimumFontSizeState() => _minimumSubtitleFontSize.Enabled = minimumLabel.Enabled = _subtitleFontSizeRelativeToWindow.Checked;
        _subtitleFontSizeConstant.CheckedChanged += (_, _) => UpdateMinimumFontSizeState();
        _subtitleFontSizeRelativeToWindow.CheckedChanged += (_, _) => UpdateMinimumFontSizeState();
        UpdateMinimumFontSizeState();
        size.Controls.AddRange([
            _subtitleFontSizeConstant,
            _subtitleFontSizeRelativeToWindow,
            minimumLabel,
            _minimumSubtitleFontSize
        ]);

        _subtitleTextColor = Color.FromArgb(subtitleTextColorArgb);
        _subtitleBackgroundColor = Color.FromArgb(subtitleBackgroundColorArgb);
        _subtitleOutlineColor = Color.FromArgb(subtitleOutlineColorArgb);
        var colors = new GroupBox
        {
            Text = "Kolory i obrys",
            Location = new Point(12, 228),
            Size = new Size(578, 166)
        };
        var textColorButton = CreateColorButton("Kolor napisu", new Point(12, 22));
        textColorButton.Click += (_, _) => SelectColor(
            _subtitleTextColor,
            color => _subtitleTextColor = color,
            UpdateSubtitleAppearancePreview);
        var backgroundColorButton = CreateColorButton("Kolor tła napisu", new Point(12, 50));
        backgroundColorButton.Click += (_, _) => SelectColor(
            _subtitleBackgroundColor,
            color => _subtitleBackgroundColor = color,
            UpdateSubtitleAppearancePreview);
        var backgroundOpacityLabel = new Label
        {
            Text = "Przezroczystość tła:",
            Location = new Point(12, 80),
            AutoSize = true
        };
        _subtitleBackgroundOpacityPercent = CreateNumber(subtitleBackgroundOpacityPercent, 0, 100, new Point(140, 76));
        var outlineColorButton = CreateColorButton("Kolor obrysu", new Point(12, 108));
        outlineColorButton.Click += (_, _) => SelectColor(
            _subtitleOutlineColor,
            color => _subtitleOutlineColor = color,
            UpdateSubtitleAppearancePreview);
        var outlineThicknessLabel = new Label
        {
            Text = "Grubość obrysu:",
            Location = new Point(12, 140),
            AutoSize = true
        };
        _subtitleOutlineThickness = CreateNumber(subtitleOutlineThickness, 0, 100, new Point(140, 136));
        _subtitleAppearancePreview = new Panel
        {
            Location = new Point(220, 22),
            Size = new Size(345, 138),
            BackColor = PreviewSurfaceColor
        };
        _subtitleAppearancePreview.Paint += DrawSubtitleAppearancePreview;
        _subtitleFontName.SelectedIndexChanged += (_, _) => UpdateSubtitleAppearancePreview();
        _subtitleFontSize.ValueChanged += (_, _) => UpdateSubtitleAppearancePreview();
        _subtitleFontBold.CheckedChanged += (_, _) => UpdateSubtitleAppearancePreview();
        _subtitleBackgroundOpacityPercent.ValueChanged += (_, _) => UpdateSubtitleAppearancePreview();
        _subtitleOutlineThickness.ValueChanged += (_, _) => UpdateSubtitleAppearancePreview();
        colors.Controls.AddRange([
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            _subtitleBackgroundOpacityPercent,
            outlineColorButton,
            outlineThicknessLabel,
            _subtitleOutlineThickness,
            _subtitleAppearancePreview
        ]);

        var placement = new GroupBox
        {
            Text = "Położenie napisów i marginesy",
            Location = new Point(12, 406),
            Size = new Size(578, 126)
        };
        var alignmentIndex = Math.Clamp((int)subtitleAlignment - 1, 0, _subtitleAlignmentButtons.Length - 1);
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var index = (row * 3) + column;
                var button = new RadioButton
                {
                    AutoSize = true,
                    Location = new Point(16 + (column * 23), 25 + (row * 23)),
                    Checked = index == alignmentIndex,
                    AccessibleName = GetSubtitleAlignmentName((SubtitleAlignment)(index + 1))
                };
                _subtitleAlignmentButtons[index] = button;
                placement.Controls.Add(button);
            }
        }

        placement.Controls.Add(CreateMarginLabel("Lewo", new Point(104, 24)));
        _subtitleMarginLeft = CreateNumber(subtitleMarginLeft, -10000, 10000, new Point(154, 23));
        placement.Controls.Add(_subtitleMarginLeft);
        placement.Controls.Add(CreateMarginLabel("Góra", new Point(250, 24)));
        _subtitleMarginTop = CreateNumber(subtitleMarginTop, -10000, 10000, new Point(300, 23));
        placement.Controls.Add(_subtitleMarginTop);
        placement.Controls.Add(CreateMarginLabel("Prawo", new Point(104, 55)));
        _subtitleMarginRight = CreateNumber(subtitleMarginRight, -10000, 10000, new Point(154, 54));
        placement.Controls.Add(_subtitleMarginRight);
        placement.Controls.Add(CreateMarginLabel("Dół", new Point(250, 55)));
        _subtitleMarginBottom = CreateNumber(subtitleMarginBottom, -10000, 10000, new Point(300, 54));
        placement.Controls.Add(_subtitleMarginBottom);
        _positionSubtitlesRelativeToVideoFrame = new CheckBox
        {
            Text = "Pozycja napisów względem klatki obrazu",
            Location = new Point(12, 96),
            AutoSize = true,
            Checked = positionSubtitlesRelativeToVideoFrame
        };
        placement.Controls.Add(_positionSubtitlesRelativeToVideoFrame);

        ArrangeSubtitleAppearanceTab(
            tab,
            font,
            fontNameLabel,
            fontSizeLabel,
            size,
            minimumLabel,
            colors,
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            outlineColorButton,
            outlineThicknessLabel,
            placement);
        return tab;
    }

    private static Label CreateMarginLabel(string text, Point location) => new()
    {
        Text = text,
        Location = location,
        Size = new Size(44, 23),
        TextAlign = ContentAlignment.MiddleRight
    };

    private static string GetSubtitleAlignmentName(SubtitleAlignment alignment) => alignment switch
    {
        SubtitleAlignment.TopLeft => "Góra, lewo",
        SubtitleAlignment.TopCenter => "Góra, środek",
        SubtitleAlignment.TopRight => "Góra, prawo",
        SubtitleAlignment.MiddleLeft => "Środek, lewo",
        SubtitleAlignment.MiddleCenter => "Środek",
        SubtitleAlignment.MiddleRight => "Środek, prawo",
        SubtitleAlignment.BottomLeft => "Dół, lewo",
        SubtitleAlignment.BottomCenter => "Dół, środek",
        SubtitleAlignment.BottomRight => "Dół, prawo",
        _ => "Dół, środek"
    };

    private static Button CreateColorButton(string text, Point location) => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Text = text,
        Location = location,
        MinimumSize = new Size(190, 0)
    };

    private void SelectColor(Color currentColor, Action<Color> applyColor, Action updatePreview)
    {
        using var dialog = new ColorDialog { Color = currentColor };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        applyColor(dialog.Color);
        updatePreview();
    }

    private void UpdateSubtitleAppearancePreview()
    {
        _subtitleAppearancePreview.BackColor = PreviewSurfaceColor;
        _subtitleAppearancePreview.Invalidate();
    }

    private void DrawSubtitleAppearancePreview(object? sender, PaintEventArgs eventArgs)
    {
        var graphics = eventArgs.Graphics;
        graphics.Clear(PreviewSurfaceColor);
        var backgroundAlpha = (int)Math.Round(_subtitleBackgroundOpacityPercent.Value * 255m / 100m, MidpointRounding.AwayFromZero);
        if (backgroundAlpha > 0)
        {
            using var background = new SolidBrush(Color.FromArgb(backgroundAlpha, _subtitleBackgroundColor));
            graphics.FillRectangle(background, _subtitleAppearancePreview.ClientRectangle);
        }
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var style = _subtitleFontBold.Checked ? FontStyle.Bold : FontStyle.Regular;
        var requestedSize = (float)_subtitleFontSize.Value * graphics.DpiY / 72f;
        var availableWidth = Math.Max(1f, _subtitleAppearancePreview.ClientSize.Width - 16f);
        var previewText = GetPreviewText(requestedSize, style, graphics, availableWidth);
        using var font = CreatePreviewFont(requestedSize, style);
        var bounds = graphics.MeasureString(previewText, font, int.MaxValue, StringFormat.GenericTypographic);
        var point = new PointF(
            Math.Max(0f, (_subtitleAppearancePreview.ClientSize.Width - bounds.Width) / 2f),
            Math.Max(0f, (_subtitleAppearancePreview.ClientSize.Height - bounds.Height) / 2f));
        var outlineWidth = (float)_subtitleOutlineThickness.Value * graphics.DpiX / 96f;
        using var glyphs = new System.Drawing.Drawing2D.GraphicsPath();
        glyphs.AddString(previewText, font.FontFamily, (int)font.Style, font.Size, point, StringFormat.GenericTypographic);
        if (outlineWidth > 0f)
        {
            using var outline = new Pen(_subtitleOutlineColor, outlineWidth) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            graphics.DrawPath(outline, glyphs);
        }

        using var foreground = new SolidBrush(_subtitleTextColor);
        graphics.FillPath(foreground, glyphs);
    }

    private string GetPreviewText(float fontSize, FontStyle style, Graphics graphics, float availableWidth)
    {
        foreach (var candidate in new[] { "Text... 123...", "Text...", "Text" })
        {
            using var font = CreatePreviewFont(fontSize, style);
            if (graphics.MeasureString(candidate, font, int.MaxValue, StringFormat.GenericTypographic).Width <= availableWidth)
            {
                return candidate;
            }
        }

        return "Text";
    }

    private Font CreatePreviewFont(float size, FontStyle style)
    {
        try
        {
            return new Font(SelectedSubtitleFontName, size, style, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font("Arial", size, style, GraphicsUnit.Pixel);
        }
    }

    private static Color PreviewSurfaceColor => Color.FromArgb(48, 48, 48);

    private TabPage CreateEditingTab(EditingSettings settings)
    {
        var tab = new TabPage("Edycja") { Padding = new Padding(12) };

        var reaction = new GroupBox
        {
            Text = "Reakcja",
            Location = new Point(12, 12),
            Size = new Size(578, 108)
        };
        var reactionLabel = new Label
        {
            Text = "Szybkość reakcji w sekundach (do opcji \"Wstaw klatkę początkową\"):",
            Location = new Point(12, 25),
            AutoSize = true
        };
        reaction.Controls.Add(reactionLabel);
        _reactionSeconds = CreateDecimalNumber(settings.ReactionSeconds, new Point(414, 21));
        _applyReactionToEnd = CreateCheckBox("Zastosuj dla \"Wstaw klatkę końcową\"", 50, settings.ApplyReactionToEndFrame);
        _applyReactionWhenMeasuringDelay = CreateCheckBox("Zastosuj przy badaniu opóźnienia napisów względem filmu", 76, settings.ApplyReactionWhenMeasuringSubtitleDelay);
        reaction.Controls.AddRange([_reactionSeconds, _applyReactionToEnd, _applyReactionWhenMeasuringDelay]);

        var frameInsertion = new GroupBox
        {
            Text = "Wstawianie klatki",
            Location = new Point(12, 131),
            Size = new Size(578, 92)
        };
        _preserveSubtitleDuration = CreateCheckBox("Przy wstawianiu klatki początkowej zachowaj poprzednią różnicę między klatkami", 25, settings.PreserveSubtitleDurationWhenInsertingStartFrame);
        _moveCaretToNextLine = CreateCheckBox("Po wstawieniu klatki napisu przenieś kursor do następnego wiersza", 54, settings.MoveCaretToNextLineAfterInsertingFrame);
        frameInsertion.Controls.AddRange([_preserveSubtitleDuration, _moveCaretToNextLine]);

        var duration = new GroupBox
        {
            Text = "Domyślny czas wyświetlania napisu",
            Location = new Point(12, 234),
            Size = new Size(578, 82)
        };
        var defaultDurationLabel = new Label { Text = "Czas na 50 znaków napisu:", Location = new Point(12, 29), AutoSize = true };
        duration.Controls.Add(defaultDurationLabel);
        _defaultFiftyCharacterDisplaySeconds = CreateDecimalNumber(settings.DefaultFiftyCharacterDisplaySeconds, new Point(190, 25));
        var additionalDurationLabel = new Label { Text = "plus dodatkowo [sek.]:", Location = new Point(295, 29), AutoSize = true };
        duration.Controls.Add(additionalDurationLabel);
        _additionalDefaultDisplaySeconds = CreateDecimalNumber(settings.AdditionalDefaultDisplaySeconds, new Point(442, 25));
        duration.Controls.AddRange([_defaultFiftyCharacterDisplaySeconds, _additionalDefaultDisplaySeconds]);

        ArrangeEditingTab(
            tab,
            reaction,
            reactionLabel,
            frameInsertion,
            duration,
            defaultDurationLabel,
            additionalDurationLabel);
        return tab;
    }

    private TabPage CreateSubtitlesTab(SubtitlePlaybackSettings settings)
    {
        var tab = new TabPage("Napisy") { Padding = new Padding(12) };
        var display = new GroupBox
        {
            Text = "Wyświetlanie napisów",
            Location = new Point(12, 12),
            Size = new Size(578, 180)
        };

        _adjustLineWidthToScreen = CreateCheckBox(
            "Dostosuj szerokość wierszy napisów, jeżeli wychodzą poza ekran",
            24,
            settings.AdjustLineWidthToScreen);
        _interpretMicroDvdControlCodes = CreateCheckBox(
            "Interpretuj w napisach kody kontrolne mDVDp (np. {y:i}{c:$FA0530})",
            49,
            settings.InterpretMicroDvdControlCodes);
        _warnOnUnsortedSubtitles = CreateCheckBox(
            "Podczas oglądania filmu informuj o nieposortowanych napisach",
            74,
            settings.WarnOnUnsortedSubtitles);

        var italicLabel = new Label
        {
            Text = "Wyświetlaj w kursywie wiersze zaczynające się od:",
            Location = new Point(12, 104),
            AutoSize = true
        };
        _italicLinesStartingWithSlash = new CheckBox
        {
            Text = "/",
            Location = new Point(342, 101),
            AutoSize = true,
            Checked = settings.ItalicLinesStartingWithSlash
        };
        _italicLinesStartingWithAsterisk = new CheckBox
        {
            Text = "*",
            Location = new Point(380, 101),
            AutoSize = true,
            Checked = settings.ItalicLinesStartingWithAsterisk
        };
        _italicLinesStartingWithHash = new CheckBox
        {
            Text = "#",
            Location = new Point(418, 101),
            AutoSize = true,
            Checked = settings.ItalicLinesStartingWithHash
        };

        var gapLabel = new Label
        {
            Text = "Minimalny odstęp między wyświetlanymi napisami (1-20) [w klatkach]:",
            Location = new Point(12, 139),
            AutoSize = true
        };
        _minimumSubtitleGapFrames = CreateNumber(settings.MinimumGapFrames, 1, 20, new Point(418, 135));

        display.Controls.AddRange([
            _adjustLineWidthToScreen,
            _interpretMicroDvdControlCodes,
            _warnOnUnsortedSubtitles,
            italicLabel,
            _italicLinesStartingWithSlash,
            _italicLinesStartingWithAsterisk,
            _italicLinesStartingWithHash,
            gapLabel,
            _minimumSubtitleGapFrames
        ]);
        ArrangeSubtitlesTab(tab, display, italicLabel, gapLabel);
        return tab;
    }

    private TabPage CreateMovieTab(PlaybackSettings settings)
    {
        var tab = new TabPage("Film") { Padding = new Padding(12) };
        var userSeek = new GroupBox
        {
            Text = "Przewinięcie użytkownika",
            Location = new Point(12, 12),
            Size = new Size(578, 98)
        };
        var backwardLabel = new Label
        {
            Text = "Przewinięcie użytkownika do tyłu [sek.]",
            Location = new Point(12, 25),
            Size = new Size(406, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        userSeek.Controls.Add(backwardLabel);
        _userSeekBackwardSeconds = CreateNumber(settings.UserSeekBackwardSeconds, 1, 9999, new Point(430, 27));
        userSeek.Controls.Add(_userSeekBackwardSeconds);
        var forwardLabel = new Label
        {
            Text = "Przewinięcie użytkownika do przodu [sek.]",
            Location = new Point(12, 54),
            Size = new Size(406, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        userSeek.Controls.Add(forwardLabel);
        _userSeekForwardSeconds = CreateNumber(settings.UserSeekForwardSeconds, 1, 9999, new Point(430, 57));
        userSeek.Controls.Add(_userSeekForwardSeconds);

        var pause = new GroupBox
        {
            Text = "Automatyczna pauza",
            Location = new Point(12, 122),
            Size = new Size(578, 112)
        };
        _pauseOnMinimize = CreateCheckBox(
            "Wstrzymaj film, gdy okno aplikacji jest zminimalizowane",
            24,
            settings.PauseOnMinimize);
        _pauseWhenDialogDisplayed = CreateCheckBox(
            "Wstrzymaj film, gdy wyświetlane jest okno dialogowe",
            51,
            settings.PauseWhenDialogDisplayed);
        _pauseOnDeactivate = CreateCheckBox(
            "Wstrzymaj film, gdy okno aplikacji nie jest aktywne",
            78,
            settings.PauseOnDeactivate);
        pause.Controls.AddRange([_pauseOnMinimize, _pauseWhenDialogDisplayed, _pauseOnDeactivate]);

        var fullScreen = new GroupBox
        {
            Text = "Tryb pełnoekranowy",
            Location = new Point(12, 246),
            Size = new Size(578, 58)
        };
        _exitFullscreenWhenPlaybackEnds = CreateCheckBox(
            "Po zakończeniu odtwarzania wyjdź z trybu pełnoekranowego",
            24,
            settings.ExitFullscreenWhenPlaybackEnds);
        fullScreen.Controls.Add(_exitFullscreenWhenPlaybackEnds);

        ArrangeMovieTab(tab, userSeek, backwardLabel, forwardLabel, pause, fullScreen);
        return tab;
    }

    private TabPage CreateVideoTab(PlaybackSettings settings)
    {
        var tab = new TabPage("Wideo") { Padding = new Padding(12) };
        var decoding = new GroupBox
        {
            Text = "Dekodowanie filmu",
            Location = new Point(12, 12),
            Size = new Size(578, 180)
        };

        var modeLabel = new Label
        {
            Text = "Ustawienie sposobu dekodowania filmu:",
            AutoSize = true
        };

        _videoDecodingAutoRadio = new RadioButton
        {
            Text = string.Empty,
            AutoSize = true,
            Checked = settings.VideoDecodingMode != VideoDecodingMode.Software,
            AccessibleName = "Automatyczne"
        };

        var autoTitleLabel = new Label
        {
            Text = "Automatyczne",
            AutoSize = true
        };

        var autoDesc1Label = new Label
        {
            Text = "Używa dekodowania sprzętowego (H/W), jeśli jest dostępne.",
            AutoSize = true
        };

        var autoDesc2Label = new Label
        {
            Text = "W przeciwnym razie automatycznie używa dekodowania programowego (S/W).",
            AutoSize = true
        };

        _videoDecodingSoftwareRadio = new RadioButton
        {
            Text = string.Empty,
            AutoSize = true,
            Checked = settings.VideoDecodingMode == VideoDecodingMode.Software,
            AccessibleName = "Programowe (S/W)"
        };

        var softwareTitleLabel = new Label
        {
            Text = "Programowe (S/W)",
            AutoSize = true
        };

        var softwareDescLabel = new Label
        {
            Text = "Zawsze używa dekodowania programowego.",
            AutoSize = true
        };

        var lifecycleLabel = new Label
        {
            Text = "Zmiana sposobu dekodowania zostanie zastosowana po ponownym otwarciu pliku wideo.",
            AutoSize = true
        };

        autoTitleLabel.Click += (_, _) => _videoDecodingAutoRadio.Checked = true;
        autoDesc1Label.Click += (_, _) => _videoDecodingAutoRadio.Checked = true;
        autoDesc2Label.Click += (_, _) => _videoDecodingAutoRadio.Checked = true;

        softwareTitleLabel.Click += (_, _) => _videoDecodingSoftwareRadio.Checked = true;
        softwareDescLabel.Click += (_, _) => _videoDecodingSoftwareRadio.Checked = true;

        var layout = CreateGroupLayout(
            decoding,
            new ColumnStyle(SizeType.AutoSize),
            new ColumnStyle(SizeType.Percent, 100F));

        for (var i = 0; i < 7; i++)
        {
            AddAutoRow(layout);
        }

        PrepareLeftAligned(modeLabel);
        PrepareLeftAligned(_videoDecodingAutoRadio);
        PrepareLeftAligned(autoTitleLabel);
        PrepareLeftAligned(autoDesc1Label);
        PrepareLeftAligned(autoDesc2Label);
        PrepareLeftAligned(_videoDecodingSoftwareRadio);
        PrepareLeftAligned(softwareTitleLabel);
        PrepareLeftAligned(softwareDescLabel);
        PrepareLeftAligned(lifecycleLabel);

        modeLabel.Margin = new Padding(3, 0, 3, 6);
        _videoDecodingAutoRadio.Margin = new Padding(3, 1, 0, 0);
        autoTitleLabel.Margin = new Padding(3, 1, 3, 2);
        autoDesc1Label.Margin = new Padding(3, 0, 3, 1);
        autoDesc2Label.Margin = new Padding(3, 0, 3, 8);
        _videoDecodingSoftwareRadio.Margin = new Padding(3, 1, 0, 0);
        softwareTitleLabel.Margin = new Padding(3, 1, 3, 2);
        softwareDescLabel.Margin = new Padding(3, 0, 3, 8);
        lifecycleLabel.Margin = new Padding(3, 0, 3, 0);

        layout.Controls.Add(modeLabel, 1, 0);

        layout.Controls.Add(_videoDecodingAutoRadio, 0, 1);
        layout.Controls.Add(autoTitleLabel, 1, 1);

        layout.Controls.Add(autoDesc1Label, 1, 2);

        layout.Controls.Add(autoDesc2Label, 1, 3);

        layout.Controls.Add(_videoDecodingSoftwareRadio, 0, 4);
        layout.Controls.Add(softwareTitleLabel, 1, 4);

        layout.Controls.Add(softwareDescLabel, 1, 5);

        layout.Controls.Add(lifecycleLabel, 1, 6);

        ArrangeTabSections(tab, decoding);
        return tab;
    }

    private TabPage CreateAppearanceTab(InformationOsdSettings settings)
    {
        var tab = new TabPage("OSD") { Padding = new Padding(12) };
        var informationOsd = new GroupBox
        {
            Text = "Pozycja tekstu informacyjnego (OSD)",
            Location = new Point(12, 12),
            Size = new Size(578, 126)
        };

        _informationOsdTopRight = new RadioButton
        {
            Text = "Prawy górny róg okna",
            Location = new Point(12, 26),
            AutoSize = true,
            Checked = settings.Position == InformationOsdPosition.TopRight
        };
        _informationOsdHidden = new RadioButton
        {
            Text = "Nie wyświetlaj",
            Location = new Point(12, 53),
            AutoSize = true,
            Checked = settings.Position == InformationOsdPosition.Hidden
        };

        var osdFontSizeLabel = new Label
        {
            Text = "Wielkość czcionki OSD (10-30):",
            Location = new Point(196, 24),
            Size = new Size(284, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        informationOsd.Controls.Add(osdFontSizeLabel);
        _informationOsdFontSize = CreateNumber(settings.FontSize, 10, 30, new Point(492, 26));

        var osdDurationLabel = new Label
        {
            Text = "Czas wyświetlania OSD [sek.]",
            Location = new Point(196, 54),
            Size = new Size(284, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        informationOsd.Controls.Add(osdDurationLabel);
        _informationOsdDurationSeconds = new NumericUpDown
        {
            Location = new Point(492, 56),
            Size = new Size(62, 23),
            Minimum = 0,
            Maximum = 9999,
            DecimalPlaces = 1,
            Increment = 0.1m,
            Value = Math.Clamp(decimal.Round((decimal)settings.DisplayDurationSeconds, 1), 0m, 9999m),
            TextAlign = HorizontalAlignment.Right
        };
        _showInformationOsdInWindowedMode = new CheckBox
        {
            Text = "Wyświetlaj również w trybie okienkowym",
            Location = new Point(12, 88),
            AutoSize = true,
            Checked = settings.ShowInWindowedMode
        };

        void UpdateEnabledState() =>
            _showInformationOsdInWindowedMode.Enabled = !_informationOsdHidden.Checked;

        _informationOsdTopRight.CheckedChanged += (_, _) => UpdateEnabledState();
        _informationOsdHidden.CheckedChanged += (_, _) => UpdateEnabledState();
        UpdateEnabledState();

        informationOsd.Controls.AddRange([
            _informationOsdTopRight,
            _informationOsdHidden,
            _informationOsdFontSize,
            _informationOsdDurationSeconds,
            _showInformationOsdInWindowedMode
        ]);

        _informationOsdTextColor = Color.FromArgb(settings.TextColorArgb);
        _informationOsdBackgroundColor = Color.FromArgb(settings.BackgroundColorArgb);
        _informationOsdBorderColor = Color.FromArgb(settings.BorderColorArgb);
        var osdColors = new GroupBox
        {
            Text = "Kolory i tło OSD",
            Location = new Point(12, 150),
            Size = new Size(578, 166)
        };
        var textColorButton = CreateColorButton("Kolor napisu", new Point(12, 22));
        textColorButton.Click += (_, _) => SelectColor(
            _informationOsdTextColor,
            color => _informationOsdTextColor = color,
            UpdateInformationOsdAppearancePreview);
        var backgroundColorButton = CreateColorButton("Kolor tła", new Point(12, 50));
        backgroundColorButton.Click += (_, _) => SelectColor(
            _informationOsdBackgroundColor,
            color => _informationOsdBackgroundColor = color,
            UpdateInformationOsdAppearancePreview);
        var backgroundOpacityLabel = new Label
        {
            Text = "Przezroczystość tła:",
            Location = new Point(12, 80),
            AutoSize = true
        };
        _informationOsdBackgroundOpacityPercent = CreateNumber(
            settings.BackgroundOpacityPercent,
            0,
            100,
            new Point(140, 76));
        var borderColorButton = CreateColorButton("Kolor obrysu", new Point(12, 108));
        borderColorButton.Click += (_, _) => SelectColor(
            _informationOsdBorderColor,
            color => _informationOsdBorderColor = color,
            UpdateInformationOsdAppearancePreview);
        var borderThicknessLabel = new Label
        {
            Text = "Grubość obrysu:",
            Location = new Point(12, 140),
            AutoSize = true
        };
        _informationOsdBorderThickness = CreateNumber(settings.BorderThickness, 0, 100, new Point(140, 136));
        _informationOsdAppearancePreview = new Panel
        {
            Location = new Point(220, 22),
            Size = new Size(345, 138),
            BackColor = PreviewSurfaceColor
        };
        _informationOsdAppearancePreview.Paint += DrawInformationOsdAppearancePreview;
        _informationOsdFontSize.ValueChanged += (_, _) => UpdateInformationOsdAppearancePreview();
        _informationOsdBackgroundOpacityPercent.ValueChanged += (_, _) => UpdateInformationOsdAppearancePreview();
        _informationOsdBorderThickness.ValueChanged += (_, _) => UpdateInformationOsdAppearancePreview();
        osdColors.Controls.AddRange([
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            _informationOsdBackgroundOpacityPercent,
            borderColorButton,
            borderThicknessLabel,
            _informationOsdBorderThickness,
            _informationOsdAppearancePreview
        ]);

        ArrangeAppearanceTab(
            tab,
            informationOsd,
            osdFontSizeLabel,
            osdDurationLabel,
            osdColors,
            textColorButton,
            backgroundColorButton,
            backgroundOpacityLabel,
            borderColorButton,
            borderThicknessLabel);
        return tab;
    }

    private void UpdateInformationOsdAppearancePreview()
    {
        _informationOsdAppearancePreview.BackColor = PreviewSurfaceColor;
        _informationOsdAppearancePreview.Invalidate();
    }

    private void DrawInformationOsdAppearancePreview(object? sender, PaintEventArgs eventArgs)
    {
        const string previewText = "OSD 12:34";
        var graphics = eventArgs.Graphics;
        graphics.Clear(PreviewSurfaceColor);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", (float)_informationOsdFontSize.Value, FontStyle.Regular);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        var textSize = graphics.MeasureString(previewText, font, PointF.Empty, format);
        var textHeight = TextRenderer.MeasureText(previewText, font).Height;
        var bounds = new Rectangle(
            Math.Max(0, (_informationOsdAppearancePreview.ClientSize.Width - (int)Math.Ceiling(textSize.Width) - (2 * OsdAppearanceRenderer.HorizontalPadding)) / 2),
            Math.Max(0, (_informationOsdAppearancePreview.ClientSize.Height - textHeight - (2 * OsdAppearanceRenderer.VerticalPadding)) / 2),
            Math.Max(1, (int)Math.Ceiling(textSize.Width) + (2 * OsdAppearanceRenderer.HorizontalPadding)),
            Math.Max(1, textHeight + (2 * OsdAppearanceRenderer.VerticalPadding)));
        OsdAppearanceRenderer.DrawBackground(
            graphics,
            bounds,
            _informationOsdBorderColor,
            _informationOsdBackgroundColor,
            (int)_informationOsdBackgroundOpacityPercent.Value,
            (int)_informationOsdBorderThickness.Value);
        using var foreground = new SolidBrush(Color.FromArgb(255, _informationOsdTextColor));
        graphics.DrawString(
            previewText,
            font,
            foreground,
            new PointF(
                bounds.X + OsdAppearanceRenderer.HorizontalPadding,
                bounds.Y + OsdAppearanceRenderer.VerticalPadding),
            format);
    }

    private static NumericUpDown CreateNumber(int value, int minimum, int maximum, Point location) => new()
    {
        Location = location,
        Size = new Size(62, 23),
        Minimum = minimum,
        Maximum = maximum,
        Value = Math.Clamp(value, minimum, maximum),
        TextAlign = HorizontalAlignment.Right
    };

    private static Label CreateTrailingNumericLabel(string text, int top) => new()
    {
        Text = text,
        Location = new Point(340, top),
        Size = new Size(140, 23),
        TextAlign = ContentAlignment.MiddleRight
    };

    private static NumericUpDown CreateDecimalNumber(double value, Point location) => new()
    {
        Location = location,
        Size = new Size(82, 25),
        Minimum = 0,
        Maximum = 9999,
        DecimalPlaces = 2,
        Increment = 0.01m,
        Value = Math.Clamp((decimal)value, 0m, 9999m),
        TextAlign = HorizontalAlignment.Right
    };

    private static CheckBox CreateCheckBox(string text, int top, bool value) => new()
    {
        Text = text,
        Location = new Point(12, top),
        AutoSize = true,
        Checked = value
    };
}

internal static class SystemFontCatalog
{
    private const byte DefaultCharSet = 1;
    private static readonly Lazy<IReadOnlyList<string>> FamilyNames = new(EnumerateFamilyNames);

    public static IReadOnlyList<string> GetFamilyNames() => FamilyNames.Value;

    private static IReadOnlyList<string> EnumerateFamilyNames()
    {
        var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var logFont = new LogFont
        {
            CharacterSet = DefaultCharSet,
            FaceName = string.Empty
        };
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        var deviceContext = graphics.GetHdc();
        try
        {
            EnumFontFamiliesEx(
                deviceContext,
                ref logFont,
                (fontAddress, textMetricAddress, fontType, parameter) =>
                {
                    var font = Marshal.PtrToStructure<LogFont>(fontAddress);
                    if (!string.IsNullOrWhiteSpace(font.FaceName) && !font.FaceName.StartsWith('@'))
                    {
                        names.Add(font.FaceName);
                    }

                    return 1;
                },
                IntPtr.Zero,
                0);
        }
        finally
        {
            graphics.ReleaseHdc(deviceContext);
        }

        return names
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumFontFamiliesEx(
        IntPtr deviceContext,
        ref LogFont logFont,
        EnumFontFamiliesExProc callback,
        IntPtr parameter,
        uint flags);

    private delegate int EnumFontFamiliesExProc(
        IntPtr fontAddress,
        IntPtr textMetricAddress,
        uint fontType,
        IntPtr parameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LogFont
    {
        public int Height;
        public int Width;
        public int Escapement;
        public int Orientation;
        public int Weight;
        public byte Italic;
        public byte Underline;
        public byte StrikeOut;
        public byte CharacterSet;
        public byte OutputPrecision;
        public byte ClipPrecision;
        public byte Quality;
        public byte PitchAndFamily;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }
}
