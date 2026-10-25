using SubEdit.Player.WinForms.Controls;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ShowFindDialog()
    {
        if (_isFullScreen)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        var initialQuery = string.IsNullOrEmpty(_subtitleEditor.SelectedText)
            ? _lastFindQuery ?? string.Empty
            : _subtitleEditor.SelectedText;
        using var dialog = new SubtitleFindDialog(initialQuery);
        dialog.FindNextRequested += (_, _) => FindNextSubtitleText(
            dialog.Query,
            dialog.MatchCase,
            dialog.WholeWord,
            focusEditor: false);
        dialog.FindPreviousRequested += (_, _) => FindPreviousSubtitleText(
            dialog.Query,
            dialog.MatchCase,
            dialog.WholeWord,
            focusEditor: false);
        dialog.ShowDialog(this);
    }

    private void ShowReplaceDialog()
    {
        if (_isFullScreen)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        if (_replaceDialog is not null)
        {
            _replaceDialog.Activate();
            return;
        }

        var initialQuery = string.IsNullOrEmpty(_subtitleEditor.SelectedText)
            ? _lastFindQuery ?? string.Empty
            : _subtitleEditor.SelectedText;
        var dialog = new SubtitleReplaceDialog(initialQuery, _lastFindMatchCase, _lastFindWholeWord);
        dialog.FindNextRequested += (_, _) => FindNextSubtitleText(
            dialog.Query,
            dialog.MatchCase,
            dialog.WholeWord,
            focusEditor: false);
        dialog.ReplaceRequested += (_, _) => ReplaceNextSubtitleText(
            dialog.Query,
            dialog.Replacement,
            dialog.MatchCase,
            dialog.WholeWord);
        dialog.ReplaceAllRequested += (_, _) => ReplaceAllSubtitleText(
            dialog.Query,
            dialog.Replacement,
            dialog.MatchCase,
            dialog.WholeWord);
        dialog.FormClosed += (_, _) => _replaceDialog = null;
        _replaceDialog = dialog;
        dialog.Show(this);
    }

    private bool FindNextSubtitleText()
    {
        if (_repeatUnsortedSubtitleSearch)
        {
            return FindNextUnsortedSubtitle();
        }

        if (_repeatSubtitleGapSearch)
        {
            return FindNextSubtitleGap();
        }

        if (_repeatShortSubtitleSearch)
        {
            return FindNextShortSubtitle();
        }

        if (_repeatLongSubtitleLineSearch)
        {
            return FindNextLongSubtitleLine();
        }

        return !string.IsNullOrEmpty(_lastFindQuery) && FindNextSubtitleText(
            _lastFindQuery,
            _lastFindMatchCase,
            _lastFindWholeWord,
            focusEditor: true);
    }

    private bool FindNextSubtitleText(string query, bool matchCase, bool wholeWord, bool focusEditor)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        _lastFindQuery = query;
        _lastFindMatchCase = matchCase;
        _lastFindWholeWord = wholeWord;
        _repeatLongSubtitleLineSearch = false;
        _repeatShortSubtitleSearch = false;
        _repeatSubtitleGapSearch = false;
        _repeatUnsortedSubtitleSearch = false;

        if (!FindSubtitleText(query, matchCase, wholeWord, focusEditor, forward: true))
        {
            ShowTemporaryFileStatus($"Nie znaleziono tekstu: {query}");
        }

        return true;
    }

    private bool FindPreviousSubtitleText()
    {
        if (_repeatUnsortedSubtitleSearch)
        {
            return FindPreviousUnsortedSubtitle();
        }

        if (_repeatSubtitleGapSearch)
        {
            return FindPreviousSubtitleGap();
        }

        if (_repeatShortSubtitleSearch)
        {
            return FindPreviousShortSubtitle();
        }

        if (_repeatLongSubtitleLineSearch)
        {
            return FindPreviousLongSubtitleLine();
        }

        return !string.IsNullOrEmpty(_lastFindQuery) && FindPreviousSubtitleText(
            _lastFindQuery,
            _lastFindMatchCase,
            _lastFindWholeWord,
            focusEditor: true);
    }

    private bool FindPreviousSubtitleText(string query, bool matchCase, bool wholeWord, bool focusEditor)
    {
        if (string.IsNullOrEmpty(query))
        {
            return false;
        }

        _lastFindQuery = query;
        _lastFindMatchCase = matchCase;
        _lastFindWholeWord = wholeWord;
        _repeatLongSubtitleLineSearch = false;
        _repeatShortSubtitleSearch = false;
        _repeatSubtitleGapSearch = false;
        _repeatUnsortedSubtitleSearch = false;

        if (!FindSubtitleText(query, matchCase, wholeWord, focusEditor, forward: false))
        {
            ShowTemporaryFileStatus($"Nie znaleziono tekstu: {query}");
        }

        return true;
    }

    private bool FindSubtitleText(string query, bool matchCase, bool wholeWord, bool focusEditor, bool forward = true)
    {
        var text = _subtitleEditor.Text;
        if (text.Length == 0)
        {
            return false;
        }

        var comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        int foundIndex;

        if (forward)
        {
            var start = Math.Clamp(_subtitleEditor.SelectionEnd, 0, text.Length);
            foundIndex = FindSubtitleTextIndex(text, query, start, comparison, wholeWord);
            if (foundIndex < 0 && start > 0)
            {
                foundIndex = FindSubtitleTextIndex(text, query, 0, comparison, wholeWord);
            }
        }
        else
        {
            var start = Math.Min(_subtitleEditor.SelectionStart - 1, text.Length - 1);
            foundIndex = start >= 0 ? FindPreviousSubtitleTextIndex(text, query, start, comparison, wholeWord) : -1;
            if (foundIndex < 0)
            {
                foundIndex = FindPreviousSubtitleTextIndex(text, query, text.Length - 1, comparison, wholeWord);
            }
        }

        if (foundIndex < 0)
        {
            return false;
        }

        _subtitleEditor.SelectionStart = foundIndex;
        _subtitleEditor.SelectionEnd = foundIndex + query.Length;
        CenterSubtitleEditorOnCaret();
        if (focusEditor)
        {
            _subtitleEditor.Focus();
        }
        return true;
    }

    private void ReplaceNextSubtitleText(string query, string replacement, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        StoreFindOptions(query, matchCase, wholeWord);
        if (IsCurrentSelectionMatch(query, matchCase))
        {
            _subtitleEditor.ExecuteWritableMutation(() => _subtitleEditor.ReplaceSelection(replacement));
        }

        FindNextSubtitleText(query, matchCase, wholeWord, focusEditor: false);
    }

    private void ReplaceAllSubtitleText(string query, string replacement, bool matchCase, bool wholeWord)
    {
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        StoreFindOptions(query, matchCase, wholeWord);
        var text = _subtitleEditor.Text;
        var comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        var matches = new List<int>();
        for (var index = FindSubtitleTextIndex(text, query, 0, comparison, wholeWord);
             index >= 0;
             index = FindSubtitleTextIndex(text, query, index + 1, comparison, wholeWord))
        {
            matches.Add(index);
        }

        if (matches.Count == 0)
        {
            ShowTemporaryFileStatus($"Nie znaleziono tekstu: {query}");
            return;
        }

        _subtitleEditor.ExecuteWritableMutation(() =>
        {
            _subtitleEditor.BeginUndoAction();
            try
            {
                for (var index = matches.Count - 1; index >= 0; index--)
                {
                    _subtitleEditor.SelectionStart = matches[index];
                    _subtitleEditor.SelectionEnd = matches[index] + query.Length;
                    _subtitleEditor.ReplaceSelection(replacement);
                }
            }
            finally
            {
                _subtitleEditor.EndUndoAction();
            }
        });

        _subtitleEditor.SelectionStart = matches[0];
        _subtitleEditor.SelectionEnd = matches[0] + replacement.Length;
        CenterSubtitleEditorOnCaret();
        ShowTemporaryFileStatus($"Zamieniono wystąpień: {matches.Count}");
    }

    private bool IsCurrentSelectionMatch(string query, bool matchCase)
    {
        var comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        return string.Equals(_subtitleEditor.SelectedText, query, comparison);
    }

    private void StoreFindOptions(string query, bool matchCase, bool wholeWord)
    {
        _lastFindQuery = query;
        _lastFindMatchCase = matchCase;
        _lastFindWholeWord = wholeWord;
        _repeatLongSubtitleLineSearch = false;
        _repeatShortSubtitleSearch = false;
        _repeatSubtitleGapSearch = false;
        _repeatUnsortedSubtitleSearch = false;
    }

    private void ShowFindLongSubtitleLineDialog()
    {
        if (_isFullScreen)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        using var dialog = new FindLongSubtitleLineDialog(_settings.LongSubtitleLineLength);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.LongSubtitleLineLength = dialog.MinimumLength;
        _repeatLongSubtitleLineSearch = true;
        _repeatShortSubtitleSearch = false;
        _repeatSubtitleGapSearch = false;
        _repeatUnsortedSubtitleSearch = false;
        FindNextLongSubtitleLine();
    }

    private bool FindNextLongSubtitleLine()
    {
        var minimumLength = Math.Max(1, _settings.LongSubtitleLineLength);
        if (!TryFindNextLongSubtitleLine(minimumLength, out var position))
        {
            ShowTemporaryFileStatus($"Nie znaleziono wiersza dłuższego niż {minimumLength} znaków.");
            return true;
        }

        _subtitleEditor.SetCaretPosition(position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindNextLongSubtitleLine(int minimumLength, out int position)
    {
        position = -1;
        var searchStart = Math.Clamp(_subtitleEditor.SelectionEnd, 0, _subtitleEditor.TextLength);
        var isSrtDocument = SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text);
        var insideSrtCue = false;

        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var line = _subtitleEditor.Lines[lineIndex];
            var lineText = line.Text.TrimEnd('\r', '\n');

            if (isSrtDocument)
            {
                if (lineText.Contains("-->", StringComparison.Ordinal) &&
                    SubtitleDocument.TryGetCueStartMilliseconds(
                        lineText,
                        GetVideoFrameRate(),
                        out _))
                {
                    insideSrtCue = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(lineText))
                {
                    insideSrtCue = false;
                    continue;
                }

                if (insideSrtCue && lineText.Length > minimumLength && line.Position > searchStart)
                {
                    position = line.Position;
                    return true;
                }

                continue;
            }

            string subtitleText;
            if (!SubtitleDocument.TryGetInlineCue(
                    lineText,
                    GetVideoFrameRate(),
                    out _,
                    out _,
                    out subtitleText) &&
                !SubtitleDocument.TryGetTmPlayerCue(lineText, out _, out subtitleText))
            {
                continue;
            }

            var subtitleStart = line.Position + lineText.Length - subtitleText.Length;
            var segmentStart = 0;
            while (segmentStart <= subtitleText.Length)
            {
                var separator = subtitleText.IndexOf('|', segmentStart);
                var segmentEnd = separator >= 0 ? separator : subtitleText.Length;
                var candidatePosition = subtitleStart + segmentStart;
                if (segmentEnd - segmentStart > minimumLength && candidatePosition > searchStart)
                {
                    position = candidatePosition;
                    return true;
                }

                if (separator < 0)
                {
                    break;
                }

                segmentStart = separator + 1;
            }
        }

        return false;
    }

    private bool FindPreviousLongSubtitleLine()
    {
        var minimumLength = Math.Max(1, _settings.LongSubtitleLineLength);
        if (!TryFindPreviousLongSubtitleLine(minimumLength, out var position))
        {
            ShowTemporaryFileStatus($"Nie znaleziono wiersza dłuższego niż {minimumLength} znaków.");
            return true;
        }

        _subtitleEditor.SetCaretPosition(position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindPreviousLongSubtitleLine(int minimumLength, out int position)
    {
        position = -1;
        var searchStart = Math.Clamp(_subtitleEditor.SelectionStart, 0, _subtitleEditor.TextLength);
        var isSrtDocument = SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text);
        var insideSrtCue = false;
        var foundPosition = -1;

        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var line = _subtitleEditor.Lines[lineIndex];
            var lineText = line.Text.TrimEnd('\r', '\n');

            if (isSrtDocument)
            {
                if (lineText.Contains("-->", StringComparison.Ordinal) &&
                    SubtitleDocument.TryGetCueStartMilliseconds(
                        lineText,
                        GetVideoFrameRate(),
                        out _))
                {
                    insideSrtCue = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(lineText))
                {
                    insideSrtCue = false;
                    continue;
                }

                if (insideSrtCue && lineText.Length > minimumLength && line.Position < searchStart)
                {
                    foundPosition = line.Position;
                }

                continue;
            }

            string subtitleText;
            if (!SubtitleDocument.TryGetInlineCue(
                    lineText,
                    GetVideoFrameRate(),
                    out _,
                    out _,
                    out subtitleText) &&
                !SubtitleDocument.TryGetTmPlayerCue(lineText, out _, out subtitleText))
            {
                continue;
            }

            var subtitleStart = line.Position + lineText.Length - subtitleText.Length;
            var segmentStart = 0;
            while (segmentStart <= subtitleText.Length)
            {
                var separator = subtitleText.IndexOf('|', segmentStart);
                var segmentEnd = separator >= 0 ? separator : subtitleText.Length;
                var candidatePosition = subtitleStart + segmentStart;
                if (segmentEnd - segmentStart > minimumLength && candidatePosition < searchStart)
                {
                    foundPosition = candidatePosition;
                }

                if (separator < 0)
                {
                    break;
                }

                segmentStart = separator + 1;
            }
        }

        if (foundPosition >= 0)
        {
            position = foundPosition;
            return true;
        }

        return false;
    }

    private void ShowFindShortSubtitleDialog()
    {
        if (_isFullScreen || _subtitleEditor.TextLength == 0)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        using var dialog = new FindShortSubtitleDialog(_settings.ShortSubtitleSearch, GetVideoFrameRate());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.ShortSubtitleSearch.MaximumDurationSeconds = dialog.MaximumDurationSeconds;
        _settings.ShortSubtitleSearch.AdjustForLineLength = dialog.AdjustForLineLength;
        _repeatShortSubtitleSearch = true;
        _repeatLongSubtitleLineSearch = false;
        _repeatSubtitleGapSearch = false;
        _repeatUnsortedSubtitleSearch = false;
        FindNextShortSubtitle();
    }

    private bool FindNextShortSubtitle()
    {
        var maximumDurationSeconds = _settings.ShortSubtitleSearch.MaximumDurationSeconds;
        if (!TryFindNextShortSubtitle(maximumDurationSeconds, _settings.ShortSubtitleSearch.AdjustForLineLength, out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono krótko wyświetlanego napisu.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindNextShortSubtitle(
        double maximumDurationSeconds,
        bool adjustForLineLength,
        out int lineIndex)
    {
        lineIndex = -1;
        if (!double.IsFinite(maximumDurationSeconds) || maximumDurationSeconds <= 0d)
        {
            return false;
        }

        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        for (var index = 0; index + 1 < subtitles.Count; index++)
        {
            var subtitle = subtitles[index];
            if (subtitle.LineIndex <= currentLine)
            {
                continue;
            }

            var nextStartMilliseconds = subtitles[index + 1].StartMilliseconds;
            var effectiveEndMilliseconds = subtitle.EndMilliseconds is { } endMilliseconds
                ? Math.Min(endMilliseconds, nextStartMilliseconds)
                : nextStartMilliseconds;
            effectiveEndMilliseconds = Math.Max(subtitle.StartMilliseconds, effectiveEndMilliseconds);
            var durationSeconds = (effectiveEndMilliseconds - subtitle.StartMilliseconds) / 1000d;
            if (adjustForLineLength)
            {
                durationSeconds *= 30d / Math.Max(1, subtitle.CharacterCount);
            }

            if (durationSeconds <= maximumDurationSeconds)
            {
                lineIndex = subtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private bool FindPreviousShortSubtitle()
    {
        var maximumDurationSeconds = _settings.ShortSubtitleSearch.MaximumDurationSeconds;
        if (!TryFindPreviousShortSubtitle(maximumDurationSeconds, _settings.ShortSubtitleSearch.AdjustForLineLength, out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono krótko wyświetlanego napisu.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindPreviousShortSubtitle(
        double maximumDurationSeconds,
        bool adjustForLineLength,
        out int lineIndex)
    {
        lineIndex = -1;
        if (!double.IsFinite(maximumDurationSeconds) || maximumDurationSeconds <= 0d)
        {
            return false;
        }

        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        for (var index = subtitles.Count - 2; index >= 0; index--)
        {
            var subtitle = subtitles[index];
            if (subtitle.LineIndex >= currentLine)
            {
                continue;
            }

            var nextStartMilliseconds = subtitles[index + 1].StartMilliseconds;
            var effectiveEndMilliseconds = subtitle.EndMilliseconds is { } endMilliseconds
                ? Math.Min(endMilliseconds, nextStartMilliseconds)
                : nextStartMilliseconds;
            effectiveEndMilliseconds = Math.Max(subtitle.StartMilliseconds, effectiveEndMilliseconds);
            var durationSeconds = (effectiveEndMilliseconds - subtitle.StartMilliseconds) / 1000d;
            if (adjustForLineLength)
            {
                durationSeconds *= 30d / Math.Max(1, subtitle.CharacterCount);
            }

            if (durationSeconds <= maximumDurationSeconds)
            {
                lineIndex = subtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private List<TimedSubtitleLine> GetTimedSubtitleLines()
    {
        var subtitles = new List<TimedSubtitleLine>();
        var frameRate = GetVideoFrameRate();
        var isSrtDocument = SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text);
        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var lineText = _subtitleEditor.Lines[lineIndex].Text.TrimEnd('\r', '\n');
            if (isSrtDocument)
            {
                if (lineText.Contains("-->", StringComparison.Ordinal) &&
                    SubtitleDocument.TryGetCueTiming(lineText, frameRate, out var srtStart, out var srtEnd))
                {
                    subtitles.Add(new TimedSubtitleLine(
                        lineIndex,
                        srtStart,
                        srtEnd == 0 ? null : srtEnd,
                        GetSrtSubtitleTextLength(lineIndex)));
                }

                continue;
            }

            if (SubtitleDocument.TryGetInlineCue(
                    lineText,
                    frameRate,
                    out var start,
                    out var end,
                    out var inlineText))
            {
                subtitles.Add(new TimedSubtitleLine(
                    lineIndex,
                    start,
                    end == 0 ? null : end,
                    inlineText.Length));
            }
            else if (SubtitleDocument.TryGetTmPlayerCue(lineText, out start, out var tmPlayerText))
            {
                subtitles.Add(new TimedSubtitleLine(lineIndex, start, null, tmPlayerText.Length));
            }
        }

        return subtitles;
    }

    private void ShowFindSubtitleGapDialog()
    {
        if (_isFullScreen || _subtitleEditor.TextLength == 0)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        using var dialog = new FindSubtitleGapDialog(_settings.SubtitleGapSearch, GetVideoFrameRate());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.SubtitleGapSearch.MinimumGapSeconds = dialog.MinimumGapSeconds;
        _repeatSubtitleGapSearch = true;
        _repeatShortSubtitleSearch = false;
        _repeatLongSubtitleLineSearch = false;
        _repeatUnsortedSubtitleSearch = false;
        FindNextSubtitleGap();
    }

    private bool FindNextSubtitleGap()
    {
        if (!TryFindNextSubtitleGap(_settings.SubtitleGapSearch.MinimumGapSeconds, out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono przerwy w wyświetlaniu napisów.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindNextSubtitleGap(double minimumGapSeconds, out int lineIndex)
    {
        lineIndex = -1;
        if (!double.IsFinite(minimumGapSeconds) || minimumGapSeconds <= 0d)
        {
            return false;
        }

        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        var minimumGapMilliseconds = minimumGapSeconds * 1000d;
        for (var index = 0; index + 1 < subtitles.Count; index++)
        {
            var subtitle = subtitles[index];
            if (subtitle.LineIndex < currentLine)
            {
                continue;
            }

            var nextSubtitle = subtitles[index + 1];
            var effectiveEndMilliseconds = subtitle.EndMilliseconds ?? nextSubtitle.StartMilliseconds;
            if (nextSubtitle.StartMilliseconds - effectiveEndMilliseconds >= minimumGapMilliseconds)
            {
                lineIndex = nextSubtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private bool FindPreviousSubtitleGap()
    {
        if (!TryFindPreviousSubtitleGap(_settings.SubtitleGapSearch.MinimumGapSeconds, out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono przerwy w wyświetlaniu napisów.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindPreviousSubtitleGap(double minimumGapSeconds, out int lineIndex)
    {
        lineIndex = -1;
        if (!double.IsFinite(minimumGapSeconds) || minimumGapSeconds <= 0d)
        {
            return false;
        }

        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        var minimumGapMilliseconds = minimumGapSeconds * 1000d;
        for (var index = subtitles.Count - 2; index >= 0; index--)
        {
            var subtitle = subtitles[index];
            var nextSubtitle = subtitles[index + 1];
            if (nextSubtitle.LineIndex >= currentLine)
            {
                continue;
            }

            var effectiveEndMilliseconds = subtitle.EndMilliseconds ?? nextSubtitle.StartMilliseconds;
            if (nextSubtitle.StartMilliseconds - effectiveEndMilliseconds >= minimumGapMilliseconds)
            {
                lineIndex = nextSubtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private void FindUnsortedSubtitle()
    {
        if (_isFullScreen || _subtitleEditor.TextLength == 0)
        {
            return;
        }

        _splitContainer.Panel2Collapsed = false;
        _repeatUnsortedSubtitleSearch = true;
        _repeatSubtitleGapSearch = false;
        _repeatShortSubtitleSearch = false;
        _repeatLongSubtitleLineSearch = false;
        FindNextUnsortedSubtitle();
    }

    private bool FindNextUnsortedSubtitle()
    {
        if (!TryFindNextUnsortedSubtitle(out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono nieposortowanego napisu.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindNextUnsortedSubtitle(out int lineIndex)
    {
        lineIndex = -1;
        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        for (var index = 1; index < subtitles.Count; index++)
        {
            var subtitle = subtitles[index];
            if (subtitle.LineIndex <= currentLine)
            {
                continue;
            }

            if (subtitle.StartMilliseconds < subtitles[index - 1].StartMilliseconds)
            {
                lineIndex = subtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private bool FindPreviousUnsortedSubtitle()
    {
        if (!TryFindPreviousUnsortedSubtitle(out var lineIndex))
        {
            ShowTemporaryFileStatus("Nie znaleziono nieposortowanego napisu.");
            return true;
        }

        var line = _subtitleEditor.Lines[lineIndex];
        _subtitleEditor.SetCaretPosition(line.Position);
        CenterSubtitleEditorOnCaret();
        _subtitleEditor.Focus();
        return true;
    }

    private bool TryFindPreviousUnsortedSubtitle(out int lineIndex)
    {
        lineIndex = -1;
        var subtitles = GetTimedSubtitleLines();
        var currentLine = Math.Clamp(_subtitleEditor.CurrentLine, 0, Math.Max(0, _subtitleEditor.Lines.Count - 1));
        for (var index = subtitles.Count - 1; index >= 1; index--)
        {
            var subtitle = subtitles[index];
            if (subtitle.LineIndex >= currentLine)
            {
                continue;
            }

            if (subtitle.StartMilliseconds < subtitles[index - 1].StartMilliseconds)
            {
                lineIndex = subtitle.LineIndex;
                return true;
            }
        }

        return false;
    }

    private int GetSrtSubtitleTextLength(int timingLineIndex)
    {
        var length = 0;
        for (var lineIndex = timingLineIndex + 1; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var lineText = _subtitleEditor.Lines[lineIndex].Text.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(lineText))
            {
                break;
            }

            length += lineText.Length;
        }

        return length;
    }

    private readonly record struct TimedSubtitleLine(
        int LineIndex,
        long StartMilliseconds,
        long? EndMilliseconds,
        int CharacterCount);

    private static int FindSubtitleTextIndex(
        string text,
        string query,
        int start,
        StringComparison comparison,
        bool wholeWord)
    {
        for (var index = text.IndexOf(query, start, comparison); index >= 0; index = text.IndexOf(query, index + 1, comparison))
        {
            if (!wholeWord || IsWholeWordMatch(text, index, query.Length))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindPreviousSubtitleTextIndex(
        string text,
        string query,
        int start,
        StringComparison comparison,
        bool wholeWord)
    {
        if (start < 0 || start >= text.Length)
        {
            return -1;
        }

        for (var index = text.LastIndexOf(query, start, comparison); index >= 0; index = index > 0 ? text.LastIndexOf(query, index - 1, comparison) : -1)
        {
            if (!wholeWord || IsWholeWordMatch(text, index, query.Length))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsWholeWordMatch(string text, int index, int length)
    {
        return (index == 0 || !IsWordCharacter(text[index - 1])) &&
               (index + length >= text.Length || !IsWordCharacter(text[index + length]));
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
}
