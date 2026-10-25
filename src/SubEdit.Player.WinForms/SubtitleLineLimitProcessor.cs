using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class SubtitleLineLimitProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^(?<prefix>\{-?\d+}\{-?\d*})(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^(?<prefix>\[-?\d+]\[-?\d*])(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^(?<prefix>\s*[+-]?\d{1,}:[+-]?\d{1,2}:[+-]?\d{1,2}(?:,\d+)?[:=])(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^\s*[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3}\s*-->\s*[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3}.*$",
        RegexOptions.Compiled);

    public static SubtitleLineLimitResult Limit(
        string text,
        int maximumLines,
        int firstLine,
        int lastLine,
        bool treatUnrecognizedAsText)
    {
        if (string.IsNullOrEmpty(text) || maximumLines < 1 || lastLine < firstLine)
        {
            return new SubtitleLineLimitResult(text, 0);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);

        return SubtitleFormatConverter.IsSrtDocument(text)
            ? LimitSrt(lines, maximumLines, firstLine, lastLine, lineBreak)
            : LimitInline(lines, maximumLines, firstLine, lastLine, lineBreak, treatUnrecognizedAsText);
    }

    private static SubtitleLineLimitResult LimitInline(
        List<string> lines,
        int maximumLines,
        int firstLine,
        int lastLine,
        string lineBreak,
        bool treatUnrecognizedAsText)
    {
        var modifiedLineCount = 0;
        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            var line = lines[lineIndex];
            if (!TrySeparatePrefix(line, out var prefix, out var subtitleText) && !treatUnrecognizedAsText)
            {
                continue;
            }

            if (prefix.Length == 0)
            {
                subtitleText = line;
            }

            var limitedText = LimitText(subtitleText, maximumLines);
            if (limitedText == subtitleText)
            {
                continue;
            }

            lines[lineIndex] = prefix + limitedText;
            modifiedLineCount++;
        }

        return new SubtitleLineLimitResult(string.Join(lineBreak, lines), modifiedLineCount);
    }

    private static SubtitleLineLimitResult LimitSrt(
        List<string> lines,
        int maximumLines,
        int firstLine,
        int lastLine,
        string lineBreak)
    {
        var modifiedLineCount = 0;
        foreach (var cue in ParseSrtCues(lines).OrderByDescending(cue => cue.TextStart))
        {
            if (cue.BlockStart < firstLine || cue.BlockEndExclusive - 1 > lastLine)
            {
                continue;
            }

            var subtitleText = string.Join('|', lines.Skip(cue.TextStart).Take(cue.TextLineCount));
            var limitedText = LimitText(subtitleText, maximumLines);
            if (limitedText == subtitleText)
            {
                continue;
            }

            lines.RemoveRange(cue.TextStart, cue.TextLineCount);
            lines.InsertRange(cue.TextStart, limitedText.Split('|'));
            modifiedLineCount++;
        }

        return new SubtitleLineLimitResult(string.Join(lineBreak, lines), modifiedLineCount);
    }

    private static string LimitText(string text, int maximumLines)
    {
        var separatorPositions = new List<int>();
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '|')
            {
                separatorPositions.Add(index);
            }
        }

        if (separatorPositions.Count + 1 <= maximumLines)
        {
            return text;
        }

        var retainedSeparators = SelectRetainedSeparators(text.Length, separatorPositions, maximumLines - 1);
        var rows = text.Split('|');
        var outputRows = new List<string>(maximumLines);
        var currentRow = rows[0];
        for (var separatorIndex = 0; separatorIndex < separatorPositions.Count; separatorIndex++)
        {
            var nextRow = rows[separatorIndex + 1];
            if (retainedSeparators.Contains(separatorPositions[separatorIndex]))
            {
                outputRows.Add(currentRow);
                currentRow = nextRow;
            }
            else
            {
                currentRow = JoinRows(currentRow, nextRow);
            }
        }

        outputRows.Add(currentRow);
        return string.Join('|', outputRows);
    }

    private static HashSet<int> SelectRetainedSeparators(
        int textLength,
        IReadOnlyList<int> separatorPositions,
        int separatorsToRetain)
    {
        var selected = new HashSet<int>();
        if (separatorsToRetain <= 0)
        {
            return selected;
        }

        var interval = textLength / (float)(separatorsToRetain + 1);
        for (var targetNumber = 1; targetNumber <= separatorsToRetain; targetNumber++)
        {
            var target = (int)(targetNumber * interval);
            var bestPosition = -1;
            var bestDistance = int.MaxValue;
            foreach (var position in separatorPositions)
            {
                if (selected.Contains(position))
                {
                    continue;
                }

                var distance = Math.Abs(target - position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestPosition = position;
            }

            if (bestPosition >= 0)
            {
                selected.Add(bestPosition);
            }
        }

        return selected;
    }

    private static string JoinRows(string left, string right)
    {
        var leftLocalControls = GetLeadingLocalMicroDvdControls(left);
        var rightLocalControls = GetLeadingLocalMicroDvdControls(right);
        var bothLeadingItalic = HasLeadingLegacyItalic(left) && HasLeadingLegacyItalic(right);
        var bothTrailingItalic = HasTrailingLegacyItalic(left) && HasTrailingLegacyItalic(right);

        if (leftLocalControls.Length > 0 && leftLocalControls == rightLocalControls)
        {
            right = right[rightLocalControls.Length..];
        }
        if (bothLeadingItalic)
        {
            right = RemoveLeadingLegacyItalic(right);
        }
        if (bothTrailingItalic)
        {
            left = RemoveTrailingLegacyItalic(left);
        }

        left = left.TrimEnd();
        right = right.TrimStart();
        return left.Length == 0
            ? right
            : right.Length == 0
                ? left
                : left + " " + right;
    }

    private static string GetLeadingLocalMicroDvdControls(string text)
    {
        var endOfPrefix = 0;
        for (var index = 0; index < text.Length && text[index] == '{';)
        {
            var end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                break;
            }

            var code = text[(index + 1)..end];
            if (code.Length < 2 || code[1] != ':' || !char.IsLower(code[0]))
            {
                break;
            }

            endOfPrefix = end + 1;
            index = end + 1;
        }

        return text[..endOfPrefix];
    }

    private static bool HasLeadingLegacyItalic(string text)
    {
        var index = GetLeadingLocalMicroDvdControls(text).Length;
        return index < text.Length && text[index] == '/';
    }

    private static string RemoveLeadingLegacyItalic(string text)
    {
        var index = GetLeadingLocalMicroDvdControls(text).Length;
        return index < text.Length && text[index] == '/'
            ? text.Remove(index, 1)
            : text;
    }

    private static bool HasTrailingLegacyItalic(string text) => text.TrimEnd().EndsWith('/');

    private static string RemoveTrailingLegacyItalic(string text)
    {
        var end = text.Length - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end]))
        {
            end--;
        }

        return end >= 0 && text[end] == '/'
            ? text.Remove(end, 1)
            : text;
    }

    private static bool TrySeparatePrefix(string line, out string prefix, out string text)
    {
        foreach (var pattern in new[] { MicroDvdPattern, Mpl2Pattern, TmPlayerPattern })
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            prefix = match.Groups["prefix"].Value;
            text = match.Groups["text"].Value;
            return true;
        }

        prefix = string.Empty;
        text = line;
        return false;
    }

    private static List<SrtCue> ParseSrtCues(IReadOnlyList<string> lines)
    {
        var cues = new List<SrtCue>();
        for (var index = 0; index < lines.Count;)
        {
            while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }
            if (index >= lines.Count)
            {
                break;
            }

            var blockStart = index;
            if (int.TryParse(lines[index].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) && index + 1 < lines.Count)
            {
                index++;
            }
            if (index >= lines.Count || !SrtTimingPattern.IsMatch(lines[index]))
            {
                index = blockStart + 1;
                continue;
            }

            index++;
            var textStart = index;
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            cues.Add(new SrtCue(blockStart, index, textStart, index - textStart));
        }

        return cues;
    }

    private readonly record struct SrtCue(int BlockStart, int BlockEndExclusive, int TextStart, int TextLineCount);
}

internal readonly record struct SubtitleLineLimitResult(string Text, int ModifiedLineCount);
