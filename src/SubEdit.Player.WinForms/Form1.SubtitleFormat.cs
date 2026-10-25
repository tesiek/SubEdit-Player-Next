using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ShowSubtitleFormatDialog()
    {
        using var dialog = new SubtitleFormatDialog(GetActiveSubtitleFormat());
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.ActiveSubtitleFormat = dialog.SelectedFormat.ToString();
            UpdateActiveSubtitleFormatIndicator();
        }
    }

    private void UpdateSubtitleConversionAvailability()
    {
        var canConvert = !SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text) &&
            SubtitleTimingFormatter.Detect(_subtitleEditor.Text).HasValue;
        _convertSubtitleFormatMenuItem?.Enabled = canConvert;
        _correctSubtitlesMenuItem?.Enabled = !string.IsNullOrWhiteSpace(_subtitleEditor.Text);
        _goToSubtitleMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _shiftSubtitleTimeMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _changeSubtitleDisplayTimeMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _rapidSubtitleMergeMenuItem?.Enabled = _subtitleDocument.Cues.Count > 1;
        _splitLongSubtitleLinesMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _limitSubtitleLinesMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _changeSubtitleFrameRateMenuItem?.Enabled =
            SubtitleTimingFormatter.Detect(_subtitleEditor.Text) == SubtitleTimingFormat.MicroDvd;
        _sortSubtitlesMenuItem?.Enabled = _subtitleDocument.Cues.Count > 1;
        _addHalfSecondMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _subtractHalfSecondMenuItem?.Enabled = _subtitleDocument.Cues.Count > 0;
        _measureSubtitleDelayMenuItem?.Enabled =
            _playback.IsMediaOpen && _currentVideoPath is not null && _subtitleDocument.Cues.Count > 0;
        UpdateSubtitleGeometryAvailability();
        ToolbarButton("toolbarConvert").Enabled = canConvert;
        ToolbarButton("toolbarAddTime").Enabled = _subtitleDocument.Cues.Count > 0;
        ToolbarButton("toolbarShowTime").Enabled = _subtitleDocument.Cues.Count > 0;
        UpdateSubtitleCurrentTimeJumpAvailability();
        UpdateSubtitleTraceAvailability();
    }

    private SubtitleTimingFormat GetActiveSubtitleFormat() =>
        Enum.TryParse<SubtitleTimingFormat>(_settings.ActiveSubtitleFormat, ignoreCase: true, out var format)
            ? format
            : SubtitleTimingFormat.TmPlayerLong;

    private void UpdateActiveSubtitleFormatIndicator()
    {
        if (_activeSubtitleFormatButton is not null)
        {
            _activeSubtitleFormatButton.ToolTipText = $"Aktywny format napisów: {GetActiveSubtitleFormatDisplayName()} (Ctrl+~)";
        }

        if (_defaultSubtitleFormatMenuItem is not null)
        {
            _defaultSubtitleFormatMenuItem.Text = GetDefaultSubtitleFormatMenuText();
        }
    }

    private string GetDefaultSubtitleFormatMenuText() =>
        $"Domyślny format napisów: {GetActiveSubtitleFormat() switch
        {
            SubtitleTimingFormat.TmPlayerLong => "00:00:00",
            SubtitleTimingFormat.TmPlayerShort => "0:00:00",
            SubtitleTimingFormat.MicroDvd => "{000}{000}",
            SubtitleTimingFormat.Mpl2 => "[000][000]",
            _ => "00:00:00"
        }}";

    private string GetActiveSubtitleFormatDisplayName() => GetActiveSubtitleFormat() switch
    {
        SubtitleTimingFormat.TmPlayerLong => "00:00:00 (TMPlayer)",
        SubtitleTimingFormat.TmPlayerShort => "0:00:00 (TMPlayer)",
        SubtitleTimingFormat.MicroDvd => "{000}{000} (mDVD)",
        SubtitleTimingFormat.Mpl2 => "[000][000] (MPL2)",
        _ => string.Empty
    };
}
