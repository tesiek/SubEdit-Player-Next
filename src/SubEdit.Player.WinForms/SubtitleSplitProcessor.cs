using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal readonly record struct SubtitleSplitResult(
    string FirstPartText,
    string RemainderText,
    int FirstPartCueCount,
    int RemainderCueCount);

internal static partial class SubtitleSplitProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex TmPlayerPattern = new(
        @"^(?<before>\s*)(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);

    [GeneratedRegex(@"^(?<before>\s*)(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<arrow>\s*-->\s*)(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<after>.*)$")]
    private static partial Regex SrtTimingPattern();

    public static bool CanSplit(string text, long durationMilliseconds, double framesPerSecond)
    {
        if (string.IsNullOrWhiteSpace(text) || durationMilliseconds <= 0)
        {
            return false;
        }

        if (SubtitleFormatConverter.IsSrtDocument(text))
        {
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            var cues = ParseSrtCues(lines);
            return cues.Count > 0 && cues.Any(cue => cue.StartMilliseconds >= durationMilliseconds);
        }

        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        var rawLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var hasAnyCue = false;
        var hasRemainderCue = false;

        foreach (var line in rawLines)
        {
            if (SubtitleDocument.TryGetCueStartMilliseconds(line, safeFramesPerSecond, out var startMilliseconds))
            {
                hasAnyCue = true;
                if (startMilliseconds >= durationMilliseconds)
                {
                    hasRemainderCue = true;
                    break;
                }
            }
        }

        return hasAnyCue && hasRemainderCue;
    }

    public static SubtitleSplitResult Split(
        string text,
        long durationMilliseconds,
        double framesPerSecond)
    {
        if (string.IsNullOrEmpty(text) || durationMilliseconds <= 0)
        {
            return new SubtitleSplitResult(text, string.Empty, 0, 0);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return SubtitleFormatConverter.IsSrtDocument(text)
            ? SplitSrt(text, durationMilliseconds, lineBreak)
            : SplitInline(text, durationMilliseconds, Math.Max(1d, framesPerSecond), lineBreak);
    }

    private static SubtitleSplitResult SplitSrt(
        string text,
        long durationMilliseconds,
        string lineBreak)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = ParseSrtCues(lines);

        var firstPartCues = new List<SrtCueBlock>();
        var remainderCues = new List<SrtCueBlock>();

        foreach (var cue in cues)
        {
            if (cue.StartMilliseconds < durationMilliseconds)
            {
                firstPartCues.Add(cue);
            }
            else
            {
                remainderCues.Add(cue);
            }
        }

        var firstPartBlocks = new List<string>(firstPartCues.Count);
        for (var index = 0; index < firstPartCues.Count; index++)
        {
            var cue = firstPartCues[index];
            var blockLines = new List<string>(cue.TextLines.Length + 2)
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                cue.TimingLine
            };
            blockLines.AddRange(cue.TextLines);
            firstPartBlocks.Add(string.Join(lineBreak, blockLines));
        }

        var remainderBlocks = new List<string>(remainderCues.Count);
        for (var index = 0; index < remainderCues.Count; index++)
        {
            var cue = remainderCues[index];
            var newStart = Math.Max(0, cue.StartMilliseconds - durationMilliseconds);
            var newEnd = Math.Max(0, cue.EndMilliseconds - durationMilliseconds);
            var shiftedTimingLine = ReplaceSrtTiming(cue.TimingLine, newStart, newEnd);

            var blockLines = new List<string>(cue.TextLines.Length + 2)
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                shiftedTimingLine
            };
            blockLines.AddRange(cue.TextLines);
            remainderBlocks.Add(string.Join(lineBreak, blockLines));
        }

        var blockSeparator = $"{lineBreak}{lineBreak}";
        var firstPartText = string.Join(blockSeparator, firstPartBlocks);
        var remainderText = string.Join(blockSeparator, remainderBlocks);

        return new SubtitleSplitResult(
            firstPartText,
            remainderText,
            firstPartCues.Count,
            remainderCues.Count);
    }

    private static SubtitleSplitResult SplitInline(
        string text,
        long durationMilliseconds,
        double framesPerSecond,
        string lineBreak)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var firstPartLines = new List<string>();
        var remainderLines = new List<string>();
        var firstPartCueCount = 0;
        var remainderCueCount = 0;

        foreach (var line in lines)
        {
            if (SubtitleDocument.TryGetCueStartMilliseconds(line, framesPerSecond, out var startMilliseconds))
            {
                if (startMilliseconds < durationMilliseconds)
                {
                    firstPartLines.Add(line);
                    firstPartCueCount++;
                }
                else
                {
                    var shiftedLine = ShiftInlineLine(line, durationMilliseconds, framesPerSecond);
                    remainderLines.Add(shiftedLine);
                    remainderCueCount++;
                }
            }
            else
            {
                if (remainderCueCount == 0)
                {
                    firstPartLines.Add(line);
                }
                else
                {
                    remainderLines.Add(line);
                }
            }
        }

        return new SubtitleSplitResult(
            string.Join(lineBreak, firstPartLines),
            string.Join(lineBreak, remainderLines),
            firstPartCueCount,
            remainderCueCount);
    }

    private static string ShiftInlineLine(
        string line,
        long durationMilliseconds,
        double framesPerSecond)
    {
        var microDvd = MicroDvdPattern.Match(line);
        if (microDvd.Success && long.TryParse(microDvd.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startFrame))
        {
            var durationFrames = (long)Math.Round(durationMilliseconds * framesPerSecond / 1000d, MidpointRounding.AwayFromZero);
            var newStartFrame = Math.Max(0, startFrame - durationFrames);
            var endText = microDvd.Groups["end"].Value;
            var newEndText = string.Empty;
            if (endText.Length > 0 && long.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endFrame))
            {
                var newEndFrame = endFrame - durationFrames;
                newEndText = newEndFrame.ToString(CultureInfo.InvariantCulture);
            }

            return $"{{{newStartFrame.ToString(CultureInfo.InvariantCulture)}}}" +
                $"{{{newEndText}}}" +
                microDvd.Groups["text"].Value;
        }

        var mpl2 = Mpl2Pattern.Match(line);
        if (mpl2.Success && long.TryParse(mpl2.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startDeci))
        {
            var durationDeci = (long)Math.Round(durationMilliseconds / 100d, MidpointRounding.AwayFromZero);
            var newStartDeci = Math.Max(0, startDeci - durationDeci);
            var endText = mpl2.Groups["end"].Value;
            var newEndText = string.Empty;
            if (endText.Length > 0 && long.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endDeci))
            {
                var newEndDeci = endDeci - durationDeci;
                newEndText = newEndDeci.ToString(CultureInfo.InvariantCulture);
            }

            return $"[{newStartDeci.ToString(CultureInfo.InvariantCulture)}]" +
                $"[{newEndText}]" +
                mpl2.Groups["text"].Value;
        }

        var tmPlayer = TmPlayerPattern.Match(line);
        if (tmPlayer.Success && TryReadTmPlayerSeconds(tmPlayer, out var tmPlayerSeconds))
        {
            var durationSeconds = (long)Math.Round(durationMilliseconds / 1000d, MidpointRounding.AwayFromZero);
            var newTotalSeconds = Math.Max(0, tmPlayerSeconds - durationSeconds);
            var seconds = newTotalSeconds % 60;
            var minutes = (newTotalSeconds / 60) % 60;
            var hours = newTotalSeconds / 3600;
            var originalHours = tmPlayer.Groups["hours"].Value;
            var hoursText = originalHours.Length == 1
                ? hours.ToString(CultureInfo.InvariantCulture)
                : hours.ToString($"D{originalHours.Length}", CultureInfo.InvariantCulture);

            return $"{tmPlayer.Groups["before"].Value}{hoursText}:{minutes:D2}:{seconds:D2}" +
                $"{tmPlayer.Groups["separator"].Value}{tmPlayer.Groups["text"].Value}";
        }

        return line;
    }

    private static bool TryReadTmPlayerSeconds(Match match, out long totalSeconds) =>
        SubtitleTimingFormatter.TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out totalSeconds);

    private static List<SrtCueBlock> ParseSrtCues(IReadOnlyList<string> lines)
    {
        var cues = new List<SrtCueBlock>();
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

            var timing = SrtTimingPattern().Match(lines[timingIndex]);
            if (!timing.Success ||
                !TryParseSrtTimestamp(timing.Groups["start"].Value, out var start) ||
                !TryParseSrtTimestamp(timing.Groups["end"].Value, out var end))
            {
                while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
                {
                    index++;
                }
                continue;
            }

            var textStartIndex = timingIndex + 1;
            var textEndIndex = textStartIndex;
            while (textEndIndex < lines.Count && !string.IsNullOrWhiteSpace(lines[textEndIndex]))
            {
                textEndIndex++;
            }

            var textLines = lines.Skip(textStartIndex).Take(textEndIndex - textStartIndex).ToArray();
            cues.Add(new SrtCueBlock(start, end, lines[timingIndex], textLines));
            index = textEndIndex;
        }

        return cues;
    }

    private static bool TryParseSrtTimestamp(string text, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(text, out milliseconds);

    private static string ReplaceSrtTiming(string timingLine, long startMilliseconds, long endMilliseconds)
    {
        var match = SrtTimingPattern().Match(timingLine);
        if (!match.Success)
        {
            return timingLine;
        }

        var separator = match.Groups["start"].Value.Contains('.') ? '.' : ',';
        return $"{match.Groups["before"].Value}" +
            $"{FormatSrtTimestamp(startMilliseconds, separator)}" +
            $"{match.Groups["arrow"].Value}" +
            $"{FormatSrtTimestamp(endMilliseconds, separator)}" +
            $"{match.Groups["after"].Value}";
    }

    private static string FormatSrtTimestamp(long milliseconds, char separator) =>
        SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, separator);

    private sealed record SrtCueBlock(
        long StartMilliseconds,
        long EndMilliseconds,
        string TimingLine,
        string[] TextLines);

    public static string? GenerateNextNumberedPartPath(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(currentPath);
        var fileName = Path.GetFileNameWithoutExtension(currentPath);
        var extension = Path.GetExtension(currentPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(extension))
        {
            return null;
        }

        var numberStart = fileName.Length;
        while (numberStart > 0 && fileName[numberStart - 1] is >= '0' and <= '9')
        {
            numberStart--;
        }

        if (numberStart == fileName.Length)
        {
            return null;
        }

        var numberText = fileName[numberStart..];
        if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var partNumber) || partNumber == int.MaxValue)
        {
            return null;
        }

        var nextNumber = (partNumber + 1).ToString(CultureInfo.InvariantCulture).PadLeft(numberText.Length, '0');
        return Path.Combine(directory, fileName[..numberStart] + nextNumber + extension);
    }

    public static string GetSuggestedRemainderPath(string savedFirstPartPath)
    {
        var generated = GenerateNextNumberedPartPath(savedFirstPartPath);
        if (!string.IsNullOrWhiteSpace(generated) &&
            !string.Equals(generated, savedFirstPartPath, StringComparison.OrdinalIgnoreCase))
        {
            return generated;
        }

        var directory = Path.GetDirectoryName(savedFirstPartPath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(savedFirstPartPath);
        var extension = Path.GetExtension(savedFirstPartPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".txt";
        }

        var fallbackName = $"{fileNameWithoutExt}_CD2{extension}";
        return string.IsNullOrWhiteSpace(directory)
            ? fallbackName
            : Path.Combine(directory, fallbackName);
    }
}
