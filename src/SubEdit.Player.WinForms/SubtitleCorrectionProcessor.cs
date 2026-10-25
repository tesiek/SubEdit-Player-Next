using SubEdit.Player.Core.Configuration;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

internal static class SubtitleCorrectionProcessor
{
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex SrtTimingPattern = new(
        @"^\s*(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})",
        RegexOptions.Compiled);
    private static readonly Regex MicroDvdColorPattern = new(
        @"\{[cC]:\s*\$[0-9a-fA-F]{6}\s*\}",
        RegexOptions.Compiled);
    private static readonly Regex RepeatedSpacesPattern = new(
        @" {2,}",
        RegexOptions.Compiled);
    private static readonly Regex SpacesBetweenDigitsPattern = new(
        @"(?<=\d) +(?=\d)",
        RegexOptions.Compiled);

    public static string Correct(
        string text,
        SubtitleCorrectionSettings options,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine,
        bool treatUnrecognizedAsText)
    {
        if (string.IsNullOrEmpty(text) || lastLine < firstLine)
        {
            return text;
        }

        return SubtitleFormatConverter.IsSrtDocument(text)
            ? CorrectSrt(text, options, framesPerSecond, minimumGapFrames, firstLine, lastLine)
            : CorrectInline(text, options, framesPerSecond, minimumGapFrames, firstLine, lastLine, treatUnrecognizedAsText);
    }

    private static string CorrectInline(
        string text,
        SubtitleCorrectionSettings options,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine,
        bool treatUnrecognizedAsText)
    {
        var lineBreak = GetLineBreak(text);
        var lines = SplitLines(text);
        firstLine = Math.Clamp(firstLine, 0, lines.Length - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Length - 1);
        var safeFramesPerSecond = Math.Max(1d, framesPerSecond);
        var entries = lines
            .Select((line, index) => InlineEntry.Parse(line, index, safeFramesPerSecond))
            .ToList();

        foreach (var entry in entries.Where(entry =>
                     entry.LineIndex >= firstLine &&
                     entry.LineIndex <= lastLine &&
                     (entry.IsRecognized || treatUnrecognizedAsText || string.IsNullOrWhiteSpace(entry.Text))))
        {
            if (options.RemoveEmptyLines && string.IsNullOrWhiteSpace(entry.Text))
            {
                entry.Delete = true;
            }
        }

        if (options.RemoveRepeatedLines)
        {
            InlineEntry? previous = null;
            foreach (var entry in entries.Where(entry => IsTarget(entry, firstLine, lastLine, treatUnrecognizedAsText)))
            {
                if (entry.Delete)
                {
                    continue;
                }

                if (previous is not null && string.Equals(previous.Text, entry.Text, StringComparison.Ordinal))
                {
                    previous.ExtendEndTo(entry);
                    entry.Delete = true;
                    continue;
                }

                previous = entry;
            }
        }

        foreach (var entry in entries.Where(entry =>
                     !entry.Delete && IsTarget(entry, firstLine, lastLine, treatUnrecognizedAsText)))
        {
            entry.Text = CorrectText(entry.Text, '|', options);
        }

        if (options.LimitDisplayTime)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry.Delete || !entry.IsRecognized || entry.LineIndex < firstLine || entry.LineIndex > lastLine)
                {
                    continue;
                }

                var next = entries
                    .Skip(index + 1)
                    .FirstOrDefault(candidate => !candidate.Delete && candidate.IsRecognized);
                if (next is not null)
                {
                    entry.LimitEndBefore(next, safeFramesPerSecond, minimumGapFrames);
                }
            }
        }

        return string.Join(lineBreak, entries.Where(entry => !entry.Delete).Select(entry => entry.Serialize()));
    }

    private static string CorrectSrt(
        string text,
        SubtitleCorrectionSettings options,
        double framesPerSecond,
        int minimumGapFrames,
        int firstLine,
        int lastLine)
    {
        var lineBreak = GetLineBreak(text);
        var lines = SplitLines(text).ToList();
        firstLine = Math.Clamp(firstLine, 0, lines.Count - 1);
        lastLine = Math.Clamp(lastLine, firstLine, lines.Count - 1);
        var cues = ParseSrtCues(lines);
        var targets = cues
            .Where(cue => cue.BlockStart <= lastLine && cue.BlockEndExclusive > firstLine)
            .ToList();

        foreach (var cue in targets)
        {
            if (options.RemoveEmptyLines && string.IsNullOrWhiteSpace(cue.Text))
            {
                cue.Delete = true;
            }
        }

        if (options.RemoveRepeatedLines)
        {
            SrtCue? previous = null;
            foreach (var cue in targets)
            {
                if (cue.Delete)
                {
                    continue;
                }

                if (previous is not null && string.Equals(previous.Text, cue.Text, StringComparison.Ordinal))
                {
                    previous.EndMilliseconds = cue.EndMilliseconds;
                    previous.TimingChanged = true;
                    cue.Delete = true;
                    continue;
                }

                previous = cue;
            }
        }

        foreach (var cue in targets.Where(cue => !cue.Delete))
        {
            var corrected = CorrectText(cue.Text, '\n', options);
            cue.TextChanged = !string.Equals(corrected, cue.Text, StringComparison.Ordinal);
            cue.Text = corrected;
        }

        if (options.LimitDisplayTime)
        {
            for (var index = 0; index < cues.Count; index++)
            {
                var cue = cues[index];
                if (cue.Delete || !targets.Contains(cue))
                {
                    continue;
                }

                var next = cues.Skip(index + 1).FirstOrDefault(candidate => !candidate.Delete);
                if (next is null)
                {
                    continue;
                }

                var maximumEnd = Math.Max(
                    cue.StartMilliseconds,
                    next.StartMilliseconds - MinimumGapMilliseconds(framesPerSecond, minimumGapFrames));
                if (cue.EndMilliseconds > maximumEnd)
                {
                    cue.EndMilliseconds = maximumEnd;
                    cue.TimingChanged = true;
                }
            }
        }

        var anyDeleted = false;
        foreach (var cue in cues.OrderByDescending(cue => cue.BlockStart))
        {
            if (cue.Delete)
            {
                anyDeleted = true;
                var deleteEnd = cue.BlockEndExclusive;
                if (deleteEnd < lines.Count && string.IsNullOrWhiteSpace(lines[deleteEnd]))
                {
                    deleteEnd++;
                }

                lines.RemoveRange(cue.BlockStart, deleteEnd - cue.BlockStart);
                continue;
            }

            if (cue.TextChanged)
            {
                var replacementLines = cue.Text.Split('\n');
                lines.RemoveRange(cue.TextStart, cue.TextEndExclusive - cue.TextStart);
                lines.InsertRange(cue.TextStart, replacementLines);
            }

            if (cue.TimingChanged)
            {
                lines[cue.TimingLine] = ReplaceSrtEnd(lines[cue.TimingLine], cue.EndMilliseconds);
            }
        }

        if (anyDeleted)
        {
            RenumberSrt(lines);
        }

        return string.Join(lineBreak, lines);
    }

    private static bool IsTarget(
        InlineEntry entry,
        int firstLine,
        int lastLine,
        bool treatUnrecognizedAsText) =>
        entry.LineIndex >= firstLine &&
        entry.LineIndex <= lastLine &&
        (entry.IsRecognized || treatUnrecognizedAsText);

    private static string CorrectText(
        string text,
        char lineSeparator,
        SubtitleCorrectionSettings options)
    {
        if (options.FixPunctuation)
        {
            text = CorrectPunctuation(text, lineSeparator);
        }

        if (options.FixDialogueDashes)
        {
            text = CorrectDialogueDashes(text, lineSeparator, options.AddSpaceAfterDialogueDash);
        }

        if (options.RemoveSingleDialogueDash)
        {
            text = RemoveSingleDialogueDash(text, lineSeparator);
        }

        if (options.RemoveEllipsesFromSingleLines)
        {
            text = RemoveLeadingEllipsisFromSingleLine(text, lineSeparator);
        }

        if (options.RemoveDoubleSpaces)
        {
            text = RepeatedSpacesPattern.Replace(text, " ");
        }

        if (options.TrimLineSpaces)
        {
            text = JoinRows(SplitRows(text, lineSeparator).Select(row => row.Trim(' ')), lineSeparator);
        }

        if (options.RemoveSpacesBetweenDigits)
        {
            text = SpacesBetweenDigitsPattern.Replace(text, string.Empty);
        }

        if (options.RemoveMicroDvdColorInformation)
        {
            text = MicroDvdColorPattern.Replace(text, string.Empty);
        }

        return text;
    }

    private static string CorrectPunctuation(string text, char lineSeparator)
    {
        var result = new StringBuilder(text.Length + 8);
        var braceDepth = 0;
        var insideHtmlTag = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '{')
            {
                braceDepth++;
                result.Append(character);
                continue;
            }

            if (character == '}' && braceDepth > 0)
            {
                braceDepth--;
                result.Append(character);
                continue;
            }

            if (character == '<')
            {
                insideHtmlTag = true;
                result.Append(character);
                continue;
            }

            if (character == '>' && insideHtmlTag)
            {
                insideHtmlTag = false;
                result.Append(character);
                continue;
            }

            if (braceDepth > 0 || insideHtmlTag || !IsPunctuation(character))
            {
                result.Append(character);
                continue;
            }

            while (result.Length > 0 && result[^1] == ' ')
            {
                result.Length--;
            }

            result.Append(character);
            var nextIndex = index + 1;
            while (nextIndex < text.Length && text[nextIndex] == ' ')
            {
                nextIndex++;
            }

            var previousIndex = index - 1;
            while (previousIndex >= 0 && text[previousIndex] == ' ')
            {
                previousIndex--;
            }

            var previous = previousIndex >= 0 ? text[previousIndex] : '\0';
            var next = nextIndex < text.Length ? text[nextIndex] : '\0';
            var isNumericSeparator =
                character is '.' or ',' or ':' &&
                char.IsDigit(previous) &&
                char.IsDigit(next);
            var needsSpace =
                !isNumericSeparator &&
                next != '\0' &&
                next != lineSeparator &&
                next != '|' &&
                next != '/' &&
                next != '<' &&
                next != '}' &&
                !IsPunctuation(next) &&
                next is not '\'' and not '"' and not ')' and not ']';

            if (needsSpace)
            {
                result.Append(' ');
            }

            index = nextIndex - 1;
        }

        return result.ToString();
    }

    private static string CorrectDialogueDashes(string text, char lineSeparator, bool addSpace)
    {
        return JoinRows(
            SplitRows(text, lineSeparator).Select(row =>
            {
                var dashIndex = FindLeadingDialogueDash(row);
                if (dashIndex < 0)
                {
                    return row;
                }

                var contentStart = dashIndex + 1;
                while (contentStart < row.Length && row[contentStart] == ' ')
                {
                    contentStart++;
                }

                var prefix = row[..dashIndex].Replace(" ", string.Empty, StringComparison.Ordinal);
                return prefix + "-" + (addSpace && contentStart < row.Length ? " " : string.Empty) + row[contentStart..];
            }),
            lineSeparator);
    }

    private static string RemoveSingleDialogueDash(string text, char lineSeparator)
    {
        var rows = SplitRows(text, lineSeparator);
        if (rows.Length == 0)
        {
            return text;
        }

        var firstDash = FindLeadingDialogueDash(rows[0]);
        if (firstDash < 0)
        {
            return text;
        }

        if (rows.Skip(1).Any(row => FindLeadingDialogueDash(row) >= 0))
        {
            return text;
        }

        var contentStart = firstDash + 1;
        if (contentStart < rows[0].Length && rows[0][contentStart] == ' ')
        {
            contentStart++;
        }

        rows[0] = rows[0][..firstDash] + rows[0][contentStart..];
        return JoinRows(rows, lineSeparator);
    }

    private static string RemoveLeadingEllipsisFromSingleLine(string text, char lineSeparator)
    {
        if (text.IndexOf(lineSeparator) >= 0)
        {
            return text;
        }

        var index = 0;
        var foundDot = false;
        while (index < text.Length && text[index] is ' ' or '.')
        {
            foundDot |= text[index] == '.';
            index++;
        }

        return foundDot ? text[index..] : text;
    }

    private static int FindLeadingDialogueDash(string row)
    {
        var index = 0;
        while (index < row.Length)
        {
            if (row[index] == ' ')
            {
                index++;
                continue;
            }

            if (row[index] == '/')
            {
                index++;
                continue;
            }

            if (row[index] == '{')
            {
                var end = row.IndexOf('}', index + 1);
                if (end >= 0)
                {
                    index = end + 1;
                    continue;
                }
            }

            if (row[index] == '<')
            {
                var end = row.IndexOf('>', index + 1);
                if (end >= 0)
                {
                    index = end + 1;
                    continue;
                }
            }

            return row[index] == '-' ? index : -1;
        }

        return -1;
    }

    private static bool IsPunctuation(char character) =>
        character is '.' or ',' or '!' or '?' or ':' or ';' or '…';

    private static string[] SplitRows(string text, char separator) => text.Split(separator);

    private static string JoinRows(IEnumerable<string> rows, char separator) =>
        string.Join(separator.ToString(), rows);

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
            if (int.TryParse(lines[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
                index + 1 < lines.Count)
            {
                timingLine++;
            }

            if (!TryParseSrtTiming(lines[timingLine], out var start, out var end))
            {
                index++;
                continue;
            }

            var textStart = timingLine + 1;
            index = textStart;
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }

            cues.Add(new SrtCue
            {
                BlockStart = blockStart,
                BlockEndExclusive = index,
                TimingLine = timingLine,
                TextStart = textStart,
                TextEndExclusive = index,
                StartMilliseconds = start,
                EndMilliseconds = end,
                Text = string.Join("\n", lines.Skip(textStart).Take(index - textStart))
            });
        }

        return cues;
    }

    private static void RenumberSrt(List<string> lines)
    {
        var cueNumber = 1;
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
            if (int.TryParse(lines[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
                index + 1 < lines.Count)
            {
                timingLine++;
                hasIndex = true;
            }

            if (!TryParseSrtTiming(lines[timingLine], out _, out _))
            {
                index++;
                continue;
            }

            var newNumber = cueNumber.ToString(CultureInfo.InvariantCulture);
            if (hasIndex)
            {
                lines[blockStart] = newNumber;
            }
            else
            {
                lines.Insert(blockStart, newNumber);
                timingLine++;
            }

            cueNumber++;

            index = timingLine + 1;
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
            {
                index++;
            }
        }
    }

    private static bool TryParseSrtTiming(string line, out long start, out long end)
    {
        start = 0;
        end = 0;
        var match = SrtTimingPattern.Match(line);
        return match.Success &&
            TryParseSrtTimestamp(match.Groups["start"].Value, out start) &&
            TryParseSrtTimestamp(match.Groups["end"].Value, out end);
    }

    private static bool TryParseSrtTimestamp(string value, out long milliseconds) =>
        SubtitleTimingFormatter.TryParseSrtTimestamp(value, out milliseconds);

    private static string ReplaceSrtEnd(string timingLine, long endMilliseconds)
    {
        var match = SrtTimingPattern.Match(timingLine);
        if (!match.Success)
        {
            return timingLine;
        }

        var endGroup = match.Groups["end"];
        var separator = endGroup.Value.Contains('.') ? '.' : ',';
        return timingLine[..endGroup.Index] +
            FormatSrtTimestamp(endMilliseconds, separator) +
            timingLine[(endGroup.Index + endGroup.Length)..];
    }

    private static string FormatSrtTimestamp(long milliseconds, char separator = ',') =>
        SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, separator);

    private static long MinimumGapMilliseconds(double framesPerSecond, int minimumGapFrames) =>
        Math.Max(
            1,
            (long)Math.Round(
                Math.Clamp(minimumGapFrames, 1, 20) * 1000d / Math.Max(1d, framesPerSecond),
                MidpointRounding.AwayFromZero));

    private static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string GetLineBreak(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private sealed class InlineEntry
    {
        public required int LineIndex { get; init; }
        public required string Prefix { get; init; }
        public required string Text { get; set; }
        public required InlineFormat Format { get; init; }
        public required long StartMilliseconds { get; init; }
        public required long? EndMilliseconds { get; set; }
        public required long StartNative { get; init; }
        public required long? EndNative { get; set; }
        public bool Delete { get; set; }

        public bool IsRecognized => Format != InlineFormat.PlainText;

        public static InlineEntry Parse(string line, int lineIndex, double framesPerSecond)
        {
            var microDvd = MicroDvdPattern.Match(line);
            if (microDvd.Success &&
                long.TryParse(microDvd.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startFrame) &&
                TryParseOptionalLong(microDvd.Groups["end"].Value, out var endFrame))
            {
                return new InlineEntry
                {
                    LineIndex = lineIndex,
                    Prefix = string.Empty,
                    Text = microDvd.Groups["text"].Value,
                    Format = InlineFormat.MicroDvd,
                    StartMilliseconds = ToMilliseconds(startFrame, framesPerSecond),
                    EndMilliseconds = endFrame is null ? null : ToMilliseconds(endFrame.Value, framesPerSecond),
                    StartNative = startFrame,
                    EndNative = endFrame
                };
            }

            var mpl2 = Mpl2Pattern.Match(line);
            if (mpl2.Success &&
                long.TryParse(mpl2.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startTenth) &&
                TryParseOptionalLong(mpl2.Groups["end"].Value, out var endTenth))
            {
                return new InlineEntry
                {
                    LineIndex = lineIndex,
                    Prefix = string.Empty,
                    Text = mpl2.Groups["text"].Value,
                    Format = InlineFormat.Mpl2,
                    StartMilliseconds = startTenth * 100,
                    EndMilliseconds = endTenth is null ? null : endTenth.Value * 100,
                    StartNative = startTenth,
                    EndNative = endTenth
                };
            }

            var tmPlayer = TmPlayerPattern.Match(line);
            if (tmPlayer.Success &&
                SubtitleTimingFormatter.TryParseTmPlayerTime(
                    tmPlayer.Groups["hours"].Value,
                    tmPlayer.Groups["minutes"].Value,
                    tmPlayer.Groups["seconds"].Value,
                    out var totalSeconds))
            {
                var textGroup = tmPlayer.Groups["text"];
                return new InlineEntry
                {
                    LineIndex = lineIndex,
                    Prefix = line[..textGroup.Index],
                    Text = textGroup.Value,
                    Format = InlineFormat.TmPlayer,
                    StartMilliseconds = totalSeconds * 1000L,
                    EndMilliseconds = null,
                    StartNative = 0,
                    EndNative = null
                };
            }

            return new InlineEntry
            {
                LineIndex = lineIndex,
                Prefix = string.Empty,
                Text = line,
                Format = InlineFormat.PlainText,
                StartMilliseconds = 0,
                EndMilliseconds = null,
                StartNative = 0,
                EndNative = null
            };
        }

        public void ExtendEndTo(InlineEntry later)
        {
            if (later.EndMilliseconds is null)
            {
                return;
            }

            EndMilliseconds = later.EndMilliseconds;
            EndNative = Format == later.Format ? later.EndNative : EndNative;
        }

        public void LimitEndBefore(InlineEntry next, double framesPerSecond, int minimumGapFrames)
        {
            if (EndMilliseconds is not { } currentEnd)
            {
                return;
            }

            if (Format == InlineFormat.MicroDvd && next.Format == InlineFormat.MicroDvd)
            {
                var maximumNativeEnd = Math.Max(StartNative, next.StartNative - Math.Clamp(minimumGapFrames, 1, 20));
                if (EndNative is not { } currentNativeEnd || currentNativeEnd <= maximumNativeEnd)
                {
                    return;
                }

                EndNative = maximumNativeEnd;
                EndMilliseconds = Format == InlineFormat.MicroDvd
                    ? ToMilliseconds(maximumNativeEnd, framesPerSecond)
                    : maximumNativeEnd * 100;
                return;
            }

            var maximumEnd = Math.Max(
                StartMilliseconds,
                next.StartMilliseconds - MinimumGapMilliseconds(framesPerSecond, minimumGapFrames));
            if (currentEnd <= maximumEnd)
            {
                return;
            }

            EndMilliseconds = maximumEnd;
            EndNative = Format switch
            {
                InlineFormat.MicroDvd => Math.Max(
                    StartNative,
                    (long)Math.Round(
                        maximumEnd * framesPerSecond / 1000d,
                        MidpointRounding.AwayFromZero)),
                InlineFormat.Mpl2 => Math.Max(
                    StartNative,
                    (long)Math.Round(maximumEnd / 100d, MidpointRounding.AwayFromZero)),
                _ => EndNative
            };
        }

        public string Serialize() => Format switch
        {
            InlineFormat.MicroDvd => $"{{{StartNative}}}{{{FormatOptional(EndNative)}}}{Text}",
            InlineFormat.Mpl2 => $"[{StartNative}][{FormatOptional(EndNative)}]{Text}",
            _ => Prefix + Text
        };

        private static bool TryParseOptionalLong(string value, out long? result)
        {
            if (value.Length == 0)
            {
                result = null;
                return true;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = null;
            return false;
        }

        private static long ToMilliseconds(long frame, double framesPerSecond) =>
            (long)Math.Round(frame * 1000d / framesPerSecond, MidpointRounding.AwayFromZero);

        private static string FormatOptional(long? value) =>
            value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private sealed class SrtCue
    {
        public int BlockStart { get; init; }
        public int BlockEndExclusive { get; init; }
        public int TimingLine { get; init; }
        public int TextStart { get; init; }
        public int TextEndExclusive { get; init; }
        public long StartMilliseconds { get; init; }
        public long EndMilliseconds { get; set; }
        public required string Text { get; set; }
        public bool Delete { get; set; }
        public bool TextChanged { get; set; }
        public bool TimingChanged { get; set; }
    }

    private enum InlineFormat
    {
        PlainText,
        MicroDvd,
        Mpl2,
        TmPlayer
    }
}
