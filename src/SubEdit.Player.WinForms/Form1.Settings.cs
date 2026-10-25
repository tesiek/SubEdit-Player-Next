namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ShowEditorOptionsDialog()
    {
        using var dialog = new EditorOptionsDialog(
            _settings.Editor,
            _settings.Editing,
            _settings.SubtitlePlayback,
            _settings.Playback,
            _settings.InformationOsd,
            _settings.SubtitleFontName,
            _settings.SubtitleFontSize,
            _settings.SubtitleFontBold,
            _settings.SubtitleFontSizeMode,
            _settings.MinimumSubtitleFontSize,
            _settings.SubtitleTextColorArgb,
            _settings.SubtitleBackgroundColorArgb,
            _settings.SubtitleBackgroundOpacityPercent,
            _settings.SubtitleOutlineColorArgb,
            _settings.SubtitleOutlineThickness,
            _settings.SubtitleAlignment,
            _settings.SubtitleMarginLeft,
            _settings.SubtitleMarginRight,
            _settings.SubtitleMarginTop,
            _settings.SubtitleMarginBottom,
            _settings.PositionSubtitlesRelativeToVideoFrame,
            _settings.LastOptionsTabIndex);
        dialog.ApplyRequested += (_, _) => ApplyOptions(dialog);
        dialog.ShowDialog(this);
        RememberOptionsTab(dialog.SelectedTabIndex);
    }

    private void ApplyOptions(EditorOptionsDialog dialog)
    {
        _settings.Editor = dialog.CreateEditorSettings();
        _settings.Editing = dialog.CreateEditingSettings();
        _settings.SubtitlePlayback = dialog.CreateSubtitlePlaybackSettings();
        _settings.Playback.UserSeekBackwardSeconds = dialog.SelectedUserSeekBackwardSeconds;
        _settings.Playback.UserSeekForwardSeconds = dialog.SelectedUserSeekForwardSeconds;
        _settings.Playback.PauseOnMinimize = dialog.SelectedPauseOnMinimize;
        _settings.Playback.PauseWhenDialogDisplayed = dialog.SelectedPauseWhenDialogDisplayed;
        _settings.Playback.PauseOnDeactivate = dialog.SelectedPauseOnDeactivate;
        _settings.Playback.ExitFullscreenWhenPlaybackEnds = dialog.SelectedExitFullscreenWhenPlaybackEnds;
        _settings.Playback.VideoDecodingMode = dialog.SelectedVideoDecodingMode;
        _settings.InformationOsd = dialog.CreateInformationOsdSettings();
        _settings.SubtitleFontName = dialog.SelectedSubtitleFontName;
        _settings.SubtitleFontSize = dialog.SelectedSubtitleFontSize;
        _settings.SubtitleFontBold = dialog.SelectedSubtitleFontBold;
        _settings.SubtitleFontSizeMode = dialog.SelectedSubtitleFontSizeMode;
        _settings.MinimumSubtitleFontSize = dialog.SelectedMinimumSubtitleFontSize;
        _settings.SubtitleTextColorArgb = dialog.SelectedSubtitleTextColor.ToArgb();
        _settings.SubtitleBackgroundColorArgb = dialog.SelectedSubtitleBackgroundColor.ToArgb();
        _settings.SubtitleBackgroundOpacityPercent = dialog.SelectedSubtitleBackgroundOpacityPercent;
        _settings.SubtitleOutlineColorArgb = dialog.SelectedSubtitleOutlineColor.ToArgb();
        _settings.SubtitleOutlineThickness = dialog.SelectedSubtitleOutlineThickness;
        _settings.SubtitleAlignment = dialog.SelectedSubtitleAlignment;
        _settings.SubtitleMarginLeft = dialog.SelectedSubtitleMarginLeft;
        _settings.SubtitleMarginRight = dialog.SelectedSubtitleMarginRight;
        _settings.SubtitleMarginTop = dialog.SelectedSubtitleMarginTop;
        _settings.SubtitleMarginBottom = dialog.SelectedSubtitleMarginBottom;
        _settings.PositionSubtitlesRelativeToVideoFrame = dialog.SelectedPositionSubtitlesRelativeToVideoFrame;
        _settings.LastOptionsTabIndex = dialog.SelectedTabIndex;
        ApplyOverlaySettings();
        if (!CanShowInformationOsd())
        {
            ClearInformationOsd();
        }
        LayoutFullScreenControls();
        ApplyEditorSettings();
        RefreshSubtitleDocument();
        SaveSettingsOrShowWarning();
    }

    private void ApplyOverlaySettings()
    {
        _videoOverlay.TransparentSubtitles = _transparentSubtitles;
        _videoOverlay.SubtitleFontName = _settings.SubtitleFontName;
        _videoOverlay.SubtitleFontSize = _settings.SubtitleFontSize;
        _videoOverlay.SubtitleFontBold = _settings.SubtitleFontBold;
        _videoOverlay.SubtitleFontSizeMode = _settings.SubtitleFontSizeMode;
        _videoOverlay.MinimumSubtitleFontSize = _settings.MinimumSubtitleFontSize;
        _videoOverlay.SubtitleTextColor = Color.FromArgb(_settings.SubtitleTextColorArgb);
        _videoOverlay.SubtitleBackgroundColor = Color.FromArgb(_settings.SubtitleBackgroundColorArgb);
        _videoOverlay.SubtitleBackgroundOpacityPercent = _settings.SubtitleBackgroundOpacityPercent;
        _videoOverlay.SubtitleOutlineColor = Color.FromArgb(_settings.SubtitleOutlineColorArgb);
        _videoOverlay.SubtitleOutlineThickness = _settings.SubtitleOutlineThickness;
        _videoOverlay.SubtitleAlignment = _settings.SubtitleAlignment;
        _videoOverlay.SubtitleMarginLeft = _settings.SubtitleMarginLeft;
        _videoOverlay.SubtitleMarginRight = _settings.SubtitleMarginRight;
        _videoOverlay.SubtitleMarginTop = _settings.SubtitleMarginTop;
        _videoOverlay.SubtitleMarginBottom = _settings.SubtitleMarginBottom;
        _videoOverlay.PositionSubtitlesRelativeToVideoFrame = _settings.PositionSubtitlesRelativeToVideoFrame;
        _videoOverlay.AdjustLineWidthToScreen = _settings.SubtitlePlayback.AdjustLineWidthToScreen;
        _videoOverlay.OsdFontSize = _settings.InformationOsd.FontSize;
        _videoOverlay.OsdTextColor = Color.FromArgb(_settings.InformationOsd.TextColorArgb);
        _videoOverlay.OsdBackgroundColor = Color.FromArgb(_settings.InformationOsd.BackgroundColorArgb);
        _videoOverlay.OsdBackgroundOpacityPercent = _settings.InformationOsd.BackgroundOpacityPercent;
        _videoOverlay.OsdBorderColor = Color.FromArgb(_settings.InformationOsd.BorderColorArgb);
        _videoOverlay.OsdBorderThickness = _settings.InformationOsd.BorderThickness;
    }

    private void RememberOptionsTab(int tabIndex)
    {
        if (_settings.LastOptionsTabIndex == tabIndex)
        {
            return;
        }

        _settings.LastOptionsTabIndex = tabIndex;
        SaveSettingsOrShowWarning();
    }

    private void SaveSettingsOrShowWarning()
    {
        if (_settingsStore.TrySave(_settings))
        {
            return;
        }

        MessageBox.Show(
            this,
            "Nie udało się zapisać ustawień w pliku SubEdit.Player.Next.json.",
            "SubEdit",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void ApplyEditorSettings()
    {
        _subtitleEditor.ApplyEditorSettings(_settings.Editor);
        ConfigureAutoSaveTimer();
    }
}