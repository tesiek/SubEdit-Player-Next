using SubEdit.Player.WinForms.Controls;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void UpdateUndoRedoAvailability()
    {
        ToolbarButton("toolbarUndo").Enabled = _subtitleEditor.CanUndo;
        ToolbarButton("toolbarRedo").Enabled = _subtitleEditor.CanRedo;
    }

    private void UndoSubtitleEdit()
    {
        ClearRememberedSubtitleTimeScaleTransform();
        var caretPosition = !_frameInsertionUndoApplied ? _frameInsertionUndoCaretPosition : null;
        _restoringFrameInsertionCaretPosition = caretPosition.HasValue;
        try
        {
            _subtitleEditor.ExecuteWritableMutation(() => _subtitleEditor.Undo());
        }
        finally
        {
            _restoringFrameInsertionCaretPosition = false;
            UpdateUndoRedoAvailability();
        }

        if (caretPosition is not { } position)
        {
            return;
        }

        _subtitleEditor.SetCaretPosition(Math.Clamp(position, 0, _subtitleEditor.TextLength));
        _subtitleEditor.ScrollCaret();
        _frameInsertionUndoApplied = true;
    }

    private void RedoSubtitleEdit()
    {
        ClearRememberedSubtitleTimeScaleTransform();
        var caretPosition = _frameInsertionUndoApplied ? _frameInsertionRedoCaretPosition : null;
        _restoringFrameInsertionCaretPosition = caretPosition.HasValue;
        try
        {
            _subtitleEditor.ExecuteWritableMutation(() => _subtitleEditor.Redo());
        }
        finally
        {
            _restoringFrameInsertionCaretPosition = false;
            UpdateUndoRedoAvailability();
        }

        if (caretPosition is not { } position)
        {
            return;
        }

        _subtitleEditor.SetCaretPosition(Math.Clamp(position, 0, _subtitleEditor.TextLength));
        _subtitleEditor.ScrollCaret();
        _frameInsertionUndoApplied = false;
    }

    private void RefreshSubtitleInfoBar()
    {
        var editorLineCount = _subtitleEditor.Lines.Count;
        if (editorLineCount == 0)
        {
            _subtitleInfoBar.SetSnapshot(new SubtitleInfoSnapshot(1, 1, 0, null, null, null, null, null, null, null, null));
            return;
        }

        var editorLineIndex = Math.Clamp(_subtitleEditor.CurrentLine, 0, editorLineCount - 1);
        var editorLine = _subtitleEditor.Lines[editorLineIndex];
        var cursorPosition = Math.Clamp(_subtitleEditor.CurrentPosition, editorLine.Position, editorLine.EndPosition);
        var editorColumn = cursorPosition - editorLine.Position + 1;
        var cue = GetCueInfoAtEditorLine(editorLineIndex, cursorPosition - editorLine.Position);

        _subtitleInfoBar.SetSnapshot(new SubtitleInfoSnapshot(
            editorColumn,
            editorLineIndex + 1,
            editorLineCount,
            cue?.TextCursorPosition,
            cue?.TextLength,
            cue?.LineCount,
            cue?.LineCursorPosition,
            cue?.LineLength,
            cue?.LineNumber,
            cue?.DurationMilliseconds,
            cue?.FiftyCharactersDurationMilliseconds));
    }

    private SubtitleCueInfo? GetCueInfoAtEditorLine(int editorLineIndex, int cursorOffset)
    {
        var frameRate = GetVideoFrameRate();
        var currentLine = _subtitleEditor.Lines[editorLineIndex].Text;
        if (SubtitleDocument.TryGetTmPlayerCue(currentLine, out var tmStart, out var tmText))
        {
            var textStartBoundary = Math.Max(0, currentLine.Length - tmText.Length);
            return CreateCueInfo(
                tmText,
                tmStart,
                FindNextTmPlayerStart(editorLineIndex, tmStart) ??
                    tmStart + GetDefaultSubtitleDisplayDurationMilliseconds(tmText),
                cursorOffset >= textStartBoundary ? cursorOffset - textStartBoundary : null);
        }

        if (SubtitleDocument.TryGetInlineCue(currentLine, frameRate, out var start, out var end, out var inlineText))
        {
            var inlineTextOffset = currentLine.Length - inlineText.Length;
            var textStartBoundary = Math.Max(0, inlineTextOffset - 1);
            return CreateCueInfo(
                inlineText,
                start,
                end > start ? end : start + GetDefaultSubtitleDisplayDurationMilliseconds(inlineText),
                cursorOffset >= textStartBoundary ? cursorOffset - textStartBoundary : null);
        }

        var timingLineIndex = FindSrtTimingLine(editorLineIndex, frameRate);
        if (timingLineIndex < 0 ||
            !SubtitleDocument.TryGetCueTiming(_subtitleEditor.Lines[timingLineIndex].Text, frameRate, out start, out end))
        {
            return null;
        }

        var firstTextLineIndex = timingLineIndex + 1;
        var lastTextLineIndex = firstTextLineIndex;
        while (lastTextLineIndex < _subtitleEditor.Lines.Count &&
               !string.IsNullOrWhiteSpace(_subtitleEditor.Lines[lastTextLineIndex].Text))
        {
            lastTextLineIndex++;
        }

        if (lastTextLineIndex == firstTextLineIndex)
        {
            return null;
        }

        var textLines = Enumerable.Range(firstTextLineIndex, lastTextLineIndex - firstTextLineIndex)
            .Select(index => _subtitleEditor.Lines[index].Text)
            .ToArray();
        int? textOffset = null;
        if (editorLineIndex >= firstTextLineIndex && editorLineIndex < lastTextLineIndex)
        {
            textOffset = cursorOffset;
            for (var index = firstTextLineIndex; index < editorLineIndex; index++)
            {
                textOffset += _subtitleEditor.Lines[index].Text.Length + 1;
            }
        }

        var srtText = string.Join("\n", textLines);
        return CreateCueInfo(srtText, start, end, textOffset);
    }

    private long GetDefaultSubtitleDisplayDurationMilliseconds(string sourceText)
    {
        var characterCount = GetEditorTextLines(sourceText).Sum(line => line.Length);
        var seconds = _settings.Editing.AdditionalDefaultDisplaySeconds +
            (characterCount * _settings.Editing.DefaultFiftyCharacterDisplaySeconds / 50d);
        return Math.Max(0L, (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero));
    }

    private long? FindNextTmPlayerStart(int editorLineIndex, long currentStartMilliseconds)
    {
        for (var index = editorLineIndex + 1; index < _subtitleEditor.Lines.Count; index++)
        {
            if (SubtitleDocument.TryGetTmPlayerCue(_subtitleEditor.Lines[index].Text, out var start, out _) &&
                start > currentStartMilliseconds)
            {
                return start;
            }
        }

        return null;
    }

    private int FindSrtTimingLine(int editorLineIndex, double frameRate) =>
        SubtitleTimingFormatter.FindSrtTimingLineIndex(
            _subtitleEditor.Lines.Count,
            i => _subtitleEditor.Lines[i].Text,
            editorLineIndex);

    private static SubtitleCueInfo CreateCueInfo(string sourceText, long startMilliseconds, long? endMilliseconds, int? cursorOffset)
    {
        var textLines = GetEditorTextLines(sourceText);
        var textLength = textLines.Sum(line => line.Length);
        var lineCount = textLines.Length;
        int? textCursorPosition = null;
        int? lineCursorPosition = null;
        int? lineLength = null;
        int? lineNumber = null;

        if (cursorOffset is { } sourceOffset)
        {
            var safeOffset = Math.Clamp(sourceOffset, 0, sourceText.Length);
            var prefixLines = GetEditorTextLines(sourceText[..safeOffset]);
            var currentLineIndex = Math.Clamp(prefixLines.Length - 1, 0, lineCount - 1);
            lineNumber = currentLineIndex + 1;
            lineCursorPosition = prefixLines[currentLineIndex].Length + 1;
            lineLength = textLines[currentLineIndex].Length;
            textCursorPosition = textLines.Take(currentLineIndex).Sum(line => line.Length) + currentLineIndex + lineCursorPosition.Value;
        }

        long? duration = endMilliseconds is { } endMillisecondsValue
            ? Math.Max(0, endMillisecondsValue - startMilliseconds)
            : null;
        long? durationForFiftyCharacters = duration is { } durationValue && textLength > 0
            ? (long)Math.Round(durationValue * 50d / textLength)
            : null;
        return new SubtitleCueInfo(
            textCursorPosition,
            textLength,
            lineCount,
            lineCursorPosition,
            lineLength,
            lineNumber,
            duration,
            durationForFiftyCharacters);
    }

    private static string[] GetEditorTextLines(string source) => source
        .TrimEnd('\r', '\n')
        .Replace("\r\n", "\n")
        .Replace('\r', '\n')
        .Replace('|', '\n')
        .Split('\n');

    private readonly record struct SubtitleCueInfo(
        int? TextCursorPosition,
        int TextLength,
        int LineCount,
        int? LineCursorPosition,
        int? LineLength,
        int? LineNumber,
        long? DurationMilliseconds,
        long? FiftyCharactersDurationMilliseconds);

    private void AlignEditorHeader(Label header, SubtitleTextEditor editor)
    {
        header.Padding = new Padding(editor.LineNumberMarginWidth, 0, 0, ScaleLogicalPixels(4));
    }
}

