using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private readonly StatusCounterWidth _mDvdCounter = new('{', '}', 102);
    private readonly StatusCounterWidth _mpl2Counter = new('[', ']', 92);

    private void UpdateStatusBar(long position, long length)
    {
        var frameRate = GetVideoFrameRate();
        _mDVDStatus.Text = _mDvdCounter.Update(_currentVideoPath, ToMicroDvdFrame(position, frameRate), ToMicroDvdFrame(length, frameRate));
        _mpl2Status.Text = _mpl2Counter.Update(_currentVideoPath, ToMpl2Time(position), ToMpl2Time(length));
        _positionStatus.Text = FormatStatusTime(position);
        _lengthStatus.Text = FormatStatusTime(length);

        if (_mDVDStatus.Width != _mDvdCounter.Measure(_mDVDStatus) ||
            _mpl2Status.Width != _mpl2Counter.Measure(_mpl2Status))
        {
            _statusStrip.ApplyWidths();
            LayoutEditorStatusDetails();
            RefreshFilePathStatus();
        }

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

    private sealed class StatusCounterWidth(char open, char close, int idleWidth)
    {
        private string? _videoPath;
        private string _position = "0";
        private string _end = "0";
        private int _positionDigits = 1;
        private (string End, int PositionDigits, Font Font, int Dpi)? _measuredFor;
        private int _width;

        public string Update(string? videoPath, long position, long end)
        {
            _position = position.ToString();
            var endText = end.ToString();
            if (_videoPath != videoPath || _end != endText)
            {
                _videoPath = videoPath;
                _end = endText;
                _positionDigits = _end.Length;
                _measuredFor = null;
            }

            // A still-unknown duration can grow past its previous digit range.
            _positionDigits = Math.Max(_positionDigits, _position.Length);
            return Format(_position);
        }

        public int Measure(ToolStripStatusLabel cell)
        {
            var dpi = cell.Owner?.DeviceDpi ?? 96;
            var key = (_end, _positionDigits, cell.Font, dpi);
            if (_measuredFor == key)
            {
                return _width;
            }

            // Use the native label measurement, including the text inset and border,
            // without replacing the text of the visible cell.
            using var sample = new ToolStripStatusLabel
            {
                Font = cell.Font,
                BorderSides = cell.BorderSides,
                Padding = cell.Padding
            };
            _width = Math.Max(MeasureText(_position), MeasureText(_end));
            for (var digit = '0'; digit <= '9'; digit++)
            {
                // Measure complete pairs for fonts with unequal digit advances.
                _width = Math.Max(_width, MeasureText(new string(digit, _positionDigits)));
            }

            if (_end == "0" && _positionDigits == 1)
            {
                _width = Math.Max(_width, (int)Math.Round(idleWidth * dpi / 96d));
            }

            _measuredFor = key;
            return _width;

            int MeasureText(string position)
            {
                sample.Text = Format(position);
                return sample.GetPreferredSize(Size.Empty).Width;
            }
        }

        private string Format(string position) => $"{open}{position}{close}{open}{_end}{close}";
    }

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
        var path = _currentVideoPath ?? GetRememberedVideoPath();
        if (path is null)
        {
            _fileStatus.ForeColor = SystemColors.ControlText;
            _fileStatus.ToolTipText = string.Empty;
            _fileStatus.Text = string.Empty;
            return;
        }

        SetFilePathStatus(path);
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
        if (_fileStatusExpiresAt == default && _currentVideoPath is null &&
            GetRememberedVideoPath() is { } path)
        {
            SetFilePathStatus(path);
        }
    }

    private string? GetRememberedVideoPath()
    {
        var path = _settings.Playback.LastVideoPath;
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    private void SetFilePathStatus(string fullPath)
    {
        _fileStatus.ForeColor = SystemColors.ControlText;
        _fileStatus.ToolTipText = fullPath;
        // The content rectangle includes padding and extends to the status label's border.
        var availableWidth = Math.Max(0, _fileStatus.ContentRectangle.Width - _fileStatus.Padding.Horizontal - 8);
        var displayPath = FitPathToWidth(fullPath, _fileStatus.Font, availableWidth);
        _fileStatus.Text = TextRenderer.MeasureText(displayPath, _fileStatus.Font).Width <= availableWidth
            ? displayPath
            : string.Empty;
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
