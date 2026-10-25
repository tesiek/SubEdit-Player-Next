namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void MoveSubtitleCaretHome()
    {
        if (_subtitleEditor.Lines.Count == 0)
        {
            return;
        }

        var line = _subtitleEditor.Lines[Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1)];
        var lineText = line.Text.TrimEnd('\r', '\n');
        var subtitleTextOffset = 0;
        if (SubtitleDocument.TryGetTmPlayerCue(lineText, out _, out var tmPlayerText))
        {
            subtitleTextOffset = lineText.Length - tmPlayerText.Length;
        }
        else if (SubtitleDocument.TryGetInlineCue(
            lineText,
            GetVideoFrameRate(),
            out _,
            out _,
            out var inlineText))
        {
            subtitleTextOffset = lineText.Length - inlineText.Length;
        }

        var lineStart = line.Position;
        var subtitleTextStart = lineStart + subtitleTextOffset;
        var targetPosition = _subtitleEditor.CurrentPosition == subtitleTextStart && subtitleTextStart != lineStart
            ? lineStart
            : subtitleTextStart;
        _subtitleEditor.SetCaretPosition(targetPosition);
        _subtitleEditor.ScrollCaret();
    }

    private void MoveSubtitleCaretEnd()
    {
        if (_subtitleEditor.Lines.Count == 0)
        {
            return;
        }

        var line = _subtitleEditor.Lines[Math.Clamp(_subtitleEditor.CurrentLine, 0, _subtitleEditor.Lines.Count - 1)];
        var lineTextLength = line.Text.TrimEnd('\r', '\n').Length;
        var targetPosition = line.Position + lineTextLength;
        _subtitleEditor.SetCaretPosition(targetPosition);
        _subtitleEditor.ScrollCaret();
    }

    private void ShowGoToSubtitleDialog()
    {
        var targets = GetSubtitleNavigationTargets();
        if (targets.Count == 0)
        {
            return;
        }

        using var dialog = new GoToSubtitleDialog(GetVideoFrameRate());
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var targetMilliseconds = (long)Math.Min(
            long.MaxValue,
            Math.Round(dialog.TargetSeconds * 1000d, MidpointRounding.AwayFromZero));
        var target = targets
            .Where(candidate => candidate.StartMilliseconds <= targetMilliseconds)
            .OrderByDescending(candidate => candidate.StartMilliseconds)
            .FirstOrDefault();
        if (target == default)
        {
            target = targets.MinBy(candidate => candidate.StartMilliseconds);
        }

        MoveSubtitleCaretToLine(target.LineIndex);
    }

    private IReadOnlyList<(long StartMilliseconds, int LineIndex)> GetSubtitleNavigationTargets()
    {
        var targets = new List<(long StartMilliseconds, int LineIndex)>();
        var frameRate = GetVideoFrameRate();
        var isSrt = SubtitleFormatConverter.IsSrtDocument(_subtitleEditor.Text);
        long? previousStart = null;

        for (var lineIndex = 0; lineIndex < _subtitleEditor.Lines.Count; lineIndex++)
        {
            var lineText = _subtitleEditor.Lines[lineIndex].Text;
            if (!SubtitleDocument.TryGetCueStartMilliseconds(lineText, frameRate, out var startMilliseconds))
            {
                continue;
            }

            if (previousStart == startMilliseconds)
            {
                continue;
            }

            var destinationLine = lineIndex;
            if (isSrt &&
                lineIndex + 1 < _subtitleEditor.Lines.Count &&
                !string.IsNullOrWhiteSpace(_subtitleEditor.Lines[lineIndex + 1].Text))
            {
                destinationLine++;
            }

            targets.Add((startMilliseconds, destinationLine));
            previousStart = startMilliseconds;
        }

        return targets;
    }
}
