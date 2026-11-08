using System.Globalization;

namespace SubEdit.Player.WinForms;

internal static class SrtCueBlocks
{
    public static List<Cue> Parse(string text, out int? invalidLine)
    {
        invalidLine = null;
        IReadOnlyList<string> lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = new List<Cue>();
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

            var timingIndex = index;
            if (int.TryParse(lines[index].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) &&
                index + 1 < lines.Count)
            {
                timingIndex++;
            }

            var timing = SubtitleTimingFormatter.SrtTimingLinePartsPattern.Match(lines[timingIndex]);
            if (!timing.Success ||
                !SubtitleTimingFormatter.TryParseSrtTimestamp(timing.Groups["start"].Value, out var start) ||
                !SubtitleTimingFormatter.TryParseSrtTimestamp(timing.Groups["end"].Value, out var end))
            {
                // For a numbered but unfinished block, point to its number if there is no timing line.
                invalidLine = (string.IsNullOrWhiteSpace(lines[timingIndex]) ? index : timingIndex) + 1;
                break;
            }

            var textStartIndex = timingIndex + 1;
            var textEndIndex = textStartIndex;
            while (textEndIndex < lines.Count && !string.IsNullOrWhiteSpace(lines[textEndIndex]))
            {
                textEndIndex++;
            }

            var textLines = lines.Skip(textStartIndex).Take(textEndIndex - textStartIndex).ToArray();
            cues.Add(new Cue(start, end, lines[timingIndex], textLines));
            index = textEndIndex;
        }

        return cues;
    }

    public static string ReplaceTiming(string timingLine, long startMilliseconds, long endMilliseconds)
    {
        var match = SubtitleTimingFormatter.SrtTimingLinePartsPattern.Match(timingLine);
        if (!match.Success)
        {
            return timingLine;
        }

        var separator = match.Groups["start"].Value.Contains('.') ? '.' : ',';
        return $"{match.Groups["before"].Value}" +
            $"{SubtitleTimingFormatter.FormatSrtTimestamp(startMilliseconds, separator)}" +
            $"{match.Groups["arrow"].Value}" +
            $"{SubtitleTimingFormatter.FormatSrtTimestamp(endMilliseconds, separator)}" +
            $"{match.Groups["after"].Value}";
    }

    internal sealed record Cue(
        long StartMilliseconds,
        long EndMilliseconds,
        string TimingLine,
        string[] TextLines);
}
