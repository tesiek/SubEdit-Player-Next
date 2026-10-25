using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void UpdateStatusBar(long position, long length)
    {
        var frameRate = GetVideoFrameRate();
        _mDVDStatus.Text = $"{{{ToMicroDvdFrame(position, frameRate)}}}{{{ToMicroDvdFrame(length, frameRate)}}}";
        _mpl2Status.Text = $"[{ToMpl2Time(position)}][{ToMpl2Time(length)}]";
        _positionStatus.Text = FormatStatusTime(position);
        _lengthStatus.Text = FormatStatusTime(length);

        if (!_mediaDetailsAvailable && _currentVideoPath is not null)
        {
            UpdateMediaDetails();
        }

        UpdateFullScreenStatusBar(position, length);
    }

    private static long ToMicroDvdFrame(long milliseconds, double framesPerSecond) =>
        Math.Max(0, (long)Math.Floor(milliseconds * framesPerSecond / 1000d));

    private static long ToMpl2Time(long milliseconds) =>
        Math.Max(0, (long)Math.Round(milliseconds / 100d));

    private void ResetDecoderStatus(bool detecting)
    {
        _videoDecoderMode = VideoDecoderMode.Unknown;
        _decoderModeStatusText = detecting ? "…" : string.Empty;
        _decoderStatus.ToolTipText = detecting
            ? "Ustalanie trybu dekodowania obrazu..."
            : string.Empty;
        UpdateEditorDecoderStatus();
    }

    private void UpdateEditorDecoderStatus()
    {
        var details = new[]
            {
                _decoderModeStatusText,
                _videoCodecStatusText,
                _resolutionStatusText,
                _audioTrackStatusText
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"[{value}]");
        _decoderStatus.Tag = string.Join(" ", details);
        LayoutEditorStatusDetails();
        RefreshFilePathStatus();
    }

    private void LayoutEditorStatusDetails()
    {
        if (_statusStrip.IsDisposed)
        {
            return;
        }

        var sourceText = _decoderStatus.Tag as string ?? string.Empty;
        var fixedWidth = _statusStrip.Items
            .Cast<ToolStripItem>()
            .Where(item => !ReferenceEquals(item, _fileStatus) && !ReferenceEquals(item, _decoderStatus))
            .Sum(item => item.Width + item.Margin.Horizontal);
        var reservedFileWidth = ScaleLogicalPixels(96);
        var availableWidth = Math.Max(
            1,
            _statusStrip.ClientSize.Width -
            _statusStrip.Padding.Horizontal -
            fixedWidth -
            reservedFileWidth -
            _fileStatus.Margin.Horizontal -
            _decoderStatus.Margin.Horizontal);
        var horizontalPadding = ScaleLogicalPixels(36);
        var desiredWidth = string.IsNullOrEmpty(sourceText)
            ? ScaleLogicalPixels(72)
            : TextRenderer.MeasureText(
                sourceText,
                _decoderStatus.Font,
                Size.Empty,
                TextFormatFlags.NoPadding).Width + horizontalPadding;

        _decoderStatus.Width = Math.Min(desiredWidth, availableWidth);
        _decoderStatus.Text = FitTextToWidth(
            sourceText,
            _decoderStatus.Font,
            Math.Max(1, _decoderStatus.Width - horizontalPadding));
    }

    private void UpdateFrameRateStatus()
    {
        if (_sessionFrameRateOverridden)
        {
            _fpsStatus.Text = $"{FormatFrameRate(_sessionFrameRate)} FPS";
            _fpsStatus.ForeColor = Color.Red;
            return;
        }

        var frameRate = GetLoadedVideoFrameRate();
        _fpsStatus.Text = frameRate is > 0d ? $"{FormatFrameRate(frameRate.Value)} FPS" : string.Empty;
        _fpsStatus.ForeColor = SystemColors.ControlText;
    }

    private static string FormatStatusTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)time.TotalHours}:{time:mm\\:ss\\.fff}";
    }

    private void ShowTemporaryFileStatus(string message)
    {
        _fileStatus.ForeColor = SystemColors.ControlText;
        _fileStatus.ToolTipText = message;
        _fileStatus.Text = FitTemporaryFileStatus(message);
        _fileStatusExpiresAt = DateTime.UtcNow.AddSeconds(2);
    }

    private void RestoreFileStatus()
    {
        if (_currentVideoPath is null)
        {
            _fileStatus.ForeColor = SystemColors.ControlText;
            _fileStatus.ToolTipText = string.Empty;
            _fileStatus.Text = string.Empty;
            return;
        }

        RefreshFilePathStatus();
    }

    private void RefreshFilePathStatus()
    {
        if (_currentVideoPath is null || _fileStatusExpiresAt != default)
        {
            return;
        }

        SetFilePathStatus(_currentVideoPath);
    }

    private void RefreshInitialLastVideoStatus()
    {
        if (_fileStatusExpiresAt != default)
        {
            return;
        }

        var lastVideoPath = _settings.Playback.LastVideoPath;
        if (_currentVideoPath is null &&
            !string.IsNullOrWhiteSpace(lastVideoPath) &&
            File.Exists(lastVideoPath))
        {
            SetFilePathStatus(lastVideoPath);
        }
    }

    private void SetFilePathStatus(string fullPath)
    {
        _fileStatus.ForeColor = SystemColors.ControlText;
        _fileStatus.ToolTipText = fullPath;
        var availableWidth = Math.Max(1, _fileStatus.Width - 8);
        _fileStatus.Text = FitPathToWidth(fullPath, _fileStatus.Font, availableWidth);
    }

    private string FitTemporaryFileStatus(string message)
    {
        var availableWidth = Math.Max(1, _fileStatus.Width - 8);
        if (TextRenderer.MeasureText(message, _fileStatus.Font).Width <= availableWidth)
        {
            return message;
        }

        var displayMessage = message;
        while (displayMessage.Length > 3 &&
               TextRenderer.MeasureText($"{displayMessage}...", _fileStatus.Font).Width > availableWidth)
        {
            displayMessage = displayMessage[..^1];
        }

        return displayMessage + "...";
    }

    private void RefreshAudioTrackStatus()
    {
        _audioTrackStatusText = _playback.IsMediaOpen
            ? GetAudioTrackStatus(_playback.AudioTracks, _playback.ActiveAudioTrackId) : string.Empty;

        UpdateEditorDecoderStatus();
    }

    internal static string GetAudioTrackStatus(IReadOnlyList<PlaybackAudioTrack> tracks, int? activeId)
    {
        var track = tracks.FirstOrDefault(candidate => candidate.Id == activeId);
        if (track is not { HasDetails: true }) return string.Empty;
        var codec = MediaCodecDisplay.AudioName(track.CodecDescription, track.CodecVariant, detailed: false);
        var channels = MediaCodecDisplay.EffectiveLayout(track.EffectiveChannelLayout);
        if (string.IsNullOrWhiteSpace(channels)) channels = track.Channels > 0 ? $"{track.Channels} ch" : null;
        return !string.IsNullOrWhiteSpace(codec) && !string.IsNullOrWhiteSpace(channels) ? $"{codec} {channels}" : string.Empty;
    }
}
