using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ShowVolumeOsd()
    {
        if (!CanShowInformationOsd())
        {
            ClearInformationOsd();
            return;
        }

        _videoOverlay.PlaybackText = string.Empty;
        _playbackOsdExpiresAt = default;
        _videoOverlay.VolumeText = $"Głośność: {_volumeSlider.Value}%";
        _volumeOsdExpiresAt = GetInformationOsdExpiration();
        UpdateOverlayVisibility();
    }

    private void ShowPlaybackOsd(string text)
    {
        if (!CanShowInformationOsd())
        {
            ClearInformationOsd();
            return;
        }

        _seekOsdVisible = false;
        _videoOverlay.VolumeText = string.Empty;
        _volumeOsdExpiresAt = default;
        _videoOverlay.PlaybackText = text;
        _playbackOsdExpiresAt = GetInformationOsdExpiration();
        UpdateOverlayVisibility();
    }

    private void ShowAccumulatedSeekOsd(long offset)
    {
        if (!CanShowInformationOsd())
        {
            ClearInformationOsd();
            return;
        }

        if (!_seekOsdVisible || DateTime.UtcNow >= _playbackOsdExpiresAt)
        {
            _seekOsdOffsetMilliseconds = 0;
        }

        _seekOsdOffsetMilliseconds += offset;
        _seekOsdVisible = true;
        _videoOverlay.VolumeText = string.Empty;
        _volumeOsdExpiresAt = default;
        _videoOverlay.PlaybackText = FormatSeekOsdText(_seekOsdOffsetMilliseconds);
        _playbackOsdExpiresAt = GetInformationOsdExpiration();
        UpdateOverlayVisibility();
    }

    private bool CanShowInformationOsd() =>
        _settings.InformationOsd.Position == InformationOsdPosition.TopRight &&
        (_isFullScreen || _settings.InformationOsd.ShowInWindowedMode);

    private DateTime GetInformationOsdExpiration() =>
        DateTime.UtcNow.AddSeconds(_settings.InformationOsd.DisplayDurationSeconds);

    private void ClearInformationOsd()
    {
        _seekOsdVisible = false;
        _seekOsdOffsetMilliseconds = 0;
        _videoOverlay.PlaybackText = string.Empty;
        _videoOverlay.VolumeText = string.Empty;
        _playbackOsdExpiresAt = default;
        _volumeOsdExpiresAt = default;
        UpdateOverlayVisibility();
    }

    private static string FormatSeekOsdText(long offsetMilliseconds)
    {
        if (offsetMilliseconds == 0)
        {
            return "Pozycja bez zmian";
        }

        var totalSeconds = Math.Abs(offsetMilliseconds) / 1000;
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;
        var time = hours > 0
            ? $"{hours}h {minutes}m {seconds}s"
            : minutes > 0
                ? $"{minutes}m {seconds}s"
                : $"{seconds}s";
        return $"{(offsetMilliseconds > 0 ? "W przód" : "Wstecz")} o {time}";
    }

    private void ShowSubtitlePreviewOsd(string firstLine)
    {
        var preview = SubtitleMarkup.ParseCue(
            $"{firstLine}\nNastępna linia\n01234567890",
            default);
        _videoOverlay.SubtitlePreviewCue = new SubtitleCue(
            0,
            long.MaxValue,
            preview.Lines,
            preview.Position);
        _subtitleFontOsdExpiresAt = DateTime.UtcNow.AddMilliseconds(1500);
        UpdateOverlayVisibility();
    }

    private void ToggleTimeCounterVisibility()
    {
        _settings.TimeDisplayMode = GetNextTimeDisplayMode(_settings.TimeDisplayMode);
        if (_timeCounterMenuItem is not null)
        {
            _timeCounterMenuItem.Checked = _settings.TimeDisplayMode != TimeDisplayMode.Hidden;
        }

        UpdateTimeCounterOsd(_playback.PositionMilliseconds, _playback.DurationMilliseconds, force: true);
        SaveSettingsOrShowWarning();
    }

    internal static TimeDisplayMode GetNextTimeDisplayMode(TimeDisplayMode mode) => mode switch
    {
        TimeDisplayMode.Elapsed => TimeDisplayMode.Remaining,
        TimeDisplayMode.Remaining => TimeDisplayMode.Hidden,
        _ => TimeDisplayMode.Elapsed
    };

    internal static string FormatTimeCounterText(TimeDisplayMode mode, long position, long length) => mode switch
    {
        TimeDisplayMode.Hidden => string.Empty,
        TimeDisplayMode.Remaining => $"-{FormatTime(Math.Max(0, length - position))} / {FormatTime(length)}",
        _ => $"{FormatTime(position)} / {FormatTime(length)}"
    };

    private void UpdateTimeCounterOsd(long position, long length, bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && now - _lastTimeCounterOsdUpdateAt < TimeSpan.FromMilliseconds(50))
        {
            return;
        }

        _lastTimeCounterOsdUpdateAt = now;
        _videoOverlay.OsdText = FormatTimeCounterText(_settings.TimeDisplayMode, position, length);
        UpdateOverlayVisibility();
    }
}
