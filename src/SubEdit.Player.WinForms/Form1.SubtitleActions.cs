using System.Globalization;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ShowSubtitleConversionDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (SubtitleFormatConverter.IsSrtDocument(originalText) ||
            SubtitleTimingFormatter.Detect(originalText) is not { } sourceFormat)
        {
            return;
        }

        using var dialog = new SubtitleConversionDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (firstLine, lastLine) = GetSubtitleConversionLineRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;
        var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(
            text,
            firstLine,
            lastLine);

        if (unrecognizedLineIndices.Count > 0)
        {
            using var unknownLineDialog = new UnknownSubtitleLineDialog();
            if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.Break)
            {
                MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                return;
            }

            if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.DeleteAll)
            {
                DeleteUnrecognizedSubtitleLines(
                    ref text,
                    unrecognizedLineIndices,
                    ref lastLine,
                    ref currentLine);
            }
        }

        var framesPerSecond = GetVideoFrameRate();
        var hasUnsortedCues = SubtitleFormatConverter.HasUnsortedCues(
            text,
            sourceFormat,
            framesPerSecond,
            firstLine,
            lastLine);
        var convertedText = SubtitleFormatConverter.Convert(
            text,
            sourceFormat,
            dialog.SelectedFormat,
            framesPerSecond,
            firstLine,
            lastLine);

        if (convertedText != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(convertedText, viewState with { Line = currentLine, Column = currentColumn });
        }

        _settings.ActiveSubtitleFormat = dialog.SelectedFormat.ToString();
        UpdateActiveSubtitleFormatIndicator();
        _subtitleEditor.Focus();

        if (hasUnsortedCues)
        {
            MessageBox.Show(
                this,
                "Wystąpiły nieposortowane napisy!",
                "Ostrzeżenie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ShowSubtitleCorrectionDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        using var dialog = new SubtitleCorrectionDialog(_settings.SubtitleCorrection);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var options = dialog.CreateSettings();
        var (firstLine, lastLine) = GetSubtitleCorrectionLineRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;
        var treatUnrecognizedAsText = false;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(
                text,
                firstLine,
                lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog(allowTreatAsSubtitleText: true);
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                switch (unknownLineDialog.SelectedAction)
                {
                    case UnknownSubtitleLineAction.Break:
                        MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                        return;
                    case UnknownSubtitleLineAction.TreatAsSubtitleText:
                        treatUnrecognizedAsText = true;
                        break;
                    case UnknownSubtitleLineAction.DeleteAll:
                        {
                            DeleteUnrecognizedSubtitleLines(
                                ref text,
                                unrecognizedLineIndices,
                                ref lastLine,
                                ref currentLine);
                            break;
                        }
                }
            }
        }

        var correctedText = SubtitleCorrectionProcessor.Correct(
            text,
            options,
            GetVideoFrameRate(),
            _settings.SubtitlePlayback.MinimumGapFrames,
            firstLine,
            lastLine,
            treatUnrecognizedAsText);

        if (correctedText != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(correctedText, viewState with { Line = currentLine, Column = currentColumn });
        }

        if (dialog.SaveAsDefault)
        {
            _settings.SubtitleCorrection = options;
        }

        _subtitleEditor.Focus();
    }

    private void ShowMeasuredSubtitleDelayDialog()
    {
        if (!_playback.IsMediaOpen || string.IsNullOrWhiteSpace(_currentVideoPath) ||
            _subtitleDocument.Cues.Count == 0 || _subtitleEditor.Lines.Count == 0 ||
            !TryGetCurrentSubtitleStartMilliseconds(out var subtitleStartMilliseconds))
        {
            return;
        }

        var videoPositionMilliseconds = Math.Max(
            0,
            _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds);
        var measuredDelaySeconds = (videoPositionMilliseconds - subtitleStartMilliseconds) / 1000d;
        if (_settings.Editing.ApplyReactionWhenMeasuringSubtitleDelay)
        {
            measuredDelaySeconds -= _settings.Editing.ReactionSeconds;
        }
        ShowSubtitleTimeShiftDialog(measuredDelaySeconds);
    }

    private bool TryGetCurrentSubtitleStartMilliseconds(out long startMilliseconds)
    {
        startMilliseconds = 0;
        var currentLineIndex = Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1);
        var framesPerSecond = GetVideoFrameRate();
        if (SubtitleDocument.TryGetCueStartMilliseconds(
                _subtitleEditor.Lines[currentLineIndex].Text,
                framesPerSecond,
                out startMilliseconds))
        {
            return true;
        }

        var timingLineIndex = FindSrtTimingLine(currentLineIndex, framesPerSecond);
        return timingLineIndex >= 0 && SubtitleDocument.TryGetCueStartMilliseconds(
            _subtitleEditor.Lines[timingLineIndex].Text,
            framesPerSecond,
            out startMilliseconds);
    }

    private void ShowSubtitleTimeShiftDialog() => ShowSubtitleTimeShiftDialog(measuredDelaySeconds: null);

    private void ShowSubtitleTimeShiftDialog(double? measuredDelaySeconds)
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (_subtitleDocument.Cues.Count == 0)
        {
            return;
        }

        var framesPerSecond = GetVideoFrameRate();
        using var dialog = new SubtitleTimeShiftDialog(framesPerSecond, measuredDelaySeconds);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (firstLine, lastLine) = GetSubtitleTimeShiftLineRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(
                text,
                firstLine,
                lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog();
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.Break)
                {
                    MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.DeleteAll)
                {
                    DeleteUnrecognizedSubtitleLines(
                        ref text,
                        unrecognizedLineIndices,
                        ref lastLine,
                        ref currentLine);
                }
            }
        }

        var result = SubtitleTimeShiftProcessor.Shift(
            text,
            dialog.ShiftSeconds,
            framesPerSecond,
            dialog.ShiftStart,
            dialog.ShiftEnd,
            firstLine,
            lastLine);

        if (result.Text != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
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

    private void ShowSubtitleDisplayTimeDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (_subtitleDocument.Cues.Count == 0)
        {
            return;
        }

        var framesPerSecond = GetVideoFrameRate();
        using var dialog = new SubtitleDisplayTimeDialog(framesPerSecond, _settings.SubtitleDisplayTime);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (firstLine, lastLine) = GetSubtitleDisplayTimeLineRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(
                text,
                firstLine,
                lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog();
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.Break)
                {
                    MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.DeleteAll)
                {
                    DeleteUnrecognizedSubtitleLines(
                        ref text,
                        unrecognizedLineIndices,
                        ref lastLine,
                        ref currentLine);
                }
            }
        }

        var options = dialog.CreateSettings();
        var changedText = SubtitleDisplayTimeProcessor.Change(
            text,
            options,
            dialog.ClearEndTimes,
            framesPerSecond,
            _settings.SubtitlePlayback.MinimumGapFrames,
            firstLine,
            lastLine);

        if (changedText != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(changedText, viewState with { Line = currentLine, Column = currentColumn });
        }

        if (dialog.SaveAsDefault)
        {
            _settings.SubtitleDisplayTime = options;
        }

        _subtitleEditor.Focus();
    }

    private void ShowRapidSubtitleMergeDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (_subtitleDocument.Cues.Count < 2)
        {
            return;
        }

        using var dialog = new RapidSubtitleMergeDialog(_settings.RapidSubtitleMerge);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (firstLine, lastLine) = GetRapidSubtitleMergeLineRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var result = RapidSubtitleMergeProcessor.Merge(
            originalText,
            dialog.CreateSettings(),
            GetVideoFrameRate(),
            firstLine,
            lastLine);

        if (result.Text != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
        }

        if (dialog.SaveAsDefault)
        {
            _settings.RapidSubtitleMerge = dialog.CreateSettings();
        }

        MessageBox.Show(
            this,
            $"Ilość połączonych napisów: {result.MergedCount}",
            "Informacja",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        _subtitleEditor.Focus();
    }

    private void ShowLongSubtitleLineSplitDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        using var dialog = new LongSubtitleLineSplitDialog(_settings.LongSubtitleLineSplit);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var options = dialog.CreateSettings();
        var (firstLine, lastLine) = GetLongSubtitleLineSplitRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;
        var treatUnrecognizedAsText = false;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(text, firstLine, lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog(allowTreatAsSubtitleText: true);
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                switch (unknownLineDialog.SelectedAction)
                {
                    case UnknownSubtitleLineAction.Break:
                        MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                        return;
                    case UnknownSubtitleLineAction.TreatAsSubtitleText:
                        treatUnrecognizedAsText = true;
                        break;
                    case UnknownSubtitleLineAction.DeleteAll:
                        {
                            DeleteUnrecognizedSubtitleLines(
                                ref text,
                                unrecognizedLineIndices,
                                ref lastLine,
                                ref currentLine);
                            break;
                        }
                }
            }
        }

        var result = LongSubtitleLineSplitProcessor.Process(
            text,
            options,
            dialog.SplitLongLines,
            firstLine,
            lastLine,
            treatUnrecognizedAsText);

        if (result.Text != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
        }

        if (dialog.SplitLongLines && dialog.SaveAsDefault)
        {
            _settings.LongSubtitleLineSplit = options;
        }

        MessageBox.Show(
            this,
            dialog.SplitLongLines
                ? $"Ilość podzielonych wierszy: {result.SplitLineCount}"
                : $"Ilość usuniętych znaków podziału: {result.RemovedSplitterCount}",
            "Informacja",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        _subtitleEditor.Focus();
    }

    private void ShowSubtitleLineLimitDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (string.IsNullOrWhiteSpace(originalText))
        {
            return;
        }

        using var dialog = new SubtitleLineLimitDialog(_settings.SubtitleLineLimit);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var (firstLine, lastLine) = GetSubtitleLineLimitRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;
        var treatUnrecognizedAsText = false;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(text, firstLine, lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog(allowTreatAsSubtitleText: true);
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                switch (unknownLineDialog.SelectedAction)
                {
                    case UnknownSubtitleLineAction.Break:
                        MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                        return;
                    case UnknownSubtitleLineAction.TreatAsSubtitleText:
                        treatUnrecognizedAsText = true;
                        break;
                    case UnknownSubtitleLineAction.DeleteAll:
                        {
                            DeleteUnrecognizedSubtitleLines(
                                ref text,
                                unrecognizedLineIndices,
                                ref lastLine,
                                ref currentLine);
                            break;
                        }
                }
            }
        }

        var result = SubtitleLineLimitProcessor.Limit(
            text,
            dialog.MaximumLines,
            firstLine,
            lastLine,
            treatUnrecognizedAsText);

        if (result.Text != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
        }

        if (dialog.SaveAsDefault)
        {
            _settings.SubtitleLineLimit = new SubtitleLineLimitSettings { MaximumLines = dialog.MaximumLines };
        }

        MessageBox.Show(
            this,
            $"Ilość zmodyfikowanych linii: {result.ModifiedLineCount}",
            "Informacja",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        _subtitleEditor.Focus();
    }

    private void ShowSubtitleFrameRateDialog()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (SubtitleTimingFormatter.Detect(originalText) != SubtitleTimingFormat.MicroDvd)
        {
            return;
        }

        using var dialog = new SubtitleFrameRateDialog(GetLoadedVideoFrameRate());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var hasSelection = HasSubtitleSelection();
        var isEntireDocumentSelected = IsEntireSubtitleDocumentSelected();
        var (firstLine, lastLine) = GetSubtitleFrameRateRange(dialog.SelectedScope);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;
        var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(text, firstLine, lastLine);
        if (unrecognizedLineIndices.Count > 0)
        {
            using var unknownLineDialog = new UnknownSubtitleLineDialog();
            if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.Break)
            {
                MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                return;
            }

            if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.DeleteAll)
            {
                DeleteUnrecognizedSubtitleLines(
                    ref text,
                    unrecognizedLineIndices,
                    ref lastLine,
                    ref currentLine);
            }
        }

        var changedText = SubtitleFrameRateProcessor.Change(
            text,
            dialog.CurrentFrameRate,
            dialog.TargetFrameRate,
            firstLine,
            lastLine);
        if (changedText != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(changedText, viewState with { Line = currentLine, Column = currentColumn });
            var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
            var isFullDocument = dialog.SelectedScope == SubtitleFrameRateScope.Selection
                ? IsFullSubtitleDocumentRange(firstLine, lastLine, lastDocumentLine, hasSelection, isEntireDocumentSelected)
                : firstLine == 0 && lastLine == lastDocumentLine;
            var scale = SubtitleFrameRateProcessor.CalculateScale(dialog.CurrentFrameRate, dialog.TargetFrameRate);
            RegisterSubtitleTimeScaleTransform(
                scale,
                $"Zmiana FPS: {dialog.CurrentFrameRate.ToString("0.###", CultureInfo.InvariantCulture)} → {dialog.TargetFrameRate.ToString("0.###", CultureInfo.InvariantCulture)}",
                isFullDocument);
        }

        _subtitleEditor.Focus();
    }

    private void ShowDefaultFrameRateDialog()
    {
        using var dialog = new DefaultFrameRateDialog(_sessionFrameRate, GetLoadedVideoFrameRate());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _sessionFrameRate = dialog.FrameRate;
        _sessionFrameRateOverridden = true;
        _subtitleEditor.SetFramesPerSecond(GetVideoFrameRate());
        RefreshSubtitleDocument();
        RefreshSubtitleInfoBar();
        UpdateFrameRateStatus();
        _subtitleEditor.Focus();
    }

    private void SortSubtitles()
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        var originalText = _subtitleEditor.Text;
        if (_subtitleDocument.Cues.Count < 2)
        {
            return;
        }

        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var (firstLine, lastLine) = GetSelectedSubtitleConversionLineRange(lastDocumentLine);
        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var text = originalText;

        if (!SubtitleFormatConverter.IsSrtDocument(text))
        {
            var unrecognizedLineIndices = SubtitleFormatConverter.GetUnrecognizedLineIndices(text, firstLine, lastLine);
            if (unrecognizedLineIndices.Count > 0)
            {
                using var unknownLineDialog = new UnknownSubtitleLineDialog();
                if (unknownLineDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.Break)
                {
                    MoveSubtitleCaretToLine(unrecognizedLineIndices[0]);
                    return;
                }

                if (unknownLineDialog.SelectedAction == UnknownSubtitleLineAction.DeleteAll)
                {
                    DeleteUnrecognizedSubtitleLines(
                        ref text,
                        unrecognizedLineIndices,
                        ref lastLine,
                        ref currentLine);
                }
            }
        }

        using var sortConfirmation = new QuestionDialog(
            "Posortować napisy rosnąco?",
            includeCancel: false,
            yesText: "OK",
            noText: "Anuluj");
        if (sortConfirmation.ShowDialog(this) != DialogResult.Yes)
        {
            return;
        }

        var sortedText = SubtitleSortProcessor.Sort(
            text,
            GetVideoFrameRate(),
            firstLine,
            lastLine);
        if (sortedText != originalText)
        {
            _subtitleEditor.ApplyFullDocumentEdit(sortedText, viewState with { Line = currentLine, Column = currentColumn });
        }

        _subtitleEditor.Focus();
    }

    private void ShiftCurrentSubtitleByHalfSecond(double shiftSeconds)
    {
        if (_subtitleDocument.Cues.Count == 0 || _subtitleEditor.Lines.Count == 0)
        {
            return;
        }

        var framesPerSecond = GetVideoFrameRate();
        var editorLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1);
        if (_splitContainer.Panel2Collapsed)
        {
            var position = _isStopped ? _stoppedSeekPosition : Math.Max(0, _playback.PositionMilliseconds);
            if (!TryGetSubtitleTraceSelection(position, out var currentSubtitle))
            {
                return;
            }

            editorLine = currentSubtitle.LineIndex;
        }

        var timingLine = SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text)
            ? FindSrtTimingLine(editorLine, framesPerSecond)
            : editorLine;
        if (timingLine < 0)
        {
            return;
        }

        var timingEditorLine = _subtitleEditor.Lines[timingLine];
        var originalTimingText = timingEditorLine.Text.TrimEnd('\r', '\n');
        var caretLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1);
        var caretColumn = Math.Clamp(
            _subtitleEditor.CurrentPosition - _subtitleEditor.Lines[caretLine].Position,
            0,
            _subtitleEditor.Lines[caretLine].Text.Length);
        var result = SubtitleTimeShiftProcessor.Shift(
            originalTimingText,
            shiftSeconds,
            framesPerSecond,
            shiftStart: true,
            shiftEnd: true,
            0,
            0);
        if (result.Text != originalTimingText)
        {
            _subtitleEditor.ExecuteWritableMutation(() =>
            {
                _subtitleEditor.BeginUndoAction();
                try
                {
                    _subtitleEditor.TargetStart = timingEditorLine.Position;
                    _subtitleEditor.TargetEnd = timingEditorLine.Position + originalTimingText.Length;
                    _subtitleEditor.ReplaceTarget(result.Text);
                    var restoredLine = _subtitleEditor.Lines[Math.Clamp(caretLine, 0, _subtitleEditor.Lines.Count - 1)];
                    var restoredPosition = restoredLine.Position + Math.Min(caretColumn, restoredLine.Text.Length);
                    _subtitleEditor.SetCaretPosition(restoredPosition);
                    _subtitleEditor.ScrollCaret();
                    _subtitleEditor.Update();
                }
                finally
                {
                    _subtitleEditor.EndUndoAction();
                }
            });
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

    private void MoveSubtitleCaretToLine(int lineIndex)
    {
        var line = _subtitleEditor.Lines[Math.Clamp(lineIndex, 0, _subtitleEditor.Lines.Count - 1)];
        _subtitleEditor.SetCaretPosition(line.Position);
        _subtitleEditor.Focus();
        CenterSubtitleEditorOnCaret();
    }

    private static void DeleteUnrecognizedSubtitleLines(
        ref string text,
        IEnumerable<int> unrecognizedLineIndices,
        ref int lastLine,
        ref int currentLine)
    {
        var deletedLines = unrecognizedLineIndices.ToHashSet();
        var originalCurrentLine = currentLine;
        text = SubtitleFormatConverter.DeleteLines(text, deletedLines);
        lastLine -= deletedLines.Count;
        currentLine -= deletedLines.Count(lineIndex => lineIndex < originalCurrentLine);
    }

    private void CenterSubtitleEditorOnCaret()
    {
        _subtitleEditor.ScrollCaret();
        _subtitleEditor.ExecuteCmd(ScintillaNET.Command.VerticalCenterCaret);
    }

    private void InsertVideoPosition(bool insertStart)
    {
        if (_splitContainer.Panel2Collapsed || !_subtitleEditor.Focused || string.IsNullOrWhiteSpace(_currentVideoPath))
        {
            return;
        }

        var videoPosition = Math.Max(0, _playback.PositionMilliseconds);
        if (insertStart || _settings.Editing.ApplyReactionToEndFrame)
        {
            videoPosition = Math.Max(0, videoPosition - GetReactionMilliseconds());
        }

        if (SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text))
        {
            var timingLineIndex = FindSrtTimingLine(_subtitleEditor.CurrentLine, GetVideoFrameRate());
            if (timingLineIndex < 0 || timingLineIndex >= _subtitleEditor.Lines.Count)
            {
                return;
            }

            var timingEditorLine = _subtitleEditor.Lines[timingLineIndex];
            var originalTimingText = timingEditorLine.Text.TrimEnd('\r', '\n');
            var srtPreserveDuration = insertStart && _settings.Editing.PreserveSubtitleDurationWhenInsertingStartFrame;

            if (!SubtitleTimingFormatter.TryUpdateSrtTimingLine(
                originalTimingText,
                videoPosition,
                insertStart,
                srtPreserveDuration,
                out var newTimingText))
            {
                return;
            }

            if (newTimingText == originalTimingText)
            {
                return;
            }

            var srtOldPosition = Math.Clamp(_subtitleEditor.CurrentPosition, _subtitleEditor.Lines[_subtitleEditor.CurrentLine].Position, _subtitleEditor.Lines[_subtitleEditor.CurrentLine].EndPosition);
            var caretLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1);
            var caretColumn = Math.Clamp(
                _subtitleEditor.CurrentPosition - _subtitleEditor.Lines[caretLine].Position,
                0,
                _subtitleEditor.Lines[caretLine].Text.Length);

            _subtitleEditor.ExecuteWritableMutation(() =>
            {
                _subtitleEditor.BeginUndoAction();
                try
                {
                    _subtitleEditor.TargetStart = timingEditorLine.Position;
                    _subtitleEditor.TargetEnd = timingEditorLine.Position + originalTimingText.Length;
                    _subtitleEditor.ReplaceTarget(newTimingText);

                    var restoredLine = _subtitleEditor.Lines[Math.Clamp(caretLine, 0, _subtitleEditor.Lines.Count - 1)];
                    var lineLength = restoredLine.Text.TrimEnd('\r', '\n').Length;
                    var restoredPosition = restoredLine.Position + Math.Min(caretColumn, lineLength);
                    _subtitleEditor.SetCaretPosition(restoredPosition);
                    _subtitleEditor.ScrollCaret();
                    _subtitleEditor.Update();

                    _frameInsertionUndoCaretPosition = srtOldPosition;
                    _frameInsertionRedoCaretPosition = restoredPosition;
                    _frameInsertionUndoApplied = false;
                }
                finally
                {
                    _subtitleEditor.EndUndoAction();
                }
            });

            return;
        }

        var editorLine = _subtitleEditor.Lines[_subtitleEditor.CurrentLine];
        var oldText = editorLine.Text;
        var oldPosition = Math.Clamp(_subtitleEditor.CurrentPosition, editorLine.Position, editorLine.EndPosition);
        var hadTiming = SubtitleTimingFormatter.Detect(oldText).HasValue;

        var preserveDuration = false;
        var previousDurationMilliseconds = 0L;
        if (insertStart &&
            _settings.Editing.PreserveSubtitleDurationWhenInsertingStartFrame &&
            SubtitleDocument.TryGetInlineCue(
                oldText,
                GetVideoFrameRate(),
                out var previousStart,
                out var previousEnd,
                out _))
        {
            previousDurationMilliseconds = previousEnd - previousStart;
            preserveDuration = previousDurationMilliseconds > 0;
        }

        var newText = SubtitleTimingFormatter.Insert(
            oldText,
            videoPosition,
            GetVideoFrameRate(),
            GetActiveSubtitleFormat(),
            insertStart,
            out var timingPrefixLength);

        if (preserveDuration)
        {
            newText = SubtitleTimingFormatter.Insert(
                newText,
                videoPosition + previousDurationMilliseconds,
                GetVideoFrameRate(),
                GetActiveSubtitleFormat(),
                insertStart: false,
                out _);
        }

        if (newText == oldText)
        {
            return;
        }

        _subtitleEditor.ExecuteWritableMutation(() =>
        {
            _subtitleEditor.BeginUndoAction();
            try
            {
                _subtitleEditor.TargetStart = editorLine.Position;
                _subtitleEditor.TargetEnd = editorLine.EndPosition;
                _subtitleEditor.ReplaceTarget(newText);
                var newLine = _subtitleEditor.Lines[editorLine.Index];
                var targetPosition = !hadTiming && oldPosition == editorLine.Position
                    ? newLine.Position + timingPrefixLength
                    : oldPosition + newText.Length - oldText.Length;
                targetPosition = Math.Clamp(targetPosition, newLine.Position, newLine.EndPosition);
                _subtitleEditor.SetCaretPosition(targetPosition);
                _subtitleEditor.ScrollCaret();
                var redoCaretPosition = targetPosition;
                if (_settings.Editing.MoveCaretToNextLineAfterInsertingFrame &&
                    HasSubtitleText(oldText) &&
                    newLine.Index + 1 < _subtitleEditor.Lines.Count)
                {
                    var nextLine = _subtitleEditor.Lines[newLine.Index + 1];
                    _subtitleEditor.SetCaretPosition(nextLine.Position);
                    _subtitleEditor.ScrollCaret();
                    redoCaretPosition = nextLine.Position;
                }

                _frameInsertionUndoCaretPosition = oldPosition;
                _frameInsertionRedoCaretPosition = redoCaretPosition;
                _frameInsertionUndoApplied = false;
            }
            finally
            {
                _subtitleEditor.EndUndoAction();
            }
        });
    }

    private long GetReactionMilliseconds() =>
        (long)Math.Round(_settings.Editing.ReactionSeconds * 1000d, MidpointRounding.AwayFromZero);

    private bool HasSubtitleText(string line)
    {
        if (SubtitleDocument.TryGetInlineCue(line, GetVideoFrameRate(), out _, out _, out var inlineText))
        {
            return !string.IsNullOrWhiteSpace(inlineText);
        }

        if (SubtitleDocument.TryGetTmPlayerCue(line, out _, out var tmPlayerText))
        {
            return !string.IsNullOrWhiteSpace(tmPlayerText);
        }

        return !string.IsNullOrWhiteSpace(line);
    }
}

