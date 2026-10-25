using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void OpenVideo()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Pliki wideo|*.mkv;*.mp4;*.avi;*.mov;*.wmv;*.mpeg;*.mpg;*.m2ts;*.ts;*.mts;*.vob|Wszystkie pliki|*.*",
            Title = "Otwórz plik wideo"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        OpenVideoFile(dialog.FileName);
    }

    private void OpenSubtitleFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Pliki napisów|*.srt;*.sub;*.txt;*.mpl|Wszystkie pliki|*.*",
            Title = "Otwórz plik z napisami"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SelectTextSubtitle(dialog.FileName);
    }

    private void OpenVideoFile(string videoPath)
    {
        LoadVideoFile(videoPath);
    }

    private void OpenStartupVideo()
    {
        if (string.IsNullOrWhiteSpace(_startupVideoPath) ||
            !File.Exists(_startupVideoPath) ||
            !IsVideoFile(_startupVideoPath))
        {
            return;
        }

        var resumePosition = string.Equals(
            _settings.Playback.LastVideoPath,
            _startupVideoPath,
            StringComparison.OrdinalIgnoreCase)
            ? _settings.Playback.LastPositionMilliseconds
            : 0;
        LoadVideoFile(_startupVideoPath, resumePosition);
    }

    private void ConfigureFileDropTargets(Control control)
    {
        if (ReferenceEquals(control, this))
        {
            _videoHost.RegisterFileDrop(OnFileDragEnter, OnFileDragDrop);
        }

        if (ReferenceEquals(control, _videoHost.HostControl))
        {
            foreach (Control child in control.Controls)
            {
                ConfigureFileDropTargets(child);
            }

            return;
        }

        control.AllowDrop = true;
        control.DragEnter += OnFileDragEnter;
        control.DragDrop += OnFileDragDrop;

        foreach (Control child in control.Controls)
        {
            ConfigureFileDropTargets(child);
        }
    }

    private void OnFileDragEnter(object? sender, DragEventArgs eventArgs)
    {
        eventArgs.Effect = !_isFullScreen && TryGetDroppedMediaFile(eventArgs.Data, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnFileDragDrop(object? sender, DragEventArgs eventArgs)
    {
        if (_isFullScreen || !TryGetDroppedMediaFile(eventArgs.Data, out var filePath))
        {
            return;
        }

        if (IsVideoFile(filePath))
        {
            OpenVideoFile(filePath);
            return;
        }

        SelectTextSubtitle(filePath);
    }

    private static bool TryGetDroppedMediaFile(IDataObject? dataObject, out string filePath)
    {
        filePath = string.Empty;
        if (dataObject?.GetDataPresent(DataFormats.FileDrop) != true ||
            dataObject.GetData(DataFormats.FileDrop) is not string[] filePaths)
        {
            return false;
        }

        var supportedFilePath = filePaths.FirstOrDefault(path =>
            File.Exists(path) && (IsVideoFile(path) || IsSubtitleFile(path)));
        if (supportedFilePath is null)
        {
            return false;
        }

        filePath = supportedFilePath;
        return true;
    }

    private static bool IsSubtitleFile(string path) =>
        SupportedSubtitleExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void AppendSubtitleFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Pliki napisów|*.srt;*.sub;*.txt;*.mpl|Wszystkie pliki|*.*",
            Title = "Dodaj plik na koniec napisów"
        };

        var initialPath = _currentSubtitlePath ?? _currentVideoPath;
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            var initialDirectory = Path.GetDirectoryName(initialPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var appendedText = SubtitleFileReader.Read(dialog.FileName).Text;
            if (appendedText.Length == 0)
            {
                return;
            }

            var currentText = _subtitleEditor.Text;
            var separator = currentText.Length == 0 || EndsWithLineBreak(currentText)
                ? string.Empty
                : GetEditorLineBreak(currentText);
            var appendedTextStart = currentText.Length + separator.Length;

            _subtitleEditor.ExecuteWritableMutation(() =>
            {
                _subtitleEditor.BeginUndoAction();
                try
                {
                    _subtitleEditor.SetSelection(currentText.Length, currentText.Length);
                    _subtitleEditor.ReplaceSelection(separator + appendedText);
                    _subtitleEditor.SetCaretPosition(appendedTextStart);
                    _subtitleEditor.ScrollCaret();
                    _subtitleEditor.Focus();
                }
                finally
                {
                    _subtitleEditor.EndUndoAction();
                }
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            MessageBox.Show(this, exception.Message, "Nie można dodać pliku napisów", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool LoadVideoFile(string videoPath, long? resumePosition = null, string? preferredSubtitlePath = null)
    {
        if (!ConfirmSubtitleChanges())
        {
            return false;
        }

        if (!string.Equals(_currentVideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
        {
            _sessionFrameRate = 25d;
            _sessionFrameRateOverridden = false;
            _fpsStatus.ForeColor = SystemColors.ControlText;
        }

        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        _seekSlider.DurationMilliseconds = 0;
        _seekSlider.ChapterMarkers = Array.Empty<long>();
        _audioTrackStatusText = string.Empty;
        ClearSubtitles();
        if (!string.Equals(_settings.Playback.LastVideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
        {
            _settings.Playback.LastAudioTrackId = null;
            _settings.Playback.LastSubtitleTrackId = null;
        }

        try
        {
            _playback.Open(videoPath);
            _lastKnownMediaLengthMilliseconds = 0;
            _stoppedSeekPosition = 0;
            SynchronizeSmoothedPosition(0);
            _mediaDetailsAvailable = false;
            _resolutionStatusText = string.Empty;
            _fpsStatus.Text = string.Empty;
            ResetDecoderStatus(detecting: true);
            _sizeStatus.Text = string.Empty;
            _videoCodecStatusText = string.Empty;
            UpdateEditorDecoderStatus();
            _currentVideoPath = videoPath;
            _pendingResumePosition = resumePosition is > 0 ? resumePosition : null;
            _pendingResumePrecise = false;
            _settings.Playback.LastVideoPath = videoPath;
            RememberRecentFile(_settings.Playback.RecentVideoPaths, videoPath);
            _settings.Playback.LastPositionMilliseconds = resumePosition ?? 0;
            _isStopped = false;
            _automaticTrackSelectionPending = true;
            if (!TryLoadMatchingSubtitle(videoPath) &&
                !string.IsNullOrWhiteSpace(preferredSubtitlePath))
            {
                LoadSubtitleFile(preferredSubtitlePath, showErrors: false);
            }
            _playback.Play();
            ApplyMovieTransformations();
            ApplyPlaybackSpeed();
            RefreshFilePathStatus();
            UpdateTransportButtons();
            UpdateCloseVideoMenuState();
            UpdateWindowTitle();
            return true;
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Failed to load video file '{videoPath}': {exception.Message}");
            HandleMediaOpenFailure();
            return false;
        }
    }

    private void HandleMediaOpenFailure()
    {
        try
        {
            _playback.Close();
        }
        catch
        {
        }

        SetSubtitleTraceEnabled(false);
        InvalidatePendingRelativeSeek();
        _seekSlider.CancelInteraction();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        _audioTrackStatusText = string.Empty;
        _seekSlider.DurationMilliseconds = 0;
        _seekSlider.ChapterMarkers = Array.Empty<long>();
        _currentVideoPath = null;
        UpdateWindowTitle();
        _lastKnownMediaLengthMilliseconds = 0;
        _stoppedSeekPosition = 0;
        _isStopped = true;
        _mediaDetailsAvailable = false;
        _videoOverlay.VideoSourceSize = Size.Empty;
        _resolutionStatusText = string.Empty;
        _fpsStatus.Text = string.Empty;
        ResetDecoderStatus(detecting: false);
        _sizeStatus.Text = string.Empty;
        _videoCodecStatusText = string.Empty;
        UpdateEditorDecoderStatus();
        _externalSubtitleOverlayEnabled = false;
        UpdateSubtitleOverlay(0);
        ResetSeekSliderToStart();
        UpdateTransportButtons();
        UpdateCloseVideoMenuState();
        UpdateSubtitleConversionAvailability();

        _fileStatusExpiresAt = default;
        _fileStatus.ForeColor = Color.Red;
        _fileStatus.ToolTipText = "Nie można renderować pliku.";
        _fileStatus.Text = "Nie można renderować pliku.";
    }

    private void CloseVideoFile()
    {
        if (!_playback.IsMediaOpen && _currentVideoPath is null)
        {
            return;
        }

        SetSubtitleTraceEnabled(false);
        InvalidatePendingRelativeSeek();
        _seekSlider.CancelInteraction();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        _playback.Close();
        _audioTrackStatusText = string.Empty;
        _seekSlider.DurationMilliseconds = 0;
        _seekSlider.ChapterMarkers = Array.Empty<long>();
        _currentVideoPath = null;
        UpdateWindowTitle();
        _lastKnownMediaLengthMilliseconds = 0;
        _stoppedSeekPosition = 0;
        _isStopped = true;
        _mediaDetailsAvailable = false;
        _videoOverlay.VideoSourceSize = Size.Empty;
        _resolutionStatusText = string.Empty;
        _fpsStatus.Text = string.Empty;
        ResetDecoderStatus(detecting: false);
        _sizeStatus.Text = string.Empty;
        _videoCodecStatusText = string.Empty;
        UpdateEditorDecoderStatus();
        _settings.Playback.LastPositionMilliseconds = 0;
        _externalSubtitleOverlayEnabled = false;
        UpdateSubtitleOverlay(0);
        ResetSeekSliderToStart();
        _fileStatusExpiresAt = default;
        _fileStatus.ForeColor = SystemColors.ControlText;
        _fileStatus.ToolTipText = string.Empty;
        _fileStatus.Text = string.Empty;
        UpdateTransportButtons();
        UpdateCloseVideoMenuState();
        UpdateSubtitleConversionAvailability();
        ShowPlaybackOsd("Plik wideo zamknięty");
    }

    private void UpdateWindowTitle()
    {
        Text = _playback.IsMediaOpen && _currentVideoPath is not null
            ? Path.GetFileName(_currentVideoPath)
            : "SubEdit-Player Next";
    }

    private void UpdateCloseVideoMenuState()
    {
        var hasVideo = _playback.IsMediaOpen && _currentVideoPath is not null;
        _closeVideoMenuItem?.Enabled = hasVideo;
        _goToVideoPositionMenuItem?.Enabled = hasVideo;
        _measureSubtitleDelayMenuItem?.Enabled = hasVideo && _subtitleDocument.Cues.Count > 0;
    }

    private bool LoadSubtitleFile(string subtitlePath, bool showErrors, bool showSrtConversionQuestion = false)
    {
        try
        {
            var subtitleFile = SubtitleFileReader.Read(subtitlePath);
            var subtitleText = subtitleFile.Text;
            var convertedFromSrt = false;
            if (showSrtConversionQuestion && SubtitleFormatConverter.IsSrtDocument(subtitleText))
            {
                using var dialog = new QuestionDialog(
                    "Otwierany plik napisów jest w formacie SubRip (SRT).\r\n" +
                    "Czy przekonwertować napisy do formatu mDVD?",
                    includeCancel: false,
                    yesText: "Yes",
                    noText: "No");
                if (dialog.ShowDialog(this) == DialogResult.Yes)
                {
                    subtitleText = SubtitleFormatConverter.ConvertSrtToMicroDvd(subtitleText, GetVideoFrameRate());
                    convertedFromSrt = true;
                }
            }

            _unsortedSubtitleWarningTracker.Reset();
            SetSubtitleEditorText(subtitleText);
            _subtitleEditor.EmptyUndoBuffer();
            _subtitleEditor.SetSavePoint();
            ClearRememberedSubtitleTimeScaleTransform();
            if (SubtitleTimingFormatter.Detect(subtitleText) is { } detectedFormat)
            {
                _settings.ActiveSubtitleFormat = detectedFormat.ToString();
                UpdateActiveSubtitleFormatIndicator();
            }

            _currentSubtitlePath = convertedFromSrt ? null : subtitlePath;
            _suggestedSubtitleSavePath = convertedFromSrt
                ? Path.ChangeExtension(subtitlePath, ".txt")
                : null;
            _subtitleEncoding = subtitleFile.Encoding;
            _savedSubtitleEncoding = convertedFromSrt ? null : subtitleFile.Encoding;
            EnableExternalSubtitleOverlay();
            _settings.Playback.LastSubtitleTrackId = null;
            _settings.Playback.LastSubtitlePath = _currentSubtitlePath;
            RememberRecentFile(_settings.Playback.RecentSubtitlePaths, subtitlePath);
            UpdateSubtitleDirtyState();
            UpdateSubtitleOverlay(_playback.PositionMilliseconds);
            PositionCaretAfterSubtitleLoad();
            if (_subtitleTraceEnabled && IsHandleCreated)
            {
                BeginInvoke(ResumeSubtitleTraceAfterSubtitleLoad);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            if (showErrors)
            {
                MessageBox.Show(this, exception.Message, "Nie można wczytać napisów", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return false;
        }
    }

    private bool TryLoadMatchingSubtitle(string videoPath)
    {
        var directory = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        try
        {
            var subtitlePath = AutomaticSubtitleSelector.SelectExternalSubtitle(
                videoPath,
                Directory.EnumerateFiles(directory)
                    .Where(path => SupportedSubtitleExtensions.Contains(
                        Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)));
            return subtitlePath is not null && LoadSubtitleFile(subtitlePath, showErrors: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ClearSubtitles()
    {
        ClearRememberedSubtitleTimeScaleTransform();
        SetSubtitleTraceEnabled(false);
        _unsortedSubtitleWarningTracker.Reset();
        _externalSubtitleOverlayEnabled = false;
        SetSubtitleEditorText(string.Empty);
        _subtitleEditor.EmptyUndoBuffer();
        _subtitleEditor.SetSavePoint();
        _currentSubtitlePath = null;
        _suggestedSubtitleSavePath = null;
        _subtitleEncoding = null;
        _savedSubtitleEncoding = null;
        _settings.Playback.LastSubtitlePath = null;
        UpdateSubtitleDirtyState();
        UpdateSubtitleConversionAvailability();
    }

    private void CloseSubtitles()
    {
        if (!ConfirmSubtitleChanges())
        {
            return;
        }

        ClearSubtitles();
    }

    private void NewSubtitle()
    {
        if (!ConfirmSubtitleChanges())
        {
            return;
        }

        ClearSubtitles();
        _subtitleEditor.EmptyUndoBuffer();
        _subtitleEditor.Focus();
    }
}
