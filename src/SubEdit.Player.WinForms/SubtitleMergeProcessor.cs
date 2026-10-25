using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal enum SubtitleLogicalFormat
{
    Srt,
    MicroDvd,
    Mpl2,
    TmPlayer
}

internal readonly record struct SubtitleMergeParsedCue(
    int Index,
    long StartUnits,
    long? EndUnits,
    string DisplayText);

internal readonly record struct SubtitleMergeParseResult(
    SubtitleLogicalFormat Format,
    IReadOnlyList<SubtitleMergeParsedCue> Cues,
    IReadOnlyList<string> DisplayItems);

internal readonly record struct SubtitleMergeResult(
    string MergedText,
    int ImportedCueCount);

internal static partial class SubtitleMergeProcessor
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

    public static SubtitleLogicalFormat? DetectLogicalFormat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (SubtitleFormatConverter.IsSrtDocument(text))
        {
            return SubtitleLogicalFormat.Srt;
        }

        return SubtitleTimingFormatter.Detect(text) switch
        {
            SubtitleTimingFormat.MicroDvd => SubtitleLogicalFormat.MicroDvd,
            SubtitleTimingFormat.Mpl2 => SubtitleLogicalFormat.Mpl2,
            SubtitleTimingFormat.TmPlayerLong or SubtitleTimingFormat.TmPlayerShort => SubtitleLogicalFormat.TmPlayer,
            _ => null
        };
    }

    public static SubtitleMergeParseResult ParseForMerge(string text, SubtitleLogicalFormat format)
    {
        return format switch
        {
            SubtitleLogicalFormat.Srt => ParseSrtForMerge(text),
            SubtitleLogicalFormat.MicroDvd => ParseMicroDvdForMerge(text),
            SubtitleLogicalFormat.Mpl2 => ParseMpl2ForMerge(text),
            SubtitleLogicalFormat.TmPlayer => ParseTmPlayerForMerge(text),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static SubtitleMergeResult Merge(
        string activeText,
        string importedText,
        SubtitleLogicalFormat format,
        int selectedCueIndex,
        long positionMilliseconds,
        double? scale,
        double framesPerSecond)
    {
        return format switch
        {
            SubtitleLogicalFormat.Srt => MergeSrt(activeText, importedText, selectedCueIndex, positionMilliseconds, scale),
            SubtitleLogicalFormat.MicroDvd => MergeMicroDvd(activeText, importedText, selectedCueIndex, positionMilliseconds, scale, framesPerSecond),
            SubtitleLogicalFormat.Mpl2 => MergeMpl2(activeText, importedText, selectedCueIndex, positionMilliseconds, scale),
            SubtitleLogicalFormat.TmPlayer => MergeTmPlayer(activeText, importedText, selectedCueIndex, positionMilliseconds, scale),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static long RoundLikeOriginal(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);

    // ==========================================
    // SRT
    // ==========================================

    private static SubtitleMergeParseResult ParseSrtForMerge(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var rawCues = ParseSrtCues(lines);
        var cues = new List<SubtitleMergeParsedCue>(rawCues.Count);
        var displayItems = new List<string>(rawCues.Count);

        for (var index = 0; index < rawCues.Count; index++)
        {
            var cue = rawCues[index];
            var joinedText = string.Join(" | ", cue.TextLines.Select(l => l.Trim()));
            var display = $"[{FormatSrtTimestamp(cue.StartMilliseconds, ',')}] {joinedText}";
            cues.Add(new SubtitleMergeParsedCue(index, cue.StartMilliseconds, cue.EndMilliseconds, display));
            displayItems.Add(display);
        }

        return new SubtitleMergeParseResult(SubtitleLogicalFormat.Srt, cues, displayItems);
    }

    private static SubtitleMergeResult MergeSrt(
        string activeText,
        string importedText,
        int selectedCueIndex,
        long positionMilliseconds,
        double? scale,
        string? customLineBreak = null)
    {
        var activeLines = activeText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var importedLines = importedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        var activeCues = ParseSrtCues(activeLines);
        var importedCues = ParseSrtCues(importedLines);

        if (importedCues.Count == 0 || selectedCueIndex < 0 || selectedCueIndex >= importedCues.Count)
        {
            return new SubtitleMergeResult(activeText, 0);
        }

        var scaledStarts = new long[importedCues.Count];
        var scaledEnds = new long[importedCues.Count];

        for (var index = 0; index < importedCues.Count; index++)
        {
            var cue = importedCues[index];
            if (scale is { } scaleFactor)
            {
                scaledStarts[index] = RoundLikeOriginal(cue.StartMilliseconds * scaleFactor);
                scaledEnds[index] = RoundLikeOriginal(cue.EndMilliseconds * scaleFactor);
            }
            else
            {
                scaledStarts[index] = cue.StartMilliseconds;
                scaledEnds[index] = cue.EndMilliseconds;
            }
        }

        var syncStart = scaledStarts[selectedCueIndex];
        var delta = positionMilliseconds - syncStart;

        var lineBreak = customLineBreak ?? (activeText.Contains("\r\n", StringComparison.Ordinal) || importedText.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n");
        var blockSeparator = $"{lineBreak}{lineBreak}";

        var allBlocks = new List<string>(activeCues.Count + importedCues.Count);

        for (var index = 0; index < activeCues.Count; index++)
        {
            var cue = activeCues[index];
            var blockLines = new List<string>(cue.TextLines.Length + 2)
            {
                (index + 1).ToString(CultureInfo.InvariantCulture),
                cue.TimingLine
            };
            blockLines.AddRange(cue.TextLines);
            allBlocks.Add(string.Join(lineBreak, blockLines));
        }

        for (var index = 0; index < importedCues.Count; index++)
        {
            var cue = importedCues[index];
            var finalStart = scaledStarts[index] + delta;
            var finalEnd = scaledEnds[index] + delta;
            var shiftedTimingLine = ReplaceSrtTiming(cue.TimingLine, finalStart, finalEnd);

            var blockLines = new List<string>(cue.TextLines.Length + 2)
            {
                (activeCues.Count + index + 1).ToString(CultureInfo.InvariantCulture),
                shiftedTimingLine
            };
            blockLines.AddRange(cue.TextLines);
            allBlocks.Add(string.Join(lineBreak, blockLines));
        }

        var merged = string.Join(blockSeparator, allBlocks);
        return new SubtitleMergeResult(merged, importedCues.Count);
    }

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

    // ==========================================
    // MicroDVD
    // ==========================================

    private static SubtitleMergeParseResult ParseMicroDvdForMerge(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = new List<SubtitleMergeParsedCue>();
        var displayItems = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var match = MicroDvdPattern.Match(line);
            if (!match.Success || !long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startFrame))
            {
                continue;
            }

            long? endFrame = null;
            var endText = match.Groups["end"].Value;
            if (endText.Length > 0 && long.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnd))
            {
                endFrame = parsedEnd;
            }

            var textContent = match.Groups["text"].Value.Trim();
            var display = $"{{{startFrame}}}{{{(endFrame.HasValue ? endFrame.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}}} {textContent}";
            cues.Add(new SubtitleMergeParsedCue(cues.Count, startFrame, endFrame, display));
            displayItems.Add(display);
        }

        return new SubtitleMergeParseResult(SubtitleLogicalFormat.MicroDvd, cues, displayItems);
    }

    private static SubtitleMergeResult MergeMicroDvd(
        string activeText,
        string importedText,
        int selectedCueIndex,
        long positionMilliseconds,
        double? scale,
        double framesPerSecond)
    {
        var importedParse = ParseMicroDvdForMerge(importedText);
        if (importedParse.Cues.Count == 0 || selectedCueIndex < 0 || selectedCueIndex >= importedParse.Cues.Count)
        {
            return new SubtitleMergeResult(activeText, 0);
        }

        var safeFps = Math.Max(1d, framesPerSecond);
        var positionFrames = (long)Math.Round(positionMilliseconds * safeFps / 1000d, MidpointRounding.AwayFromZero);

        var scaledStarts = new long[importedParse.Cues.Count];
        var scaledEnds = new long?[importedParse.Cues.Count];

        for (var index = 0; index < importedParse.Cues.Count; index++)
        {
            var cue = importedParse.Cues[index];
            if (scale is { } scaleFactor)
            {
                scaledStarts[index] = RoundLikeOriginal(cue.StartUnits * scaleFactor);
                scaledEnds[index] = cue.EndUnits.HasValue
                    ? RoundLikeOriginal(cue.EndUnits.Value * scaleFactor)
                    : null;
            }
            else
            {
                scaledStarts[index] = cue.StartUnits;
                scaledEnds[index] = cue.EndUnits;
            }
        }

        var syncStart = scaledStarts[selectedCueIndex];
        var deltaFrames = positionFrames - syncStart;

        var lineBreak = activeText.Contains("\r\n", StringComparison.Ordinal) || importedText.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";

        var importedLines = importedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var transformedLines = new List<string>();
        var cueIndex = 0;

        foreach (var line in importedLines)
        {
            var match = MicroDvdPattern.Match(line);
            if (match.Success && long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                var finalStart = scaledStarts[cueIndex] + deltaFrames;
                var finalEnd = scaledEnds[cueIndex].HasValue
                    ? (scaledEnds[cueIndex]!.Value + deltaFrames).ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

                transformedLines.Add($"{{{finalStart.ToString(CultureInfo.InvariantCulture)}}}{{{finalEnd}}}{match.Groups["text"].Value}");
                cueIndex++;
            }
            else
            {
                transformedLines.Add(line);
            }
        }

        var activeTrimmed = activeText.TrimEnd('\r', '\n');
        var importedContent = string.Join(lineBreak, transformedLines);
        var merged = string.IsNullOrEmpty(activeTrimmed)
            ? importedContent
            : $"{activeTrimmed}{lineBreak}{importedContent}";

        return new SubtitleMergeResult(merged, importedParse.Cues.Count);
    }

    // ==========================================
    // MPL2
    // ==========================================

    private static SubtitleMergeParseResult ParseMpl2ForMerge(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = new List<SubtitleMergeParsedCue>();
        var displayItems = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var match = Mpl2Pattern.Match(line);
            if (!match.Success || !long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startDeci))
            {
                continue;
            }

            long? endDeci = null;
            var endText = match.Groups["end"].Value;
            if (endText.Length > 0 && long.TryParse(endText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedEnd))
            {
                endDeci = parsedEnd;
            }

            var textContent = match.Groups["text"].Value.Trim();
            var display = $"[{startDeci}][{(endDeci.HasValue ? endDeci.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}] {textContent}";
            cues.Add(new SubtitleMergeParsedCue(cues.Count, startDeci, endDeci, display));
            displayItems.Add(display);
        }

        return new SubtitleMergeParseResult(SubtitleLogicalFormat.Mpl2, cues, displayItems);
    }

    private static SubtitleMergeResult MergeMpl2(
        string activeText,
        string importedText,
        int selectedCueIndex,
        long positionMilliseconds,
        double? scale)
    {
        var importedParse = ParseMpl2ForMerge(importedText);
        if (importedParse.Cues.Count == 0 || selectedCueIndex < 0 || selectedCueIndex >= importedParse.Cues.Count)
        {
            return new SubtitleMergeResult(activeText, 0);
        }

        var positionDeci = (long)Math.Round(positionMilliseconds / 100d, MidpointRounding.AwayFromZero);

        var scaledStarts = new long[importedParse.Cues.Count];
        var scaledEnds = new long?[importedParse.Cues.Count];

        for (var index = 0; index < importedParse.Cues.Count; index++)
        {
            var cue = importedParse.Cues[index];
            if (scale is { } scaleFactor)
            {
                scaledStarts[index] = RoundLikeOriginal(cue.StartUnits * scaleFactor);
                scaledEnds[index] = cue.EndUnits.HasValue
                    ? RoundLikeOriginal(cue.EndUnits.Value * scaleFactor)
                    : null;
            }
            else
            {
                scaledStarts[index] = cue.StartUnits;
                scaledEnds[index] = cue.EndUnits;
            }
        }

        var syncStart = scaledStarts[selectedCueIndex];
        var deltaDeci = positionDeci - syncStart;

        var lineBreak = activeText.Contains("\r\n", StringComparison.Ordinal) || importedText.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";

        var importedLines = importedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var transformedLines = new List<string>();
        var cueIndex = 0;

        foreach (var line in importedLines)
        {
            var match = Mpl2Pattern.Match(line);
            if (match.Success && long.TryParse(match.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                var finalStart = scaledStarts[cueIndex] + deltaDeci;
                var finalEnd = scaledEnds[cueIndex].HasValue
                    ? (scaledEnds[cueIndex]!.Value + deltaDeci).ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

                transformedLines.Add($"[{finalStart.ToString(CultureInfo.InvariantCulture)}][{finalEnd}]{match.Groups["text"].Value}");
                cueIndex++;
            }
            else
            {
                transformedLines.Add(line);
            }
        }

        var activeTrimmed = activeText.TrimEnd('\r', '\n');
        var importedContent = string.Join(lineBreak, transformedLines);
        var merged = string.IsNullOrEmpty(activeTrimmed)
            ? importedContent
            : $"{activeTrimmed}{lineBreak}{importedContent}";

        return new SubtitleMergeResult(merged, importedParse.Cues.Count);
    }

    // ==========================================
    // TMPlayer
    // ==========================================

    private static SubtitleMergeParseResult ParseTmPlayerForMerge(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = new List<SubtitleMergeParsedCue>();
        var displayItems = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var match = TmPlayerPattern.Match(line);
            if (!match.Success || !TryReadTmPlayerSeconds(match, out var seconds))
            {
                continue;
            }

            var textContent = match.Groups["text"].Value.Trim();
            var formattedTime = SubtitleTimingFormatter.FormatTmPlayerSeconds(seconds, SubtitleTimingFormat.TmPlayerLong).TrimEnd(':');
            var display = $"{formattedTime}: {textContent}";
            cues.Add(new SubtitleMergeParsedCue(cues.Count, seconds, null, display));
            displayItems.Add(display);
        }

        return new SubtitleMergeParseResult(SubtitleLogicalFormat.TmPlayer, cues, displayItems);
    }

    private static SubtitleMergeResult MergeTmPlayer(
        string activeText,
        string importedText,
        int selectedCueIndex,
        long positionMilliseconds,
        double? scale)
    {
        var importedParse = ParseTmPlayerForMerge(importedText);
        if (importedParse.Cues.Count == 0 || selectedCueIndex < 0 || selectedCueIndex >= importedParse.Cues.Count)
        {
            return new SubtitleMergeResult(activeText, 0);
        }

        var positionSeconds = (long)Math.Round(positionMilliseconds / 1000d, MidpointRounding.AwayFromZero);

        var scaledStarts = new long[importedParse.Cues.Count];

        for (var index = 0; index < importedParse.Cues.Count; index++)
        {
            var cue = importedParse.Cues[index];
            if (scale is { } scaleFactor)
            {
                scaledStarts[index] = RoundLikeOriginal(cue.StartUnits * scaleFactor);
            }
            else
            {
                scaledStarts[index] = cue.StartUnits;
            }
        }

        var syncStart = scaledStarts[selectedCueIndex];
        var deltaSeconds = positionSeconds - syncStart;

        var lineBreak = activeText.Contains("\r\n", StringComparison.Ordinal) || importedText.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";

        var importedLines = importedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var transformedLines = new List<string>();
        var cueIndex = 0;

        foreach (var line in importedLines)
        {
            var match = TmPlayerPattern.Match(line);
            if (match.Success && TryReadTmPlayerSeconds(match, out _))
            {
                var finalSeconds = scaledStarts[cueIndex] + deltaSeconds;
                var isShort = match.Groups["hours"].Value.TrimStart('+', '-').Length == 1;
                var formatted = SubtitleTimingFormatter.FormatTmPlayerSeconds(
                    finalSeconds,
                    isShort ? SubtitleTimingFormat.TmPlayerShort : SubtitleTimingFormat.TmPlayerLong);
                var formattedTimeOnly = formatted.TrimEnd(':');

                transformedLines.Add($"{match.Groups["before"].Value}{formattedTimeOnly}" +
                    $"{match.Groups["separator"].Value}{match.Groups["text"].Value}");
                cueIndex++;
            }
            else
            {
                transformedLines.Add(line);
            }
        }

        var activeTrimmed = activeText.TrimEnd('\r', '\n');
        var importedContent = string.Join(lineBreak, transformedLines);
        var merged = string.IsNullOrEmpty(activeTrimmed)
            ? importedContent
            : $"{activeTrimmed}{lineBreak}{importedContent}";

        return new SubtitleMergeResult(merged, importedParse.Cues.Count);
    }

    private static bool TryReadTmPlayerSeconds(Match match, out long totalSeconds) =>
        SubtitleTimingFormatter.TryParseTmPlayerTime(
            match.Groups["hours"].Value,
            match.Groups["minutes"].Value,
            match.Groups["seconds"].Value,
            out totalSeconds);
}
