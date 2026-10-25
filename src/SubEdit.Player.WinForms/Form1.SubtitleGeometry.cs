using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

internal readonly record struct SubtitleGeometryMeasurement(
    int LineIndex,
    string LineText,
    double SubtitleTimeMilliseconds,
    double VideoTimeMilliseconds);

public partial class Form1
{
    private SubtitleGeometryMeasurement? _initialSubtitleGeometryMeasurement;
    private SubtitleGeometryMeasurement? _finalSubtitleGeometryMeasurement;
    private bool _geometryChangeFromInitialLine;
    private bool _geometryChangeToFinalLine;
    private long? _recentEditorPlaybackTarget;
    private long _recentEditorPlaybackTargetSetAt;

    private void MeasureSubtitleGeometryPoint(bool isInitial)
    {
        if (!_playback.IsMediaOpen || string.IsNullOrWhiteSpace(_currentVideoPath) ||
            _subtitleDocument.Cues.Count == 0 || _subtitleEditor.Lines.Count == 0)
        {
            return;
        }

        var frameRate = GetVideoFrameRate();
        var lineIndex = Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1);
        if (!SubtitleDocument.TryGetCueStartMilliseconds(
                _subtitleEditor.Lines[lineIndex].Text,
                frameRate,
                out var subtitleTimeMilliseconds))
        {
            lineIndex = FindSrtTimingLine(lineIndex, frameRate);
            if (lineIndex < 0 ||
                !SubtitleDocument.TryGetCueStartMilliseconds(
                    _subtitleEditor.Lines[lineIndex].Text,
                    frameRate,
                    out subtitleTimeMilliseconds))
            {
                return;
            }
        }

        var measurement = new SubtitleGeometryMeasurement(
            lineIndex,
            _subtitleEditor.Lines[lineIndex].Text.TrimEnd('\r', '\n'),
            subtitleTimeMilliseconds,
            GetSubtitleGeometryVideoPosition());
        if (isInitial)
        {
            _initialSubtitleGeometryMeasurement = measurement;
        }
        else
        {
            _finalSubtitleGeometryMeasurement = measurement;
        }

        UpdateSubtitleGeometryAvailability();
        if (CanApplyGeometricSubtitleShift())
        {
            ShowGeometricSubtitleShiftDialog();
        }
    }

    private void ShowGeometricSubtitleShiftDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        if (!CanApplyGeometricSubtitleShift())
        {
            return;
        }

        var initial = _initialSubtitleGeometryMeasurement!.Value;
        var final = _finalSubtitleGeometryMeasurement!.Value;
        var framesPerSecond = GetVideoFrameRate();
        using var dialog = new GeometricSubtitleShiftDialog(
            initial,
            final,
            framesPerSecond,
            _geometryChangeFromInitialLine,
            _geometryChangeToFinalLine);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _geometryChangeFromInitialLine = dialog.ChangeFromInitialLine;
        _geometryChangeToFinalLine = dialog.ChangeToFinalLine;

        var originalText = _subtitleEditor.Text;
        var hasSelection = HasSubtitleSelection();
        var isEntireDocumentSelected = IsEntireSubtitleDocumentSelected();
        var hasScopeOverride = dialog.ChangeFromInitialLine || dialog.ChangeToFinalLine;
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var (firstLine, lastLine) = GetSelectedSubtitleConversionLineRange(lastDocumentLine);
        if (dialog.ChangeFromInitialLine)
        {
            firstLine = initial.LineIndex;
        }
        if (dialog.ChangeToFinalLine)
        {
            lastLine = final.LineIndex;
        }
        if (lastLine < firstLine)
        {
            return;
        }

        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var result = GeometricSubtitleShiftProcessor.Shift(
            originalText,
            initial.SubtitleTimeMilliseconds,
            initial.SubtitleTimeMilliseconds + (dialog.InitialDelaySeconds * 1000d),
            final.SubtitleTimeMilliseconds,
            final.SubtitleTimeMilliseconds + (dialog.FinalDelaySeconds * 1000d),
            framesPerSecond,
            firstLine,
            lastLine);

        if (result.Text != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
            var updatedLastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
            var isFullDocument = !hasScopeOverride
                ? IsFullSubtitleDocumentRange(firstLine, lastLine, updatedLastDocumentLine, hasSelection, isEntireDocumentSelected)
                : firstLine == 0 && lastLine == updatedLastDocumentLine;
            RegisterSubtitleTimeScaleTransform(result.Scale, "Korekta geometryczna", isFullDocument);
        }

        _subtitleEditor.Focus();
        if (result.HasNegativeTime)
        {
            MessageBox.Show(
                this,
                "Wystąpiła przynajmniej jedna linia z ujemną wartością.",
                "Ostrzeżenie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private bool CanApplyGeometricSubtitleShift() =>
        _initialSubtitleGeometryMeasurement is { } initial &&
        _finalSubtitleGeometryMeasurement is { } final &&
        final.SubtitleTimeMilliseconds > initial.SubtitleTimeMilliseconds &&
        final.VideoTimeMilliseconds > initial.VideoTimeMilliseconds;

    private double GetSubtitleGeometryVideoPosition()
    {
        if (_recentEditorPlaybackTarget is { } targetPosition)
        {
            var elapsed = Math.Max(0, Environment.TickCount64 - _recentEditorPlaybackTargetSetAt);
            _recentEditorPlaybackTarget = null;
            if (elapsed <= 1_500)
            {
                return Math.Max(
                    0,
                    targetPosition + (_playback.State == PlaybackState.Playing ? elapsed : 0));
            }
        }

        return Math.Max(0, _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds);
    }

    private void RememberSubtitleGeometryEditorSeek(long targetPosition)
    {
        _recentEditorPlaybackTarget = Math.Max(0, targetPosition);
        _recentEditorPlaybackTargetSetAt = Environment.TickCount64;
    }

    private void ResetSubtitleGeometryMeasurements()
    {
        _initialSubtitleGeometryMeasurement = null;
        _finalSubtitleGeometryMeasurement = null;
        _recentEditorPlaybackTarget = null;
        UpdateSubtitleGeometryAvailability();
    }

    private void UpdateSubtitleGeometryAvailability()
    {
        var canMeasure = _playback.IsMediaOpen &&
            !string.IsNullOrWhiteSpace(_currentVideoPath) &&
            _subtitleDocument.Cues.Count > 0;
        if (_measureInitialSubtitleDelayMenuItem is not null)
        {
            _measureInitialSubtitleDelayMenuItem.Enabled = canMeasure;
        }
        if (_measureFinalSubtitleDelayMenuItem is not null)
        {
            _measureFinalSubtitleDelayMenuItem.Enabled = canMeasure;
        }
        if (_geometricSubtitleShiftMenuItem is not null)
        {
            _geometricSubtitleShiftMenuItem.Enabled = canMeasure && CanApplyGeometricSubtitleShift();
        }
    }
}
