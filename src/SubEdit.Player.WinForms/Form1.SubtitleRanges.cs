namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private (int FirstLine, int LastLine) GetSubtitleConversionLineRange(SubtitleConversionScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleConversionScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleConversionScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetSubtitleCorrectionLineRange(SubtitleCorrectionScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleCorrectionScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleCorrectionScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetSubtitleTimeShiftLineRange(SubtitleTimeShiftScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleTimeShiftScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleTimeShiftScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetSubtitleDisplayTimeLineRange(SubtitleDisplayTimeScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleDisplayTimeScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleDisplayTimeScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetRapidSubtitleMergeLineRange(RapidSubtitleMergeScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            RapidSubtitleMergeScope.FromCurrentLine => (currentLine, lastDocumentLine),
            RapidSubtitleMergeScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetLongSubtitleLineSplitRange(LongSubtitleLineSplitScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            LongSubtitleLineSplitScope.FromCurrentLine => (currentLine, lastDocumentLine),
            LongSubtitleLineSplitScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetSubtitleLineLimitRange(SubtitleLineLimitScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleLineLimitScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleLineLimitScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private (int FirstLine, int LastLine) GetSubtitleFrameRateRange(SubtitleFrameRateScope scope)
    {
        var lastDocumentLine = Math.Max(0, _subtitleEditor.Lines.Count - 1);
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, lastDocumentLine);
        return scope switch
        {
            SubtitleFrameRateScope.FromCurrentLine => (currentLine, lastDocumentLine),
            SubtitleFrameRateScope.ToCurrentLine => (0, currentLine),
            _ => GetSelectedSubtitleConversionLineRange(lastDocumentLine)
        };
    }

    private bool HasSubtitleSelection() =>
        _subtitleEditor.SelectionStart != _subtitleEditor.SelectionEnd;

    private bool IsEntireSubtitleDocumentSelected()
    {
        if (_subtitleEditor.TextLength == 0 || _subtitleEditor.SelectionStart == _subtitleEditor.SelectionEnd)
        {
            return false;
        }

        var selectionStart = Math.Min(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd);
        var selectionEnd = Math.Max(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd);
        return selectionStart == 0 && selectionEnd == _subtitleEditor.TextLength;
    }

    private bool IsFullSubtitleDocumentRange(
        int firstLine,
        int lastLine,
        int lastDocumentLine,
        bool hasSelection,
        bool isEntireDocumentSelected)
    {
        if (hasSelection)
        {
            return isEntireDocumentSelected && firstLine == 0 && lastLine == lastDocumentLine;
        }

        return firstLine == 0 && lastLine == lastDocumentLine;
    }

    private (int FirstLine, int LastLine) GetSelectedSubtitleConversionLineRange(int lastDocumentLine)
    {
        var selectionStart = Math.Min(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd);
        var selectionEnd = Math.Max(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd);
        if (selectionStart == selectionEnd || (selectionStart == 0 && selectionEnd == _subtitleEditor.TextLength && _subtitleEditor.TextLength > 0))
        {
            return (0, lastDocumentLine);
        }

        var firstLine = _subtitleEditor.LineFromPosition(selectionStart);
        var lastLine = _subtitleEditor.LineFromPosition(Math.Max(selectionStart, selectionEnd - 1));
        return (firstLine, lastLine);
    }
}
