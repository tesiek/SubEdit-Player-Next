using SubEdit.Player.Core.Configuration;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static partial class RapidSubtitleMergeProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$", RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^(?<before>\s*)(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<arrow>\s*-->\s*)(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})(?<after>.*)$", RegexOptions.Compiled);

    public static RapidSubtitleMergeResult Merge(
        string text,
        RapidSubtitleMergeSettings options,
        double framesPerSecond,
        int firstLine,
        int lastLine)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return new RapidSubtitleMergeResult(text, 0);
        }

        var lineBreak = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);
        return SubtitleFormatConverter.IsSrtDocument(text)
            ? MergeSrt(lines, options, firstLine, lastLine, lineBreak)
            : MergeInline(lines, options, Math.Max(1d, framesPerSecond), firstLine, lastLine, lineBreak);
    }

    private static RapidSubtitleMergeResult MergeInline(
        List<string> lines,
        RapidSubtitleMergeSettings options,
        double framesPerSecond,
        int firstLine,
        int lastLine,
        string lineBreak)
    {
        var cues = lines.Select((line, index) => InlineCue.Parse(line, index, framesPerSecond)).ToList();
        var mergedCount = 0;
        for (var index = 0; index + 1 < cues.Count;)
        {
            var first = cues[index];
            var second = cues[index + 1];
            var third = index + 2 < cues.Count ? cues[index + 2] : null;
            if (!first.IsTimed || !second.IsTimed ||
                !IsInScope(first.LineIndex, first.LineIndex, firstLine, lastLine) ||
                !IsInScope(second.LineIndex, second.LineIndex, firstLine, lastLine) ||
                !CanMerge(first, second, third, options))
            {
                index++;
                continue;
            }

            first.Text = MergeText(first.Text, second.Text, options, '|');
            first.EndMilliseconds = EffectiveEnd(second, third);
            first.SetNativeEnd(framesPerSecond);
            cues.RemoveAt(index + 1);
            mergedCount++;
        }

        return new RapidSubtitleMergeResult(string.Join(lineBreak, cues.Select(cue => cue.Serialize())), mergedCount);
    }

    private static RapidSubtitleMergeResult MergeSrt(
        List<string> lines,
        RapidSubtitleMergeSettings options,
        int firstLine,
        int lastLine,
        string lineBreak)
    {
        var cues = ParseSrt(lines);
        var removedCues = new List<SrtCue>();
        var mergedCount = 0;
        for (var index = 0; index + 1 < cues.Count;)
        {
            var first = cues[index];
            var second = cues[index + 1];
            var third = index + 2 < cues.Count ? cues[index + 2] : null;
            if (!IsInScope(first.BlockStart, first.BlockEndExclusive - 1, firstLine, lastLine) ||
                !IsInScope(second.BlockStart, second.BlockEndExclusive - 1, firstLine, lastLine) ||
                !CanMerge(first, second, third, options))
            {
                index++;
                continue;
            }

            first.Text = MergeText(first.Text, second.Text, options, '\n');
            first.EndMilliseconds = EffectiveEnd(second, third);
            first.Changed = true;
            removedCues.Add(second);
            cues.RemoveAt(index + 1);
            mergedCount++;
        }

        var edits = new List<(int BlockStart, int BlockEndExclusive, List<string>? Replacement)>();
        edits.AddRange(removedCues.Select(cue => (cue.BlockStart, cue.BlockEndExclusive, (List<string>?)null)));
        foreach (var cue in cues.Where(cue => cue.Changed).OrderByDescending(cue => cue.BlockStart))
        {
            var replacement = new List<string>();
            if (cue.NumberLine is not null)
            {
                replacement.Add(cue.NumberLine);
            }
            replacement.Add(ReplaceSrtTiming(cue.TimingLine, cue.StartMilliseconds, cue.EndMilliseconds!.Value));
            replacement.AddRange(cue.Text.Split('\n'));
            edits.Add((cue.BlockStart, cue.BlockEndExclusive, replacement));
        }
        foreach (var edit in edits.OrderByDescending(edit => edit.BlockStart))
        {
            lines.RemoveRange(edit.BlockStart, edit.BlockEndExclusive - edit.BlockStart);
            if (edit.Replacement is not null)
            {
                lines.InsertRange(edit.BlockStart, edit.Replacement);
            }
        }
        return new RapidSubtitleMergeResult(string.Join(lineBreak, lines), mergedCount);
    }

    private static bool CanMerge(TimedCue first, TimedCue second, TimedCue? third, RapidSubtitleMergeSettings options)
    {
        var firstEnd = EffectiveEnd(first, second);
        var secondEnd = EffectiveEnd(second, third);
        var firstIsQuick = IsQuicklyDisplayed(first, firstEnd, options.MaximumFiftyCharacterDisplaySeconds);
        var secondIsQuick = IsQuicklyDisplayed(second, secondEnd, options.MaximumFiftyCharacterDisplaySeconds);
        if (!firstIsQuick && !secondIsQuick)
        {
            return false;
        }

        var firstToLastEnd = secondEnd - first.StartMilliseconds;
        var firstToLastStart = second.StartMilliseconds - first.StartMilliseconds;
        if (firstToLastEnd >= SecondsToMilliseconds(options.MaximumFirstToLastEndSeconds) &&
            firstToLastStart >= SecondsToMilliseconds(options.MaximumFirstToLastStartSeconds))
        {
            return false;
        }

        var mergedLength = first.Text.Length + 1 + second.Text.Length;
        return !options.LimitCharacters || mergedLength < options.MaximumCharacters;
    }

    private static bool IsQuicklyDisplayed(TimedCue cue, long endMilliseconds, double maximumSeconds)
    {
        var displayMilliseconds = Math.Max(0L, endMilliseconds - cue.StartMilliseconds);
        var normalizedSeconds = displayMilliseconds * 50d / Math.Max(1, cue.Text.Length) / 1000d;
        return normalizedSeconds < maximumSeconds;
    }

    private static long EffectiveEnd(TimedCue cue, TimedCue? following)
    {
        var end = cue.EndMilliseconds ?? (following?.StartMilliseconds ?? cue.StartMilliseconds);
        if (following is not null)
        {
            end = Math.Min(end, following.StartMilliseconds);
        }
        return Math.Max(cue.StartMilliseconds, end);
    }

    private static string MergeText(string first, string second, RapidSubtitleMergeSettings options, char separator)
    {
        var merged = first + separator + second;
        if (options.AddAsConsecutiveLines || CountRows(merged, separator) <= options.MaximumLines)
        {
            return merged;
        }
        return ReflowRows(merged, options.MaximumLines, separator);
    }

    private static int CountRows(string text, char separator) => text.Count(character => character == separator) + 1;

    private static string ReflowRows(string text, int maximumLines, char separator)
    {
        var words = text.Replace(separator, ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
        {
            return text.Replace(separator, ' ');
        }

        var rows = new List<string>(maximumLines);
        var totalCharacters = words.Sum(word => word.Length) + words.Length - 1;
        var wordIndex = 0;
        for (var row = 0; row < maximumLines && wordIndex < words.Length; row++)
        {
            var remainingRows = maximumLines - row;
            var remainingWords = words.Length - wordIndex;
            var targetLength = (int)Math.Ceiling(totalCharacters / (double)remainingRows);
            var current = words[wordIndex++];
            while (wordIndex < words.Length &&
                   remainingWords > remainingRows &&
                   current.Length + 1 + words[wordIndex].Length <= targetLength)
            {
                current += " " + words[wordIndex++];
                remainingWords--;
            }
            totalCharacters -= current.Length;
            if (wordIndex < words.Length)
            {
                totalCharacters--;
            }
            rows.Add(current);
        }
        if (wordIndex < words.Length)
        {
            rows[^1] += " " + string.Join(" ", words.Skip(wordIndex));
        }
        return string.Join(separator, rows);
    }

    private static bool IsInScope(int startLine, int endLine, int firstLine, int lastLine) =>
        startLine >= firstLine && endLine <= lastLine;

    private static long SecondsToMilliseconds(double seconds) =>
        SubtitleTimingFormatter.RoundAwayFromZero(seconds * 1000d);

    private static List<SrtCue> ParseSrt(IReadOnlyList<string> lines)
    {
        var cues = new List<SrtCue>();
        for (var index = 0; index < lines.Count;)
        {
            while (index < lines.Count && string.IsNullOrWhiteSpace(lines[index])) index++;
            if (index >= lines.Count) break;
            var blockStart = index;
            string? numberLine = null;
            if (int.TryParse(lines[index].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out _) && index + 1 < lines.Count)
            {
                numberLine = lines[index++];
            }
            var match = SrtTimingPattern.Match(lines[index]);
            if (!match.Success || !TryParseSrtTimestamp(match.Groups["start"].Value, out var start) || !TryParseSrtTimestamp(match.Groups["end"].Value, out var end))
            {
                index = blockStart + 1;
                continue;
            }
            var timingLine = lines[index++];
            var textStart = index;
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index])) index++;
            cues.Add(new SrtCue(blockStart, index, numberLine, timingLine, start, end, string.Join('\n', lines.Skip(textStart).Take(index - textStart))));
        }
        return cues;
    }

    private static bool TryParseSrtTimestamp(string value, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(value, out milliseconds);

    private static string ReplaceSrtTiming(string line, long startMilliseconds, long endMilliseconds)
    {
        var match = SrtTimingPattern.Match(line);
        return !match.Success ? line : match.Groups["before"].Value + FormatSrtTimestamp(startMilliseconds, match.Groups["start"].Value) +
            match.Groups["arrow"].Value + FormatSrtTimestamp(endMilliseconds, match.Groups["end"].Value) + match.Groups["after"].Value;
    }

    private static string FormatSrtTimestamp(long milliseconds, string original)
    {
        var separator = original.Contains('.') ? '.' : ',';
        return SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, separator);
    }

    private abstract class TimedCue
    {
        public abstract long StartMilliseconds { get; }
        public abstract long? EndMilliseconds { get; set; }
        public abstract string Text { get; set; }
    }

    private sealed class InlineCue : TimedCue
    {
        private InlineCue(int lineIndex, InlineFormat format, string prefix, string text, long startMilliseconds, long? endMilliseconds, long startNative, long? endNative)
        {
            LineIndex = lineIndex; Format = format; Prefix = prefix; Text = text; StartMilliseconds = startMilliseconds; EndMilliseconds = endMilliseconds; StartNative = startNative; EndNative = endNative;
        }
        public int LineIndex { get; }
        public InlineFormat Format { get; }
        public string Prefix { get; }
        public bool IsTimed => Format != InlineFormat.Plain;
        public override string Text { get; set; }
        public override long StartMilliseconds { get; }
        public override long? EndMilliseconds { get; set; }
        public long StartNative { get; }
        public long? EndNative { get; private set; }

        public static InlineCue Parse(string line, int lineIndex, double fps)
        {
            if (TryParse(MicroDvdPattern.Match(line), lineIndex, InlineFormat.MicroDvd, fps, out var cue) ||
                TryParse(Mpl2Pattern.Match(line), lineIndex, InlineFormat.Mpl2, 10d, out cue)) return cue;
            var tmPlayer = TmPlayerPattern.Match(line);
            if (tmPlayer.Success &&
                SubtitleTimingFormatter.TryParseTmPlayerTime(
                    tmPlayer.Groups["hours"].Value,
                    tmPlayer.Groups["minutes"].Value,
                    tmPlayer.Groups["seconds"].Value,
                    out var totalSeconds))
            {
                var text = tmPlayer.Groups["text"];
                return new InlineCue(lineIndex, InlineFormat.TmPlayer, line[..text.Index], text.Value, totalSeconds * 1000L, null, 0, null);
            }
            return new InlineCue(lineIndex, InlineFormat.Plain, string.Empty, line, 0, null, 0, null);
        }

        private static bool TryParse(Match match, int lineIndex, InlineFormat format, double unit, out InlineCue cue)
        {
            cue = null!;
            if (!match.Success || !long.TryParse(match.Groups["start"].Value, out var start) || !TryParseOptional(match.Groups["end"].Value, out var end)) return false;
            cue = new InlineCue(lineIndex, format, string.Empty, match.Groups["text"].Value, ToMilliseconds(start, unit), end is null ? null : ToMilliseconds(end.Value, unit), start, end);
            return true;
        }

        public void SetNativeEnd(double fps)
        {
            if (EndMilliseconds is null || Format == InlineFormat.TmPlayer) return;
            var unit = Format == InlineFormat.MicroDvd ? fps : 10d;
            EndNative = SubtitleTimingFormatter.RoundAwayFromZero(EndMilliseconds.Value * unit / 1000d);
        }

        public string Serialize() => Format switch
        {
            InlineFormat.MicroDvd => $"{{{StartNative}}}{{{EndNative?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}}}{Text}",
            InlineFormat.Mpl2 => $"[{StartNative}][{EndNative?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}]{Text}",
            _ => Prefix + Text
        };

        private static bool TryParseOptional(string value, out long? result)
        {
            if (value.Length == 0) { result = null; return true; }
            if (long.TryParse(value, out var parsed)) { result = parsed; return true; }
            result = null; return false;
        }
        private static long ToMilliseconds(long value, double unitsPerSecond) =>
            SubtitleTimingFormatter.RoundAwayFromZero(value * 1000d / unitsPerSecond);
    }

    private sealed class SrtCue(int blockStart, int blockEndExclusive, string? numberLine, string timingLine, long startMilliseconds, long endMilliseconds, string text) : TimedCue
    {
        public int BlockStart { get; } = blockStart;
        public int BlockEndExclusive { get; } = blockEndExclusive;
        public string? NumberLine { get; } = numberLine;
        public string TimingLine { get; } = timingLine;
        public override long StartMilliseconds { get; } = startMilliseconds;
        public override long? EndMilliseconds { get; set; } = endMilliseconds;
        public override string Text { get; set; } = text;
        public bool Changed { get; set; }
    }

    private enum InlineFormat { Plain, MicroDvd, Mpl2, TmPlayer }
}

internal readonly record struct RapidSubtitleMergeResult(string Text, int MergedCount);
