using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void UpdateSubtitleCurrentTimeJumpAvailability()
    {
        var canJump = _playback.IsMediaOpen &&
            _subtitleDocument.Cues.Count > 0 &&
            !_isFullScreen &&
            !_splitContainer.Panel2Collapsed;
        _jumpToCurrentTimeSubtitleMenuItem?.Enabled = canJump;
    }

    private void UpdateSubtitleTraceAvailability()
    {
        var canTrace = _playback.IsMediaOpen && _subtitleDocument.Cues.Count > 0 && !_isFullScreen;
        if (!canTrace && _subtitleTraceEnabled)
        {
            SetSubtitleTraceEnabled(false);
        }

        _subtitleTraceMenuItem?.Enabled = canTrace;
        ToolbarButton("toolbarSubtitleTrace").Enabled = canTrace;
    }

    private void ToggleSubtitleTrace() => SetSubtitleTraceEnabled(!_subtitleTraceEnabled);

    private void PrepareForSelectionSensitiveSubtitleEdit()
    {
        if (_subtitleTraceEnabled)
        {
            SetSubtitleTraceEnabled(false);
        }
    }

    private void SetSubtitleTraceEnabled(bool enabled)
    {
        if (enabled && (!_playback.IsMediaOpen || _subtitleDocument.Cues.Count == 0 || _isFullScreen))
        {
            return;
        }

        _subtitleTraceEnabled = enabled;
        _tracedSubtitleLineIndex = -1;
        UpdateSubtitleTraceEditorReadOnly();
        if (_subtitleTraceMenuItem is not null)
        {
            _subtitleTraceMenuItem.Checked = enabled;
        }

        ToolbarButton("toolbarSubtitleTrace").Checked = enabled;
        if (!enabled)
        {
            _subtitleEditor.ClearTraceSelection();
            return;
        }

        if (_splitContainer.Panel2Collapsed)
        {
            _splitContainer.Panel2Collapsed = false;
            ToolbarButton("toolbarEditor").Checked = true;
        }

        if (!_subtitlesVisible)
        {
            ToggleSubtitleVisibility();
        }

        var position = _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds;
        UpdateSubtitleTrace(Math.Max(0, position));
    }

    private void UpdateSubtitleTrace(long positionMilliseconds)
    {
        if (!_subtitleTraceEnabled)
        {
            return;
        }

        if (_playback.State != PlaybackState.Playing)
        {
            ClearSubtitleTraceSelection();
            return;
        }

        if (!TryGetSubtitleTraceSelection(positionMilliseconds, out var traceSelection))
        {
            if (_tracedSubtitleLineIndex >= 0)
            {
                ClearSubtitleTraceSelection();
            }

            return;
        }

        // A click in another editor line is intentional. Keep its caret until
        // Ctrl+P is used or playback reaches a different subtitle.
        if (traceSelection.LineIndex == _tracedSubtitleLineIndex)
        {
            return;
        }

        ApplySubtitleTraceSelection(traceSelection);
    }

    private void ApplySubtitleTraceSelection(SubtitleTraceSelection traceSelection)
    {
        _tracedSubtitleLineIndex = traceSelection.LineIndex;
        _subtitleEditor.SetTraceSelection(traceSelection.TextStart, traceSelection.TextEnd);
        ScrollTraceLineToFourthRow(traceSelection.LineIndex);
    }

    private void ClearSubtitleTraceSelection()
    {
        if (_tracedSubtitleLineIndex < 0)
        {
            return;
        }

        _tracedSubtitleLineIndex = -1;
        _subtitleEditor.ClearTraceSelection();
    }

    private void UpdateSubtitleTraceEditorReadOnly()
        => _subtitleEditor.ReadOnly = _subtitleTraceEnabled && _playback.State == PlaybackState.Playing;

    private bool TryGetSubtitleTraceSelection(long positionMilliseconds, out SubtitleTraceSelection selection)
    {
        selection = default;
        var frameRate = GetVideoFrameRate();
        if (SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text))
        {
            return TryGetSrtTraceSelection(positionMilliseconds, frameRate, out selection);
        }

        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var line = _subtitleEditor.Lines[lineIndex];
            var lineText = line.Text.TrimEnd('\r', '\n');
            if (SubtitleDocument.TryGetInlineCue(
                    lineText,
                    frameRate,
                    out var startMilliseconds,
                    out var endMilliseconds,
                    out var text) ||
                SubtitleDocument.TryGetTmPlayerCue(lineText, out startMilliseconds, out text))
            {
                if (endMilliseconds <= startMilliseconds)
                {
                    endMilliseconds = FindNextTraceCueStart(lineIndex + 1, startMilliseconds, frameRate) ??
                        startMilliseconds + 3_000;
                }

                if (positionMilliseconds < startMilliseconds || positionMilliseconds > endMilliseconds)
                {
                    continue;
                }

                var textStart = line.Position + lineText.Length - text.Length;
                selection = new SubtitleTraceSelection(lineIndex, textStart, line.Position + lineText.Length);
                return true;
            }
        }

        return false;
    }

    private bool TryGetSrtTraceSelection(long positionMilliseconds, double frameRate, out SubtitleTraceSelection selection)
    {
        selection = default;
        for (var timingLineIndex = 0; timingLineIndex < _subtitleEditor.Lines.Count; timingLineIndex++)
        {
            var timingLine = _subtitleEditor.Lines[timingLineIndex];
            if (!SubtitleDocument.TryGetCueTiming(
                    timingLine.Text.TrimEnd('\r', '\n'),
                    frameRate,
                    out var startMilliseconds,
                    out var endMilliseconds) ||
                positionMilliseconds < startMilliseconds || positionMilliseconds > endMilliseconds)
            {
                continue;
            }

            return TryGetSrtTraceSelectionForTimingLine(timingLineIndex, out selection);
        }

        return false;
    }

    private bool TryGetTraceSelectionForTimingLine(
        int timingLineIndex,
        double frameRate,
        out SubtitleTraceSelection selection)
    {
        selection = default;
        if (SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text))
        {
            return TryGetSrtTraceSelectionForTimingLine(timingLineIndex, out selection);
        }

        var line = _subtitleEditor.Lines[timingLineIndex];
        var lineText = line.Text.TrimEnd('\r', '\n');
        if (!SubtitleDocument.TryGetInlineCue(lineText, frameRate, out _, out _, out var text) &&
            !SubtitleDocument.TryGetTmPlayerCue(lineText, out _, out text))
        {
            return false;
        }

        var textStart = line.Position + lineText.Length - text.Length;
        selection = new SubtitleTraceSelection(timingLineIndex, textStart, line.Position + lineText.Length);
        return true;
    }

    private bool TryGetFirstTraceSelectionAtOrAfter(
        long positionMilliseconds,
        out SubtitleTraceSelection selection)
    {
        selection = default;
        var frameRate = GetVideoFrameRate();
        var firstStartMilliseconds = long.MaxValue;
        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var lineText = _subtitleEditor.Lines[lineIndex].Text.TrimEnd('\r', '\n');
            if (!SubtitleDocument.TryGetCueStartMilliseconds(lineText, frameRate, out var startMilliseconds) ||
                startMilliseconds < positionMilliseconds ||
                startMilliseconds >= firstStartMilliseconds ||
                !TryGetTraceSelectionForTimingLine(lineIndex, frameRate, out var candidate))
            {
                continue;
            }

            firstStartMilliseconds = startMilliseconds;
            selection = candidate;
        }

        return firstStartMilliseconds != long.MaxValue;
    }

    private void PositionSubtitleTraceAfterTimeChange(long positionMilliseconds, bool playbackIsStarting)
    {
        if (!_subtitleTraceEnabled)
        {
            return;
        }

        if (_tracedSubtitleLineIndex >= 0)
        {
            ClearSubtitleTraceSelection();
        }

        if ((playbackIsStarting || _playback.State == PlaybackState.Playing) &&
            TryGetSubtitleTraceSelection(positionMilliseconds, out var activeSelection))
        {
            ApplySubtitleTraceSelection(activeSelection);
            return;
        }

        if (!TryGetFirstTraceSelectionAtOrAfter(positionMilliseconds, out var nextSelection))
        {
            return;
        }

        _subtitleEditor.SetCaretPosition(nextSelection.TextStart);
        ScrollTraceLineToFourthRow(nextSelection.LineIndex);
    }

    private void JumpToSubtitleAtCurrentTime()
    {
        if (!_playback.IsMediaOpen || _subtitleDocument.Cues.Count == 0 || _isFullScreen || _splitContainer.Panel2Collapsed)
        {
            return;
        }

        var position = _isStopped ? _stoppedSeekPosition : Math.Max(0, _playback.PositionMilliseconds);
        var hasActiveSubtitle = TryGetSubtitleTraceSelection(position, out var selection);
        if (!hasActiveSubtitle && !TryGetFirstTraceSelectionAtOrAfter(position, out selection))
        {
            return;
        }

        if (_subtitleTraceEnabled && _playback.State == PlaybackState.Playing && hasActiveSubtitle)
        {
            ApplySubtitleTraceSelection(selection);
        }
        else
        {
            ClearSubtitleTraceSelection();
            _subtitleEditor.SetCaretPosition(selection.TextStart);
            ScrollTraceLineToFourthRow(selection.LineIndex);
        }

        _subtitleEditor.Focus();
    }

    private void PositionCaretAtFirstSubtitle()
    {
        if (!TryGetFirstTraceSelectionAtOrAfter(0, out var firstSelection))
        {
            _subtitleEditor.SetCaretPosition(0);
            _subtitleEditor.Focus();
            return;
        }

        _subtitleEditor.SetCaretPosition(firstSelection.TextStart);
        _subtitleEditor.ScrollCaret();
        _subtitleEditor.Focus();
    }

    private void PositionCaretAfterSubtitleLoad()
    {
        var position = _isStopped ? _stoppedSeekPosition : Math.Max(0, _playback.PositionMilliseconds);
        var hasActiveSubtitle = TryGetSubtitleTraceSelection(position, out var selection);
        if (!hasActiveSubtitle && !TryGetFirstTraceSelectionAtOrAfter(position, out selection))
        {
            PositionCaretAtFirstSubtitle();
            return;
        }

        if (_subtitleTraceEnabled && _playback.State == PlaybackState.Playing && hasActiveSubtitle)
        {
            ApplySubtitleTraceSelection(selection);
        }
        else
        {
            ClearSubtitleTraceSelection();
            _subtitleEditor.SetCaretPosition(selection.TextStart);
            ScrollTraceLineToFourthRow(selection.LineIndex);
        }

        _subtitleEditor.Focus();
    }

    private void ResumeSubtitleTraceAfterSubtitleLoad()
    {
        if (!_subtitleTraceEnabled || IsDisposed)
        {
            return;
        }

        _tracedSubtitleLineIndex = -1;
        var position = _isStopped ? _stoppedSeekPosition : Math.Max(0, _playback.PositionMilliseconds);
        PositionSubtitleTraceAfterTimeChange(
            position,
            playbackIsStarting: _playback.State == PlaybackState.Playing);
    }

    private bool TryGetSrtTraceSelectionForTimingLine(
        int timingLineIndex,
        out SubtitleTraceSelection selection)
    {
        selection = default;
        var textStartLineIndex = timingLineIndex + 1;
        while (textStartLineIndex < _subtitleEditor.Lines.Count &&
               string.IsNullOrWhiteSpace(_subtitleEditor.Lines[textStartLineIndex].Text))
        {
            textStartLineIndex++;
        }

        if (textStartLineIndex >= _subtitleEditor.Lines.Count)
        {
            return false;
        }

        var textEndLineIndex = textStartLineIndex;
        while (textEndLineIndex + 1 < _subtitleEditor.Lines.Count &&
               !string.IsNullOrWhiteSpace(_subtitleEditor.Lines[textEndLineIndex + 1].Text))
        {
            textEndLineIndex++;
        }

        var textStartLine = _subtitleEditor.Lines[textStartLineIndex];
        var textEndLine = _subtitleEditor.Lines[textEndLineIndex];
        var textEnd = textEndLine.Position + textEndLine.Text.TrimEnd('\r', '\n').Length;
        selection = new SubtitleTraceSelection(textStartLineIndex, textStartLine.Position, textEnd);
        return true;
    }

    private long? FindNextTraceCueStart(int firstLineIndex, long currentStartMilliseconds, double frameRate)
    {
        for (var lineIndex = firstLineIndex; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            if (SubtitleDocument.TryGetCueStartMilliseconds(
                    _subtitleEditor.Lines[lineIndex].Text.TrimEnd('\r', '\n'),
                    frameRate,
                    out var startMilliseconds) &&
                startMilliseconds > currentStartMilliseconds)
            {
                return startMilliseconds;
            }
        }

        return null;
    }

    private void ScrollTraceLineToFourthRow(int lineIndex)
    {
        var line = _subtitleEditor.Lines[Math.Clamp(lineIndex, 0, _subtitleEditor.Lines.Count - 1)];
        _subtitleEditor.ScrollCaret();
        var firstVisibleLine = _subtitleEditor.FirstVisibleLine;
        var targetFirstVisibleLine = Math.Max(0, line.DisplayIndex - 3);
        _subtitleEditor.LineScroll(targetFirstVisibleLine - firstVisibleLine, 0);
    }

    private readonly record struct SubtitleTraceSelection(int LineIndex, int TextStart, int TextEnd);
}
