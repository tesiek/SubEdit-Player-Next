using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Controls;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void MoveTransportControlsToFullScreen()
    {
        _seekSlider.Parent?.Controls.Remove(_seekSlider);
        _transportToolbar.Parent?.Controls.Remove(_transportToolbar);
        _fullScreenControlsPanel.Controls.Add(_seekSlider);
        _fullScreenControlsPanel.Controls.Add(_transportToolbar);
        _seekSlider.Dock = DockStyle.None;
        _transportToolbar.Dock = DockStyle.None;
        _transportToolbar.AutoSize = false;
        _seekSlider.BackColor = FullScreenToolbarRenderer.BackgroundColor;
        _transportToolbar.Renderer = new FullScreenToolbarRenderer();
        _seekSlider.Visible = true;
        _transportToolbar.Visible = true;
        LayoutFullScreenControls();
    }

    private void RestoreTransportControlsFromFullScreen()
    {
        _fullScreenControlsPanel.Visible = false;
        _seekSlider.Parent?.Controls.Remove(_seekSlider);
        _transportToolbar.Parent?.Controls.Remove(_transportToolbar);
        _seekSlider.Dock = DockStyle.Bottom;
        _transportToolbar.Dock = DockStyle.Bottom;
        _transportToolbar.AutoSize = true;
        _seekSlider.BackColor = SystemColors.Control;
        _transportToolbar.RenderMode = ToolStripRenderMode.System;
        _splitContainer.Panel1.Controls.Add(_seekSlider);
        _splitContainer.Panel1.Controls.Add(_transportToolbar);
        _seekSlider.Visible = true;
        _transportToolbar.Visible = true;
    }

    private void UpdateFullScreenControlsFromPointer()
    {
        if (!_isFullScreen)
        {
            return;
        }

        var pointer = Cursor.Position;
        var videoBounds = _videoPanel.RectangleToScreen(_videoPanel.ClientRectangle);
        var panelBounds = _fullScreenControlsPanel.RectangleToScreen(_fullScreenControlsPanel.ClientRectangle);
        var pointerIsNearTop = videoBounds.Contains(pointer) &&
            pointer.Y <= videoBounds.Top + Math.Max(40, _fullScreenControlsPanel.Height);

        if (pointerIsNearTop ||
            (_fullScreenControlsPanel.Visible && panelBounds.Contains(pointer)) ||
            _seekSlider.IsInteracting ||
            _fullScreenActiveMenu?.Visible == true)
        {
            ShowFullScreenControls();
            return;
        }

        if (_fullScreenControlsPanel.Visible)
        {
            _fullScreenControlsPanel.Visible = false;
            _wheelSeekDeltaAccumulator.Reset();
        }
    }

    private void UpdateFullScreenMouseCursor()
    {
        if (!_isFullScreen)
        {
            return;
        }

        var cursorPosition = Cursor.Position;
        if (cursorPosition != _lastFullScreenCursorPosition)
        {
            _lastFullScreenCursorPosition = cursorPosition;
            _fullScreenCursorLastMovedAt = DateTime.UtcNow;
            ShowFullScreenMouseCursor();
            return;
        }

        var controlsBounds = _fullScreenControlsPanel.RectangleToScreen(
            _fullScreenControlsPanel.ClientRectangle);
        if (_fullScreenControlsPanel.Visible && controlsBounds.Contains(cursorPosition))
        {
            ShowFullScreenMouseCursor();
            return;
        }

        if (!_isFullScreenCursorHidden &&
            DateTime.UtcNow - _fullScreenCursorLastMovedAt >= TimeSpan.FromSeconds(2))
        {
            HideFullScreenMouseCursor();
        }
    }

    private void HideFullScreenMouseCursor()
    {
        if (_isFullScreenCursorHidden)
        {
            return;
        }

        Cursor.Hide();
        _isFullScreenCursorHidden = true;
    }

    private void ShowFullScreenMouseCursor()
    {
        if (!_isFullScreenCursorHidden)
        {
            return;
        }

        Cursor.Show();
        _isFullScreenCursorHidden = false;
    }

    private void ShowFullScreenControls()
    {
        if (!_isFullScreen || _transportToolbar.Parent != _fullScreenControlsPanel)
        {
            return;
        }

        if (!_fullScreenControlsPanel.Visible)
        {
            _fullScreenControlsPanel.Visible = true;
            _fullScreenControlsPanel.BringToFront();
        }
    }

    private void LayoutFullScreenControls()
    {
        if (_transportToolbar.Parent != _fullScreenControlsPanel || _videoPanel.ClientSize.Width <= 0)
        {
            return;
        }

        var minimumWidth = ScaleLogicalPixels(540);
        var gap = Math.Max(24, (int)Math.Round(DeviceDpi * 5d / 2.54d));
        using var overlayFont = new Font("Segoe UI", _settings.InformationOsd.FontSize, FontStyle.Regular);
        var leftOsdWidth = TextRenderer.MeasureText("0:00:00 / 0:00:00", overlayFont).Width + 20;
        var rightOsdWidth = TextRenderer.MeasureText("Głośność: 100%", overlayFont).Width + 20;
        var width = _videoPanel.ClientSize.Width - leftOsdWidth - rightOsdWidth - (2 * gap);
        var x = leftOsdWidth + gap;
        if (width < minimumWidth)
        {
            x = 8;
            width = Math.Max(1, _videoPanel.ClientSize.Width - 16);
        }

        var seekHeight = Math.Max(16, _seekSlider.Height);
        var toolbarHeight = Math.Max(28, _transportToolbar.GetPreferredSize(new Size(width, 0)).Height);
        var statusHeight = Math.Max(
            ScaleLogicalPixels(20),
            TextRenderer.MeasureText("Hg", _fullScreenStatusStrip.Font, Size.Empty, TextFormatFlags.NoPadding).Height + ScaleLogicalPixels(4));
        _fullScreenControlsPanel.Bounds = new Rectangle(x, ScaleLogicalPixels(4), width, seekHeight + toolbarHeight + statusHeight);
        _seekSlider.Bounds = new Rectangle(1, 1, Math.Max(1, width - 2), seekHeight);
        _transportToolbar.Bounds = new Rectangle(1, seekHeight, Math.Max(1, width - 2), toolbarHeight);
        _fullScreenStatusStrip.Bounds = new Rectangle(1, seekHeight + toolbarHeight, Math.Max(1, width - 2), statusHeight);
        LayoutFullScreenStatusCells();
    }

    private void UpdateFullScreenStatusBar(long position, long length)
    {
        if (!_isFullScreen)
        {
            return;
        }

        var playbackState = _isStopped
            ? "Zatrzymano"
            : _playback.State == PlaybackState.Playing ? "Odtwarzanie" : "Pauza";
        var decoder = _decoderModeStatusText;
        var details = new[]
            {
                decoder,
                _videoCodecStatusText,
                _resolutionStatusText,
                _fpsStatus.Text,
                _audioTrackStatusText
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"[{value}]");
        SetFullScreenStatusText(_fullScreenPlaybackStatus, string.Join(" ", new[] { playbackState }.Concat(details)));
        SetFullScreenStatusText(
            _fullScreenFileNameStatus,
            _currentVideoPath is null ? string.Empty : Path.GetFileName(_currentVideoPath));
        SetFullScreenStatusText(_fullScreenTimeStatus, $"{FormatTime(position)} / {FormatTime(length)}");
        LayoutFullScreenStatusCells();
    }


    private void SetFullScreenStatusText(ToolStripStatusLabel status, string text)
    {
        if (!string.Equals(status.Tag as string, text, StringComparison.Ordinal))
        {
            status.Tag = text;
            status.ToolTipText = text;
        }
    }

    private void LayoutFullScreenStatusCells()
    {
        if (_fullScreenStatusStrip.Width <= 0)
        {
            return;
        }

        var padding = ScaleLogicalPixels(3);
        _fullScreenStatusStrip.Padding = new Padding(padding, 0, padding, 0);
        var availableWidth = Math.Max(1, _fullScreenStatusStrip.ClientSize.Width - _fullScreenStatusStrip.Padding.Horizontal);
        var playbackText = GetFullScreenStatusSource(_fullScreenPlaybackStatus);
        var fileNameText = GetFullScreenStatusSource(_fullScreenFileNameStatus);
        var timeText = GetFullScreenStatusSource(_fullScreenTimeStatus);
        var timeWidth = Math.Min(
            MeasureFullScreenStatusText(timeText),
            Math.Max(ScaleLogicalPixels(90), availableWidth / 3));
        var remainingWidth = Math.Max(1, availableWidth - timeWidth);
        var minimumFileNameWidth = Math.Min(
            ScaleLogicalPixels(180),
            Math.Max(ScaleLogicalPixels(72), remainingWidth / 2));
        var playbackWidth = Math.Min(
            MeasureFullScreenStatusText(playbackText),
            Math.Max(ScaleLogicalPixels(72), remainingWidth - minimumFileNameWidth));
        playbackWidth = Math.Min(playbackWidth, remainingWidth);
        var fileNameWidth = Math.Max(1, remainingWidth - playbackWidth);

        _fullScreenPlaybackStatus.Width = playbackWidth;
        _fullScreenFileNameStatus.Width = fileNameWidth;
        _fullScreenTimeStatus.Width = timeWidth;
        _fullScreenPlaybackStatus.Text = FitTextToWidth(playbackText, _fullScreenPlaybackStatus.Font, playbackWidth);
        _fullScreenFileNameStatus.Text = FitTextToWidth(fileNameText, _fullScreenFileNameStatus.Font, fileNameWidth);
        _fullScreenTimeStatus.Text = FitTextToWidth(timeText, _fullScreenTimeStatus.Font, timeWidth);
    }

    private int MeasureFullScreenStatusText(string text) =>
        TextRenderer.MeasureText(text, _fullScreenStatusStrip.Font, Size.Empty, TextFormatFlags.NoPadding).Width + ScaleLogicalPixels(6);

    private static string GetFullScreenStatusSource(ToolStripStatusLabel status) =>
        status.Tag as string ?? status.Text ?? string.Empty;

    private void ToggleFullScreenFromVideoSurface()
    {
        ToggleFullScreen();
        Focus();
    }

    private void ToggleFullScreen()
    {
        _seekSlider.CancelInteraction();
        _wheelSeekDeltaAccumulator.Reset();
        if (!_isFullScreen)
        {
            _editorCaretPositionBeforeFullScreen = _subtitleEditor.CurrentPosition;
            SetSubtitleTraceEnabled(false);
            _restoreBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _restoreWindowState = WindowState;
            _restoreBorderStyle = FormBorderStyle;
            _editorWasVisible = !_splitContainer.Panel2Collapsed;
            _menu.Visible = false;
            _mainToolbar.Visible = false;
            _statusStrip.Visible = false;
            _splitContainer.Panel2Collapsed = true;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            _isFullScreen = true;
            MoveTransportControlsToFullScreen();
            _lastFullScreenCursorPosition = Cursor.Position;
            _fullScreenCursorLastMovedAt = DateTime.UtcNow;
            HideFullScreenMouseCursor();
            _fullScreenControlsTimer.Start();
            UpdateFullScreenControlsFromPointer();
            ApplyMovieTransformations();
            UpdateSubtitleConversionAvailability();
            ToolbarButton("toolbarFullscreen").Checked = true;
            _videoOverlay.IsFullScreen = true;
            UpdateTimeCounterOsd(_playback.PositionMilliseconds, _playback.DurationMilliseconds, force: true);
            ShowPlaybackOsd("Pełny ekran");
            BeginInvoke(SyncVideoOverlay);
            return;
        }

        WindowState = FormWindowState.Normal;
        _fullScreenControlsTimer.Stop();
        ShowFullScreenMouseCursor();
        RestoreTransportControlsFromFullScreen();
        FormBorderStyle = _restoreBorderStyle;
        Bounds = _restoreBounds;
        WindowState = _restoreWindowState;
        _menu.Visible = true;
        _mainToolbar.Visible = true;
        _statusStrip.Visible = true;
        _splitContainer.Panel2Collapsed = !_editorWasVisible;
        _isFullScreen = false;
        ApplyMovieTransformations();
        UpdateSubtitleConversionAvailability();
        ToolbarButton("toolbarFullscreen").Checked = false;
        _videoOverlay.IsFullScreen = false;
        UpdateTimeCounterOsd(_playback.PositionMilliseconds, _playback.DurationMilliseconds, force: true);
        ShowPlaybackOsd("Tryb okienkowy");
        BeginInvoke(RestoreWindowActivation);
    }
}
