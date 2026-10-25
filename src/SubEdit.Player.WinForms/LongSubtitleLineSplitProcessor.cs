using SubEdit.Player.Core.Configuration;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class LongSubtitleLineSplitProcessor
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
    private static readonly Regex HtmlTagPattern = new(
        @"^<\s*(?<closing>/)?\s*(?<name>i|b|u|s|font)\b[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static LongSubtitleLineSplitResult Process(
        string text,
        LongSubtitleLineSplitSettings options,
        bool splitLongLines,
        int firstLine,
        int lastLine,
        bool treatUnrecognizedAsText)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return new LongSubtitleLineSplitResult(text, 0, 0);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);

        return SubtitleFormatConverter.IsSrtDocument(text)
            ? ProcessSrt(lines, options, splitLongLines, firstLine, lastLine, lineBreak)
            : ProcessInline(lines, options, splitLongLines, firstLine, lastLine, lineBreak, treatUnrecognizedAsText);
    }

    private static LongSubtitleLineSplitResult ProcessInline(
        List<string> lines,
        LongSubtitleLineSplitSettings options,
        bool splitLongLines,
        int firstLine,
        int lastLine,
        string lineBreak,
        bool treatUnrecognizedAsText)
    {
        var splitLineCount = 0;
        var removedSplitterCount = 0;
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

            var result = ProcessText(subtitleText, options, splitLongLines);
            lines[lineIndex] = prefix + result.Text;
            splitLineCount += result.WasSplit ? 1 : 0;
            removedSplitterCount += result.RemovedSplitterCount;
        }

        return new LongSubtitleLineSplitResult(
            string.Join(lineBreak, lines),
            splitLineCount,
            removedSplitterCount);
    }

    private static LongSubtitleLineSplitResult ProcessSrt(
        List<string> lines,
        LongSubtitleLineSplitSettings options,
        bool splitLongLines,
        int firstLine,
        int lastLine,
        string lineBreak)
    {
        var cues = ParseSrtCues(lines);
        var splitLineCount = 0;
        var removedSplitterCount = 0;
        foreach (var cue in cues.OrderByDescending(cue => cue.TextStart))
        {
            if (cue.BlockStart < firstLine || cue.BlockEndExclusive - 1 > lastLine)
            {
                continue;
            }

            var subtitleText = string.Join('|', lines.Skip(cue.TextStart).Take(cue.TextLineCount));
            var result = ProcessText(subtitleText, options, splitLongLines);
            if (result.Text == subtitleText)
            {
                continue;
            }

            var replacement = result.Text.Split('|').ToList();
            lines.RemoveRange(cue.TextStart, cue.TextLineCount);
            lines.InsertRange(cue.TextStart, replacement);
            splitLineCount += result.WasSplit ? 1 : 0;
            removedSplitterCount += result.RemovedSplitterCount;
        }

        return new LongSubtitleLineSplitResult(
            string.Join(lineBreak, lines),
            splitLineCount,
            removedSplitterCount);
    }

    private static ProcessedText ProcessText(
        string text,
        LongSubtitleLineSplitSettings options,
        bool splitLongLines)
    {
        var workingText = text;
        var removedSplitterCount = 0;
        if (!splitLongLines || options.RemoveExistingSplitters)
        {
            (workingText, removedSplitterCount) = RemoveSplitters(
                workingText,
                splitLongLines && options.PreserveSplittersBeforeDialogues);
        }

        if (!splitLongLines)
        {
            return new ProcessedText(workingText, false, removedSplitterCount);
        }

        var rows = workingText.Split('|').ToList();
        if (options.LimitLineCount && rows.Count >= options.MaximumLines)
        {
            return new ProcessedText(workingText, false, removedSplitterCount);
        }

        var longestRowIndex = 0;
        for (var index = 1; index < rows.Count; index++)
        {
            if (rows[index].Length > rows[longestRowIndex].Length)
            {
                longestRowIndex = index;
            }
        }

        var longestRow = rows[longestRowIndex];
        if (longestRow.Length <= options.MaximumLineLength)
        {
            return new ProcessedText(workingText, false, removedSplitterCount);
        }

        var splitCount = Math.Max(1, (int)(workingText.Length / (double)options.MaximumLineLength + 0.13d));
        if (options.LimitLineCount)
        {
            splitCount = Math.Min(splitCount, options.MaximumLines - rows.Count);
        }

        var splitIndices = FindSplitIndices(longestRow, splitCount);
        if (splitIndices.Count == 0)
        {
            return new ProcessedText(workingText, false, removedSplitterCount);
        }

        rows[longestRowIndex] = SplitPreservingFormatting(longestRow, splitIndices);
        return new ProcessedText(string.Join('|', rows), true, removedSplitterCount);
    }

    private static List<int> FindSplitIndices(string row, int splitCount)
    {
        var indices = new List<int>(splitCount);
        if (splitCount <= 0 || row.Length == 0)
        {
            return indices;
        }

        var segmentLength = row.Length / (splitCount + 1);
        for (var splitNumber = 1; splitNumber <= splitCount; splitNumber++)
        {
            var target = splitNumber * segmentLength;
            var found = -1;
            for (var offset = 0; target - offset >= 0 || target + offset < row.Length; offset++)
            {
                var left = target - offset;
                if (left >= 0 && IsBreakableSpace(row, left) && !indices.Contains(left))
                {
                    found = left;
                    break;
                }

                var right = target + offset;
                if (right < row.Length && IsBreakableSpace(row, right) && !indices.Contains(right))
                {
                    found = right;
                    break;
                }
            }

            if (found < 0)
            {
                break;
            }

            indices.Add(found);
        }

        indices.Sort();
        return indices;
    }

    private static bool IsBreakableSpace(string text, int index)
    {
        if (text[index] != ' ')
        {
            return false;
        }

        var braceStart = text.LastIndexOf('{', index);
        var braceEnd = text.LastIndexOf('}', index);
        if (braceStart > braceEnd)
        {
            return false;
        }

        var tagStart = text.LastIndexOf('<', index);
        var tagEnd = text.LastIndexOf('>', index);
        return tagStart <= tagEnd;
    }

    private static string SplitPreservingFormatting(string row, IReadOnlyList<int> splitIndices)
    {
        var localControlPrefix = GetLeadingLocalMicroDvdControls(row);
        var leadingLegacyItalic = HasLeadingLegacyItalic(row);
        var trailingLegacyItalic = HasTrailingLegacyItalic(row);
        var parts = new List<string>(splitIndices.Count + 1);
        var start = 0;
        for (var partIndex = 0; partIndex <= splitIndices.Count; partIndex++)
        {
            var end = partIndex < splitIndices.Count ? splitIndices[partIndex] : row.Length;
            var part = row[start..end].Trim();
            var prefix = new StringBuilder();
            var suffix = new StringBuilder();

            if (start > 0)
            {
                prefix.Append(localControlPrefix);
                if (leadingLegacyItalic)
                {
                    prefix.Append('/');
                }

                foreach (var tag in GetActiveHtmlTags(row, start))
                {
                    prefix.Append(tag.OpeningText);
                }
            }

            if (end < row.Length)
            {
                var activeTags = GetActiveHtmlTags(row, end);
                for (var index = activeTags.Count - 1; index >= 0; index--)
                {
                    suffix.Append("</").Append(activeTags[index].Name).Append('>');
                }

                if (trailingLegacyItalic)
                {
                    suffix.Append('/');
                }
            }

            parts.Add(prefix + part + suffix.ToString());
            start = end + 1;
        }

        return string.Join('|', parts);
    }

    private static (string Text, int RemovedCount) RemoveSplitters(string text, bool preserveBeforeDialogues)
    {
        var builder = new StringBuilder(text.Length);
        var removedCount = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '|')
            {
                builder.Append(text[index]);
                continue;
            }

            if (preserveBeforeDialogues && IsDialogueAfter(text, index + 1))
            {
                builder.Append('|');
                continue;
            }

            while (builder.Length > 0 && char.IsWhiteSpace(builder[^1]))
            {
                builder.Length--;
            }

            var next = index + 1;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            if (builder.Length > 0 && next < text.Length)
            {
                builder.Append(' ');
            }

            index = next - 1;
            removedCount++;
        }

        return (builder.ToString(), removedCount);
    }

    private static bool IsDialogueAfter(string text, int index)
    {
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index < text.Length && text[index] == '{')
            {
                var end = text.IndexOf('}', index + 1);
                if (end > index)
                {
                    index = end + 1;
                    continue;
                }
            }

            if (index < text.Length && text[index] == '<')
            {
                var end = text.IndexOf('>', index + 1);
                if (end > index)
                {
                    index = end + 1;
                    continue;
                }
            }

            if (index < text.Length && text[index] == '/')
            {
                index++;
                continue;
            }

            break;
        }

        return index < text.Length && text[index] == '-';
    }

    private static string GetLeadingLocalMicroDvdControls(string text)
    {
        var result = new StringBuilder();
        for (var index = 0; index < text.Length && text[index] == '{';)
        {
            var end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                break;
            }

            var code = text[(index + 1)..end];
            if (code.Length < 2 || code[1] != ':')
            {
                break;
            }

            if (char.IsLower(code[0]))
            {
                result.Append(text, index, end - index + 1);
            }
            index = end + 1;
        }

        return result.ToString();
    }

    private static bool HasLeadingLegacyItalic(string text)
    {
        var index = 0;
        while (index < text.Length && text[index] == '{')
        {
            var end = text.IndexOf('}', index + 1);
            if (end < 0)
            {
                break;
            }

            index = end + 1;
        }

        return index < text.Length && text[index] == '/';
    }

    private static bool HasTrailingLegacyItalic(string text) => text.TrimEnd().EndsWith('/');

    private static List<HtmlTag> GetActiveHtmlTags(string text, int endExclusive)
    {
        var active = new List<HtmlTag>();
        for (var index = 0; index < Math.Min(endExclusive, text.Length);)
        {
            if (text[index] != '<')
            {
                index++;
                continue;
            }

            var match = HtmlTagPattern.Match(text[index..]);
            if (!match.Success)
            {
                index++;
                continue;
            }

            var name = match.Groups["name"].Value.ToLowerInvariant();
            if (match.Groups["closing"].Success)
            {
                var openIndex = active.FindLastIndex(tag => tag.Name == name);
                if (openIndex >= 0)
                {
                    active.RemoveRange(openIndex, active.Count - openIndex);
                }
            }
            else
            {
                active.Add(new HtmlTag(name, match.Value));
            }

            index += match.Length;
        }

        return active;
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

    private readonly record struct ProcessedText(string Text, bool WasSplit, int RemovedSplitterCount);
    private readonly record struct SrtCue(int BlockStart, int BlockEndExclusive, int TextStart, int TextLineCount);
    private readonly record struct HtmlTag(string Name, string OpeningText);
}

internal readonly record struct LongSubtitleLineSplitResult(
    string Text,
    int SplitLineCount,
    int RemovedSplitterCount);
