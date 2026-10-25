using System.Text;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void ToggleCurrentSubtitleLineItalic() => ToggleCurrentSubtitleLineStyle('i');

    private void ShowItalicMarkerConversionDialog()
    {
        using var dialog = new ItalicMarkerConversionDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var viewState = _subtitleEditor.CaptureViewState();
        var currentLine = viewState.Line;
        var currentColumn = viewState.Column;
        var result = SubtitleItalicMarkerProcessor.Convert(
            _subtitleEditor.Text,
            dialog.SelectedMarkers,
            GetVideoFrameRate());

        if (result.ChangedLineCount > 0)
        {
            _subtitleEditor.ApplyFullDocumentEdit(result.Text, viewState with { Line = currentLine, Column = currentColumn });
        }

        _subtitleEditor.Focus();
        var lineWord = result.ChangedLineCount == 1 ? "linii" : "liniach";
        MessageBox.Show(
            this,
            $"Zmieniono znaki kursywy w {result.ChangedLineCount} {lineWord}.",
            "Zamień znaki kursywy",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ToggleCurrentSubtitleLineUnderline() => ToggleCurrentSubtitleLineStyle('u');

    private void ToggleCurrentSubtitleLineBold() => ToggleCurrentSubtitleLineStyle('b');

    private void ToggleCurrentSubtitleLineStyle(char style)
    {
        PrepareForSelectionSensitiveSubtitleEdit();

        if (_splitContainer.Panel2Collapsed || !_subtitleEditor.Focused || _subtitleEditor.Lines.Count == 0)
        {
            return;
        }

        var editorLine = _subtitleEditor.Lines[_subtitleEditor.CurrentLine];
        var lineText = editorLine.Text.TrimEnd('\r', '\n');
        var frameRate = GetVideoFrameRate();
        string subtitleText;
        if (SubtitleDocument.TryGetTmPlayerCue(lineText, out _, out subtitleText) ||
            SubtitleDocument.TryGetInlineCue(lineText, frameRate, out _, out _, out subtitleText))
        {
            var subtitleTextStart = lineText.Length - subtitleText.Length;
            var currentPosition = Math.Clamp(_subtitleEditor.CurrentPosition, editorLine.Position, editorLine.EndPosition);
            var caretColumn = currentPosition - editorLine.Position;
            if (caretColumn < subtitleTextStart || caretColumn > lineText.Length)
            {
                return;
            }

            var selectionStart = Math.Min(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd) - editorLine.Position - subtitleTextStart;
            var selectionEnd = Math.Max(_subtitleEditor.SelectionStart, _subtitleEditor.SelectionEnd) - editorLine.Position - subtitleTextStart;
            if (selectionStart != selectionEnd)
            {
                if (selectionStart < 0 || selectionEnd > subtitleText.Length ||
                    subtitleText[selectionStart..selectionEnd].Contains('|'))
                {
                    return;
                }

                ToggleSelectedSubtitleTextStyle(editorLine.Position, lineText, subtitleTextStart, subtitleText, selectionStart, selectionEnd, style);
                return;
            }

            var caretInSubtitle = caretColumn - subtitleTextStart;
            var segmentStart = subtitleText.LastIndexOf('|', Math.Max(0, caretInSubtitle - 1)) + 1;
            var segmentEnd = subtitleText.IndexOf('|', caretInSubtitle);
            if (segmentEnd < 0)
            {
                segmentEnd = subtitleText.Length;
            }

            var segment = subtitleText[segmentStart..segmentEnd];
            var oldContentStart = GetSubtitleTextContentStart(segment, style);
            var updatedSegment = ToggleSubtitleSegmentStyle(segment, style);
            var newContentStart = GetSubtitleTextContentStart(updatedSegment, style);
            var updatedSubtitleText = subtitleText[..segmentStart] + updatedSegment + subtitleText[segmentEnd..];
            var updatedLineText = lineText[..subtitleTextStart] + updatedSubtitleText;
            var caretInSegment = caretInSubtitle - segmentStart;
            var updatedCaretInSegment = caretInSegment < oldContentStart
                ? Math.Min(caretInSegment, newContentStart)
                : newContentStart + caretInSegment - oldContentStart;
            var updatedCaretColumn = Math.Clamp(
                subtitleTextStart + segmentStart + updatedCaretInSegment,
                subtitleTextStart,
                updatedLineText.Length);

            _subtitleEditor.ExecuteWritableMutation(() =>
            {
                _subtitleEditor.BeginUndoAction();
                try
                {
                    _subtitleEditor.TargetStart = editorLine.Position;
                    _subtitleEditor.TargetEnd = editorLine.Position + lineText.Length;
                    _subtitleEditor.ReplaceTarget(updatedLineText);
                    var updatedCaretPosition = editorLine.Position + updatedCaretColumn;
                    _subtitleEditor.SetCaretPosition(updatedCaretPosition);
                    _subtitleEditor.ScrollCaret();
                }
                finally
                {
                    _subtitleEditor.EndUndoAction();
                }
            });
        }
    }

    private void ToggleSelectedSubtitleTextStyle(
        int editorLinePosition,
        string lineText,
        int subtitleTextStart,
        string subtitleText,
        int selectionStart,
        int selectionEnd,
        char style)
    {
        var openTag = $"<{style}>";
        var closeTag = $"</{style}>";
        var selectedText = subtitleText[selectionStart..selectionEnd];
        string updatedSubtitleText;
        int updatedSelectionStart;
        int updatedSelectionEnd;

        if (selectedText.StartsWith(openTag, StringComparison.OrdinalIgnoreCase) &&
            selectedText.EndsWith(closeTag, StringComparison.OrdinalIgnoreCase))
        {
            var unformattedText = selectedText[openTag.Length..^closeTag.Length];
            updatedSubtitleText = subtitleText[..selectionStart] + unformattedText + subtitleText[selectionEnd..];
            updatedSelectionStart = selectionStart;
            updatedSelectionEnd = selectionStart + unformattedText.Length;
        }
        else if (TryFindEnclosingSelectedHtmlStyle(
                     subtitleText,
                     selectionStart,
                     selectionEnd,
                     style,
                     out var openTagIndex,
                     out var closeTagIndex))
        {
            updatedSubtitleText = subtitleText.Remove(closeTagIndex, closeTag.Length).Remove(openTagIndex, openTag.Length);
            updatedSelectionStart = selectionStart - openTag.Length;
            updatedSelectionEnd = selectionEnd - openTag.Length;
        }
        else
        {
            updatedSubtitleText = subtitleText[..selectionStart] + openTag + selectedText + closeTag + subtitleText[selectionEnd..];
            updatedSelectionStart = selectionStart + openTag.Length;
            updatedSelectionEnd = updatedSelectionStart + selectedText.Length;
        }

        var updatedLineText = lineText[..subtitleTextStart] + updatedSubtitleText;
        _subtitleEditor.ExecuteWritableMutation(() =>
        {
            _subtitleEditor.BeginUndoAction();
            try
            {
                _subtitleEditor.TargetStart = editorLinePosition;
                _subtitleEditor.TargetEnd = editorLinePosition + lineText.Length;
                _subtitleEditor.ReplaceTarget(updatedLineText);
                _subtitleEditor.SetSelection(
                    editorLinePosition + subtitleTextStart + updatedSelectionStart,
                    editorLinePosition + subtitleTextStart + updatedSelectionEnd);
                _subtitleEditor.ScrollCaret();
            }
            finally
            {
                _subtitleEditor.EndUndoAction();
            }
        });
    }

    private static string ToggleSubtitleSegmentStyle(string segment, char style)
    {
        var controlPrefixLength = GetLeadingMicroDvdControlPrefixLength(segment);
        var controls = new List<string>();
        for (var index = 0; index < controlPrefixLength;)
        {
            var end = segment.IndexOf('}', index + 1);
            controls.Add(segment[index..(end + 1)]);
            index = end + 1;
        }

        var text = segment[controlPrefixLength..];
        var hasLegacyItalic = style == 'i' && text.StartsWith('/');
        if (hasLegacyItalic)
        {
            text = text[1..];
            if (text.EndsWith('/'))
            {
                text = text[..^1];
            }
        }

        var hasMicroDvdStyle = RemoveMicroDvdStyleControls(controls, style);
        if (!hasLegacyItalic && !hasMicroDvdStyle)
        {
            var hasOpeningTag = TryFindOpeningBoundaryTag(text, style, out var openTagIndex, out var openTagLength);
            var hasClosingTag = TryFindClosingBoundaryTag(text, style, out var closeTagIndex, out var closeTagLength);

            if (hasOpeningTag && hasClosingTag)
            {
                text = text.Remove(closeTagIndex, closeTagLength).Remove(openTagIndex, openTagLength);
            }
            else if (hasOpeningTag)
            {
                text = text + $"</{style}>";
            }
            else if (hasClosingTag)
            {
                text = $"<{style}>" + text;
            }
            else
            {
                text = $"<{style}>" + text + $"</{style}>";
            }
        }

        return string.Concat(controls) + text;
    }

    private static bool TryFindOpeningBoundaryTag(string text, char style, out int tagIndex, out int tagLength)
    {
        tagIndex = -1;
        tagLength = 0;
        var openTag = $"<{style}>";
        for (var index = 0; index < text.Length;)
        {
            if (TryReadKnownOpeningStyleTag(text, index, out var tag, out var length))
            {
                if (tagIndex < 0 && tag.Equals(openTag, StringComparison.OrdinalIgnoreCase))
                {
                    tagIndex = index;
                    tagLength = length;
                }

                index += length;
                continue;
            }

            break;
        }

        return tagIndex >= 0;
    }

    private static bool TryFindClosingBoundaryTag(string text, char style, out int tagIndex, out int tagLength)
    {
        tagIndex = -1;
        tagLength = 0;
        var closeTag = $"</{style}>";
        for (var index = text.Length; index > 0;)
        {
            if (TryReadKnownClosingStyleTagBefore(text, index, out var tag, out var length))
            {
                var currentTagIndex = index - length;
                if (tagIndex < 0 && tag.Equals(closeTag, StringComparison.OrdinalIgnoreCase))
                {
                    tagIndex = currentTagIndex;
                    tagLength = length;
                }

                index = currentTagIndex;
                continue;
            }

            break;
        }

        return tagIndex >= 0;
    }

    private static bool TryReadKnownOpeningStyleTag(string text, int index, out string tag, out int length)
    {
        tag = string.Empty;
        length = 0;
        if (index + 3 <= text.Length && text[index] == '<' && text[index + 2] == '>')
        {
            var charCode = text[index + 1];
            if (charCode is 'i' or 'I' or 'b' or 'B' or 'u' or 'U')
            {
                tag = text.Substring(index, 3);
                length = 3;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadKnownClosingStyleTagBefore(string text, int endIndex, out string tag, out int length)
    {
        tag = string.Empty;
        length = 0;
        if (endIndex >= 4 && text[endIndex - 4] == '<' && text[endIndex - 3] == '/' && text[endIndex - 1] == '>')
        {
            var charCode = text[endIndex - 2];
            if (charCode is 'i' or 'I' or 'b' or 'B' or 'u' or 'U')
            {
                tag = text.Substring(endIndex - 4, 4);
                length = 4;
                return true;
            }
        }

        return false;
    }

    private static bool RemoveMicroDvdStyleControls(List<string> controls, char style)
    {
        var removed = false;
        for (var index = controls.Count - 1; index >= 0; index--)
        {
            if (!TryGetMicroDvdStyleControl(controls[index], out var controlName, out var styles))
            {
                continue;
            }

            var removedCount = styles.RemoveAll(item => item.Equals(style.ToString(), StringComparison.OrdinalIgnoreCase));
            if (removedCount == 0)
            {
                continue;
            }

            removed = true;
            if (styles.Count == 0)
            {
                controls.RemoveAt(index);
            }
            else
            {
                controls[index] = $"{{{controlName}:{string.Join(",", styles)}}}";
            }
        }

        return removed;
    }

    private static bool TryFindEnclosingSelectedHtmlStyle(
        string text,
        int selectionStart,
        int selectionEnd,
        char style,
        out int openTagIndex,
        out int closeTagIndex)
    {
        var openTag = $"<{style}>";
        var closeTag = $"</{style}>";
        openTagIndex = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        while (openTagIndex >= 0)
        {
            var openTagEnd = openTagIndex + openTag.Length;
            if (openTagEnd <= selectionStart && !ContainsNonMarkupText(text[openTagEnd..selectionStart]))
            {
                closeTagIndex = FindMatchingHtmlStyleCloseTag(text, openTagIndex, openTag, closeTag);
                if (closeTagIndex >= selectionEnd && !ContainsNonMarkupText(text[selectionEnd..closeTagIndex]))
                {
                    return true;
                }
            }

            openTagIndex = text.IndexOf(openTag, openTagIndex + openTag.Length, StringComparison.OrdinalIgnoreCase);
        }

        closeTagIndex = -1;
        return false;
    }

    private static int FindMatchingHtmlStyleCloseTag(string text, int openTagIndex, string openTag, string closeTag)
    {
        var depth = 0;
        for (var index = openTagIndex; index < text.Length;)
        {
            if (text.AsSpan(index).StartsWith(openTag, StringComparison.OrdinalIgnoreCase))
            {
                depth++;
                index += openTag.Length;
                continue;
            }

            if (text.AsSpan(index).StartsWith(closeTag, StringComparison.OrdinalIgnoreCase))
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }

                index += closeTag.Length;
                continue;
            }

            index++;
        }

        return -1;
    }

    private static bool ContainsNonMarkupText(string text)
    {
        for (var index = 0; index < text.Length;)
        {
            if (text[index] != '<')
            {
                return true;
            }

            var end = text.IndexOf('>', index + 1);
            if (end < 0)
            {
                return true;
            }

            index = end + 1;
        }

        return false;
    }

    private static int GetSubtitleTextContentStart(string segment, char style)
    {
        var contentStart = GetLeadingMicroDvdControlPrefixLength(segment);
        if (style == 'i' && contentStart < segment.Length && segment[contentStart] == '/')
        {
            contentStart++;
        }

        while (contentStart < segment.Length && segment[contentStart] == '<')
        {
            var end = segment.IndexOf('>', contentStart + 1);
            if (end < 0 || segment.AsSpan(contentStart + 1, 1).SequenceEqual("/"))
            {
                break;
            }

            contentStart = end + 1;
        }

        return contentStart;
    }

    private static bool TryGetMicroDvdStyleControl(string control, out char name, out List<string> styles)
    {
        name = default;
        styles = [];
        if (control.Length < 5 || control[0] != '{' || control[^1] != '}' ||
            control[1] is not ('y' or 'Y') || control[2] != ':')
        {
            return false;
        }

        name = control[1];
        foreach (var style in control[3..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (style.Length != 1 || style[0] is not ('b' or 'B' or 'i' or 'I' or 'u' or 'U' or 's' or 'S'))
            {
                return false;
            }

            styles.Add(style);
        }

        return true;
    }

    private static int GetLeadingMicroDvdControlPrefixLength(string text)
    {
        var index = 0;
        while (index < text.Length && text[index] == '{')
        {
            var end = text.IndexOf('}', index + 1);
            if (end < 0 || end - index > 120)
            {
                break;
            }

            var code = text[(index + 1)..end].Trim();
            if (code.Length < 3 || code[1] != ':' || code[0] is not ('y' or 'Y' or 'f' or 'F' or 's' or 'S' or 'c' or 'C' or 'P'))
            {
                break;
            }

            index = end + 1;
        }

        return index;
    }
}
