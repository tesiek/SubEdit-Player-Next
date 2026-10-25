using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class SubtitleSortProcessor
{
    [GeneratedRegex(@"^\s*(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})")]
    private static partial Regex SrtTimingPattern();

    public static string Sort(
        string text,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return text;
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);

        if (SubtitleFormatConverter.IsSrtDocument(text))
        {
            SortSrt(lines, firstLine, lastLine);
        }
        else
        {
            SortInline(lines, Math.Max(1d, framesPerSecond), firstLine, lastLine);
        }

        return string.Join(lineBreak, lines);
    }

    private static void SortInline(
        IList<string> lines,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        var timedLines = new List<TimedLine>();
        for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
        {
            if (SubtitleDocument.TryGetCueStartMilliseconds(lines[lineIndex], framesPerSecond, out var start))
            {
                timedLines.Add(new TimedLine(lineIndex, start, lines[lineIndex]));
            }
        }

        var sortedLines = timedLines.OrderBy(line => line.StartMilliseconds).ToArray();
        for (var index = 0; index < timedLines.Count; index++)
        {
            lines[timedLines[index].LineIndex] = sortedLines[index].Text;
        }
    }

    private static void SortSrt(List<string> lines, int firstLine, int lastLine)
    {
        var allCues = ParseSrtCues(lines);
        var cues = allCues
            .Where(cue => cue.BlockStart >= firstLine && cue.BlockEndExclusive - 1 <= lastLine)
            .ToArray();
        if (cues.Length < 2)
        {
            return;
        }

        var startIndex = allCues.TakeWhile(cue => cue.BlockStart < cues[0].BlockStart).Count() + 1;
        var sortedBlocks = cues
            .OrderBy(cue => cue.StartMilliseconds)
            .Select((cue, orderIndex) =>
            {
                var blockLines = cue.Lines.ToArray();
                var newNumber = (startIndex + orderIndex).ToString(CultureInfo.InvariantCulture);
                if (cue.HasIndex)
                {
                    blockLines[0] = newNumber;
                    return blockLines;
                }

                var withNumber = new string[blockLines.Length + 1];
                withNumber[0] = newNumber;
                Array.Copy(blockLines, 0, withNumber, 1, blockLines.Length);
                return withNumber;
            })
            .ToArray();
        for (var index = cues.Length - 1; index >= 0; index--)
        {
            var destination = cues[index];
            lines.RemoveRange(destination.BlockStart, destination.BlockEndExclusive - destination.BlockStart);
            lines.InsertRange(destination.BlockStart, sortedBlocks[index]);
        }
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
            var timingLine = index;
            var hasIndex = false;
            if (int.TryParse(lines[index].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) &&
                index + 1 < lines.Count)
            {
                timingLine++;
                hasIndex = true;
            }

            var timing = SrtTimingPattern().Match(lines[timingLine]);
            if (!timing.Success || !TryParseSrtTimestamp(timing.Groups["start"].Value, out var start))
            {
                while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
                {
                    index++;
                }
                continue;
            }

            index = timingLine + 1;
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            cues.Add(new SrtCue(
                blockStart,
                index,
                start,
                lines.Skip(blockStart).Take(index - blockStart).ToArray(),
                hasIndex));
        }

        return cues;
    }

    private static bool TryParseSrtTimestamp(string text, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(text, out milliseconds);

    private readonly record struct TimedLine(int LineIndex, long StartMilliseconds, string Text);

    private sealed record SrtCue(
        int BlockStart,
        int BlockEndExclusive,
        long StartMilliseconds,
        string[] Lines,
        bool HasIndex);
}
