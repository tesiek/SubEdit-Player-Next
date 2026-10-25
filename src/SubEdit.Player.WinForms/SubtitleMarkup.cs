using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SubEdit.Player.WinForms;

[Flags]
internal enum SubtitleTextStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikeout = 8
}

internal sealed record SubtitleTextRun(
    string Text,
    SubtitleTextStyle Style,
    Color? Color,
    string? FontName = null,
    float? FontSize = null);

internal sealed record SubtitleLine(IReadOnlyList<SubtitleTextRun> Runs);

internal sealed record SubtitleCue(
    long StartMilliseconds,
    long EndMilliseconds,
    IReadOnlyList<SubtitleLine> Lines,
    Point? Position = null,
    int SourceLineNumber = 0);

internal sealed record SubtitleRenderOptions(
    bool InterpretMicroDvdControlCodes = true,
    bool ItalicLinesStartingWithSlash = true,
    bool ItalicLinesStartingWithAsterisk = true,
    bool ItalicLinesStartingWithHash = true)
{
    public static SubtitleRenderOptions Default { get; } = new();
}

internal sealed class SubtitleDocument
{
    private static readonly Regex SrtTimingPattern = new(
        @"^\s*(?<start>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(?<end>[+-]?\d{1,}:\d{2}:\d{2}[,.]\d{1,3})",
        RegexOptions.Compiled);
    private static readonly Regex Mpl2Pattern = new(
        @"^\[(?<start>-?\d+)]\[(?<end>-?\d*)](?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex MicroDvdPattern = new(
        @"^\{(?<start>-?\d+)}\{(?<end>-?\d*)}(?<text>.*)$",
        RegexOptions.Compiled);
    private static readonly Regex TmPlayerPattern = new(
        @"^\s*(?<hours>[+-]?\d{1,}):(?<minutes>[+-]?\d{1,2}):(?<seconds>[+-]?\d{1,2})(?:,\d+)?(?<separator>[:=])(?<text>.*)$",
        RegexOptions.Compiled);

    public static readonly SubtitleDocument Empty = new([]);

    private readonly IReadOnlyList<SubtitleCue> _cues;

    public IReadOnlyList<SubtitleCue> Cues => _cues;

    private SubtitleDocument(IReadOnlyList<SubtitleCue> cues)
    {
        _cues = cues;
    }

    public static SubtitleDocument Parse(
        string text,
        double framesPerSecond,
        double defaultFiftyCharacterDisplaySeconds = 3d,
        double additionalDefaultDisplaySeconds = 2d,
        SubtitleRenderOptions? renderOptions = null,
        int minimumGapFrames = 2)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Empty;
        }

        var options = renderOptions ?? SubtitleRenderOptions.Default;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var defaults = SubtitleMarkup.ParseDocumentDefaults(lines, options);
        var srt = ParseSrt(lines, defaults, options);
        if (srt.Count > 0)
        {
            return new SubtitleDocument(ApplyMinimumGap(srt, framesPerSecond, minimumGapFrames));
        }

        var cues = ParseMixedTimingFormats(
            lines,
            defaults,
            framesPerSecond,
            defaultFiftyCharacterDisplaySeconds,
            additionalDefaultDisplaySeconds,
            options);
        return cues.Count > 0
            ? new SubtitleDocument(ApplyMinimumGap(cues, framesPerSecond, minimumGapFrames))
            : Empty;
    }

    public static bool TryGetCueStartMilliseconds(string line, double framesPerSecond, out long startMilliseconds)
    {
        if (TryGetCueTiming(line, framesPerSecond, out startMilliseconds, out _))
        {
            return true;
        }

        return TryGetTmPlayerCue(line, out startMilliseconds, out _);
    }

    public static bool TryGetCueTiming(string line, double framesPerSecond, out long startMilliseconds, out long endMilliseconds) =>
        TryGetCueTiming(line, framesPerSecond, out startMilliseconds, out endMilliseconds, out _);

    public static bool TryGetCueTiming(
        string line,
        double framesPerSecond,
        out long startMilliseconds,
        out long endMilliseconds,
        out bool hasEnd)
    {
        startMilliseconds = 0;
        endMilliseconds = 0;
        hasEnd = false;
        if (TryParseSrtTiming(line, out startMilliseconds, out endMilliseconds))
        {
            hasEnd = true;
            return true;
        }

        if (TryGetInlineCue(line, framesPerSecond, out startMilliseconds, out endMilliseconds, out hasEnd, out _))
        {
            return true;
        }

        return false;
    }

    public static bool TryGetInlineCue(
        string line,
        double framesPerSecond,
        out long startMilliseconds,
        out long endMilliseconds,
        out string text) =>
        TryGetInlineCue(line, framesPerSecond, out startMilliseconds, out endMilliseconds, out _, out text);

    public static bool TryGetInlineCue(
        string line,
        double framesPerSecond,
        out long startMilliseconds,
        out long endMilliseconds,
        out bool hasEnd,
        out string text)
    {
        startMilliseconds = 0;
        endMilliseconds = 0;
        hasEnd = false;
        text = string.Empty;

        var mpl2 = Mpl2Pattern.Match(line);
        if (mpl2.Success &&
            long.TryParse(mpl2.Groups["start"].Value, out var mpl2Start) &&
            TryParseOptionalLong(mpl2.Groups["end"].Value, out var mpl2End, out hasEnd))
        {
            startMilliseconds = mpl2Start * 100;
            endMilliseconds = mpl2End * 100;
            text = mpl2.Groups["text"].Value;
            return true;
        }

        var microDvd = MicroDvdPattern.Match(line);
        if (microDvd.Success &&
            long.TryParse(microDvd.Groups["start"].Value, out var startFrame) &&
            TryParseOptionalLong(microDvd.Groups["end"].Value, out var endFrame, out hasEnd))
        {
            var millisecondsPerFrame = 1000d / Math.Max(1d, framesPerSecond);
            startMilliseconds = (long)Math.Round(startFrame * millisecondsPerFrame, MidpointRounding.AwayFromZero);
            endMilliseconds = (long)Math.Round(endFrame * millisecondsPerFrame, MidpointRounding.AwayFromZero);
            text = microDvd.Groups["text"].Value;
            return true;
        }

        return false;
    }

    private static bool TryParseOptionalLong(string value, out long result, out bool hasValue)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = 0;
            hasValue = false;
            return true;
        }

        hasValue = true;
        return long.TryParse(value, out result);
    }

    public static bool TryGetTmPlayerCue(string line, out long startMilliseconds, out string text)
    {
        startMilliseconds = 0;
        text = string.Empty;
        var match = TmPlayerPattern.Match(line);
        if (!match.Success ||
            !SubtitleTimingFormatter.TryParseTmPlayerTime(
                match.Groups["hours"].Value,
                match.Groups["minutes"].Value,
                match.Groups["seconds"].Value,
                out var totalSeconds))
        {
            return false;
        }

        startMilliseconds = totalSeconds * 1000L;
        text = match.Groups["text"].Value;
        return true;
    }

    public IEnumerable<SubtitleCue> GetUnsortedCuesReadyAt(long positionMilliseconds)
    {
        var previousDisplayEnd = long.MinValue;
        for (var index = 0; index < _cues.Count; index++)
        {
            var cue = _cues[index];
            var displayStart = Math.Max(cue.StartMilliseconds, previousDisplayEnd);
            var displayEnd = Math.Max(displayStart, cue.EndMilliseconds);
            // The playback loop reads one subtitle ahead, so it detects an out-of-order cue
            // when the cue before its predecessor finishes.
            var warningAt = index > 1 ? _cues[index - 2].EndMilliseconds : displayStart;
            if (index > 0 &&
                cue.StartMilliseconds != 0 &&
                cue.StartMilliseconds < _cues[index - 1].StartMilliseconds &&
                positionMilliseconds >= warningAt)
            {
                yield return cue;
            }

            previousDisplayEnd = displayEnd;
        }
    }

    private static List<SubtitleCue> ParseSrt(
        IReadOnlyList<string> lines,
        SubtitleTextDefaults defaults,
        SubtitleRenderOptions options)
    {
        var cues = new List<SubtitleCue>();
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
            if (int.TryParse(lines[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                timingIndex++;
            }

            if (timingIndex >= lines.Count || !TryParseSrtTiming(lines[timingIndex], out var start, out var end))
            {
                index++;
                continue;
            }

            index = timingIndex + 1;
            var textLines = new List<string>();
            while (index < lines.Count && !string.IsNullOrWhiteSpace(lines[index]))
            {
                if (!SubtitleMarkup.IsDocumentDefaultLine(lines[index]))
                {
                    textLines.Add(lines[index]);
                }
                index++;
            }

            if (textLines.Count > 0)
            {
                var cueText = SubtitleMarkup.ParseCue(string.Join("\n", textLines), defaults, options);
                cues.Add(new SubtitleCue(
                    start,
                    end,
                    cueText.Lines,
                    cueText.Position,
                    timingIndex + 1));
            }
        }

        return cues;
    }

    private static List<SubtitleCue> ParseMixedTimingFormats(
        IReadOnlyList<string> lines,
        SubtitleTextDefaults defaults,
        double framesPerSecond,
        double defaultFiftyCharacterDisplaySeconds,
        double additionalDefaultDisplaySeconds,
        SubtitleRenderOptions options)
    {
        var cues = new List<SubtitleCue>();
        for (var index = 0; index < lines.Count;)
        {
            if (TryGetInlineCue(lines[index], framesPerSecond, out var start, out var end, out var hasEnd, out var inlineText))
            {
                var inlineCueText = SubtitleMarkup.ParseCue(inlineText, defaults, options);
                var resolvedEnd = hasEnd && end > start
                    ? end
                    : start + GetDefaultDisplayDurationMilliseconds(
                        inlineCueText.Lines,
                        defaultFiftyCharacterDisplaySeconds,
                        additionalDefaultDisplaySeconds);
                cues.Add(new SubtitleCue(
                    start,
                    resolvedEnd,
                    inlineCueText.Lines,
                    inlineCueText.Position,
                    index + 1));
                index++;
                continue;
            }

            if (!TryGetTmPlayerCue(lines[index], out var startMilliseconds, out var text))
            {
                index++;
                continue;
            }

            var sourceLineNumber = index + 1;
            var textLines = new List<string> { text };
            index++;
            while (index < lines.Count &&
                   TryGetTmPlayerCue(lines[index], out var sameStartMilliseconds, out var sameStartText) &&
                   sameStartMilliseconds == startMilliseconds)
            {
                textLines.Add(sameStartText);
                index++;
            }

            var tmPlayerCueText = SubtitleMarkup.ParseCue(string.Join("\n", textLines), defaults, options);
            var endMilliseconds = FindNextCueStart(lines, index, startMilliseconds, framesPerSecond) ??
                startMilliseconds + GetDefaultDisplayDurationMilliseconds(
                    tmPlayerCueText.Lines,
                    defaultFiftyCharacterDisplaySeconds,
                    additionalDefaultDisplaySeconds);
            cues.Add(new SubtitleCue(
                startMilliseconds,
                endMilliseconds,
                tmPlayerCueText.Lines,
                tmPlayerCueText.Position,
                sourceLineNumber));
        }

        return cues;
    }

    private static IReadOnlyList<SubtitleCue> ApplyMinimumGap(
        IReadOnlyList<SubtitleCue> cues,
        double framesPerSecond,
        int minimumGapFrames)
    {
        var gapMilliseconds = (long)Math.Ceiling(
            Math.Clamp(minimumGapFrames, 1, 20) * 1000d / Math.Max(1d, framesPerSecond));
        var adjusted = new SubtitleCue[cues.Count];
        for (var index = 0; index < cues.Count; index++)
        {
            var cue = cues[index];
            if (index + 1 < cues.Count && cues[index + 1].StartMilliseconds > cue.StartMilliseconds)
            {
                var latestEnd = cues[index + 1].StartMilliseconds - gapMilliseconds;
                cue = cue with { EndMilliseconds = Math.Max(cue.StartMilliseconds, Math.Min(cue.EndMilliseconds, latestEnd)) };
            }

            adjusted[index] = cue;
        }

        return adjusted;
    }

    private static long GetDefaultDisplayDurationMilliseconds(
        IReadOnlyList<SubtitleLine> lines,
        double fiftyCharacterSeconds,
        double additionalSeconds)
    {
        var characterCount = lines.Sum(line => line.Runs.Sum(run => run.Text.Length));
        var seconds = Math.Max(0d, additionalSeconds) +
            (Math.Max(0, characterCount) * Math.Max(0d, fiftyCharacterSeconds) / 50d);
        return Math.Max(0L, (long)Math.Round(seconds * 1000d, MidpointRounding.AwayFromZero));
    }

    private static long? FindNextCueStart(
        IReadOnlyList<string> lines,
        int firstLine,
        long currentStartMilliseconds,
        double framesPerSecond)
    {
        for (var index = firstLine; index < lines.Count; index++)
        {
            if (TryGetInlineCue(lines[index], framesPerSecond, out var start, out _, out _) &&
                start > currentStartMilliseconds)
            {
                return start;
            }

            if (TryGetTmPlayerCue(lines[index], out start, out _) && start > currentStartMilliseconds)
            {
                return start;
            }
        }

        return null;
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
}

internal readonly record struct SubtitleEditorSpan(int Start, int Length, SubtitleTextStyle Style, bool IsMarkup);

internal readonly record struct SubtitleTextDefaults(
    SubtitleTextStyle Style = SubtitleTextStyle.None,
    Color? Color = null,
    string? FontName = null,
    float? FontSize = null);

internal readonly record struct ParsedSubtitleCue(IReadOnlyList<SubtitleLine> Lines, Point? Position);

internal static class SubtitleMarkup
{
    public static IReadOnlyList<SubtitleLine> ParseLines(string text)
        => ParseCue(text, default, SubtitleRenderOptions.Default).Lines;

    internal static ParsedSubtitleCue ParseCue(
        string text,
        SubtitleTextDefaults defaults,
        SubtitleRenderOptions? renderOptions = null)
    {
        var options = renderOptions ?? SubtitleRenderOptions.Default;
        var result = new List<SubtitleLine>();
        var wholeCueDefaults = defaults;
        Point? position = null;
        for (var index = 0; index < text.Length;)
        {
            if (options.InterpretMicroDvdControlCodes && TryReadMicroDvdCode(text, index, out var code))
            {
                if (code.Kind == MicroDvdCodeKind.Position)
                {
                    position = code.Position;
                }
                else if (code.IsGlobal)
                {
                    wholeCueDefaults = ApplyCode(wholeCueDefaults, code);
                }

                index += code.Length;
                continue;
            }

            index++;
        }

        foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('|', '\n').Split('\n'))
        {
            result.Add(new SubtitleLine(ParseRuns(rawLine, wholeCueDefaults, options)));
        }

        return new ParsedSubtitleCue(result, position);
    }

    internal static SubtitleTextDefaults ParseDocumentDefaults(
        IReadOnlyList<string> lines,
        SubtitleRenderOptions? renderOptions = null)
    {
        var options = renderOptions ?? SubtitleRenderOptions.Default;
        if (!options.InterpretMicroDvdControlCodes)
        {
            return default;
        }

        var defaults = default(SubtitleTextDefaults);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!IsDocumentDefaultLine(line))
            {
                continue;
            }

            for (var index = "{DEFAULT}".Length; index < line.Length;)
            {
                if (TryReadMicroDvdCode(line, index, out var code))
                {
                    defaults = ApplyCode(defaults, code);
                    index += code.Length;
                    continue;
                }

                index++;
            }
        }

        return defaults;
    }

    internal static bool IsDocumentDefaultLine(string line) =>
        line.TrimStart().StartsWith("{DEFAULT}", StringComparison.OrdinalIgnoreCase);

    internal static int GetLeadingMicroDvdCodesLength(string text)
    {
        var index = 0;
        while (index < text.Length && TryReadMicroDvdCode(text, index, out var code))
        {
            index += code.Length;
        }

        return index;
    }

    public static IReadOnlyList<SubtitleEditorSpan> GetEditorSpans(string text)
    {
        var spans = new List<SubtitleEditorSpan>();
        var style = SubtitleTextStyle.None;
        var legacyItalic = false;
        for (var index = 0; index < text.Length;)
        {
            if ((index == 0 || text[index - 1] == '\n') && IsLegacyItalicMarker(text[index], SubtitleRenderOptions.Default))
            {
                legacyItalic = true;
                AddSpan(spans, index, 1, SubtitleTextStyle.None, isMarkup: true);
                index++;
                continue;
            }

            if (TryReadOtherMarkupAction(text, index, out var action))
            {
                AddSpan(spans, index, action.Length, SubtitleTextStyle.None, isMarkup: true);
                style = (style | action.Enable) & ~action.Disable;
                index += action.Length;
                continue;
            }

            var effectiveStyle = style | (legacyItalic ? SubtitleTextStyle.Italic : SubtitleTextStyle.None);
            AddSpan(spans, index, 1, effectiveStyle, isMarkup: false);
            legacyItalic = text[index] == '\n' ? false : legacyItalic;
            index++;
        }

        return spans;
    }

    private static IReadOnlyList<SubtitleTextRun> ParseRuns(
        string source,
        SubtitleTextDefaults defaults,
        SubtitleRenderOptions options)
    {
        var runs = new List<SubtitleTextRun>();
        var index = 0;
        while (options.InterpretMicroDvdControlCodes &&
               index < source.Length &&
               TryReadMicroDvdCode(source, index, out var leadingCode))
        {
            if (leadingCode.Kind != MicroDvdCodeKind.Position)
            {
                defaults = ApplyCode(defaults, leadingCode);
            }

            index += leadingCode.Length;
        }

        var runDefaults = defaults;
        if (index < source.Length && IsLegacyItalicMarker(source[index], options))
        {
            runDefaults = runDefaults with { Style = runDefaults.Style | SubtitleTextStyle.Italic };
            index++;
        }

        var end = source.Length;
        if (end > index && IsLegacyItalicMarker(source[end - 1], options))
        {
            runDefaults = runDefaults with { Style = runDefaults.Style | SubtitleTextStyle.Italic };
            end--;
        }

        var text = new StringBuilder();
        void Flush()
        {
            if (text.Length > 0)
            {
                runs.Add(new SubtitleTextRun(text.ToString(), runDefaults.Style, runDefaults.Color, runDefaults.FontName, runDefaults.FontSize));
                text.Clear();
            }
        }

        while (index < end)
        {
            if (options.InterpretMicroDvdControlCodes &&
                TryReadMicroDvdCode(source, index, out var microDvdCode) &&
                index + microDvdCode.Length <= end)
            {
                Flush();
                if (microDvdCode.Kind != MicroDvdCodeKind.Position && !microDvdCode.IsGlobal)
                {
                    defaults = ApplyCode(defaults, microDvdCode);
                    runDefaults = defaults;
                }

                index += microDvdCode.Length;
                if (index < end && IsLegacyItalicMarker(source[index], options))
                {
                    runDefaults = runDefaults with { Style = runDefaults.Style | SubtitleTextStyle.Italic };
                    index++;
                }

                continue;
            }

            if (TryReadOtherMarkupAction(source, index, out var action) && index + action.Length <= end)
            {
                Flush();
                var style = (defaults.Style | action.Enable) & ~action.Disable;
                defaults = defaults with
                {
                    Style = style,
                    Color = action.ResetColor ? null : action.Color ?? defaults.Color
                };
                runDefaults = defaults;

                index += action.Length;
                continue;
            }

            text.Append(source[index]);
            index++;
        }

        Flush();
        return runs;
    }

    private static SubtitleTextDefaults ApplyCode(SubtitleTextDefaults defaults, MicroDvdCode code) => code.Kind switch
    {
        MicroDvdCodeKind.Style => defaults with { Style = code.Style },
        MicroDvdCodeKind.FontName => defaults with { FontName = code.FontName },
        MicroDvdCodeKind.FontSize => defaults with { FontSize = code.FontSize },
        MicroDvdCodeKind.Color => defaults with { Color = code.Color },
        _ => defaults
    };

    private static bool TryReadOtherMarkupAction(string text, int index, out MarkupAction action)
    {
        action = default;
        if (text[index] == '<')
        {
            var end = text.IndexOf('>', index + 1);
            if (end < 0 || end - index > 120)
            {
                return false;
            }

            var content = text[(index + 1)..end].Trim();
            var closing = content.StartsWith('/');
            var name = (closing ? content[1..] : content).Trim();
            if (name.Equals("i", StringComparison.OrdinalIgnoreCase))
            {
                action = new MarkupAction(end - index + 1, closing ? SubtitleTextStyle.None : SubtitleTextStyle.Italic, closing ? SubtitleTextStyle.Italic : SubtitleTextStyle.None);
                return true;
            }

            if (name.Equals("b", StringComparison.OrdinalIgnoreCase))
            {
                action = new MarkupAction(end - index + 1, closing ? SubtitleTextStyle.None : SubtitleTextStyle.Bold, closing ? SubtitleTextStyle.Bold : SubtitleTextStyle.None);
                return true;
            }

            if (name.Equals("u", StringComparison.OrdinalIgnoreCase))
            {
                action = new MarkupAction(end - index + 1, closing ? SubtitleTextStyle.None : SubtitleTextStyle.Underline, closing ? SubtitleTextStyle.Underline : SubtitleTextStyle.None);
                return true;
            }

            if (name.Equals("s", StringComparison.OrdinalIgnoreCase))
            {
                action = new MarkupAction(end - index + 1, closing ? SubtitleTextStyle.None : SubtitleTextStyle.Strikeout, closing ? SubtitleTextStyle.Strikeout : SubtitleTextStyle.None);
                return true;
            }

            if (name.StartsWith("font", StringComparison.OrdinalIgnoreCase) || (closing && name.Equals("font", StringComparison.OrdinalIgnoreCase)))
            {
                action = new MarkupAction(end - index + 1, SubtitleTextStyle.None, SubtitleTextStyle.None, TryReadHtmlColor(name), closing);
                return true;
            }
        }

        if (text[index] == '{')
        {
            var end = text.IndexOf('}', index + 1);
            if (end < 0 || end - index > 80)
            {
                return false;
            }

            var content = text[(index + 1)..end];
            var enable = SubtitleTextStyle.None;
            var disable = SubtitleTextStyle.None;
            foreach (Match match in Regex.Matches(content, @"\\(?<style>[ibu])(?<value>[01])", RegexOptions.IgnoreCase))
            {
                var flag = match.Groups["style"].Value.ToLowerInvariant() switch
                {
                    "i" => SubtitleTextStyle.Italic,
                    "b" => SubtitleTextStyle.Bold,
                    _ => SubtitleTextStyle.Underline
                };
                if (match.Groups["value"].Value == "1")
                {
                    enable |= flag;
                }
                else
                {
                    disable |= flag;
                }
            }

            if (enable != SubtitleTextStyle.None || disable != SubtitleTextStyle.None)
            {
                action = new MarkupAction(end - index + 1, enable, disable);
                return true;
            }
        }

        return false;
    }

    private static bool TryReadMicroDvdCode(string text, int index, out MicroDvdCode code)
    {
        code = default;
        if (text[index] != '{')
        {
            return false;
        }

        var end = text.IndexOf('}', index + 1);
        if (end < 0 || end - index > 120)
        {
            return false;
        }

        var content = text[(index + 1)..end].Trim();
        var length = end - index + 1;
        if (content.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            code = new MicroDvdCode(length, MicroDvdCodeKind.Default, IsGlobal: true);
            return true;
        }

        if (content.Length < 2 || content[1] != ':')
        {
            return false;
        }

        var value = content[2..].Trim();
        var isGlobal = char.IsUpper(content[0]);
        switch (char.ToLowerInvariant(content[0]))
        {
            case 'y':
                if (!TryParseStyle(value, out var style))
                {
                    return false;
                }

                code = new MicroDvdCode(length, MicroDvdCodeKind.Style, isGlobal, Style: style);
                return true;
            case 'f' when value.Length > 0:
                code = new MicroDvdCode(length, MicroDvdCodeKind.FontName, isGlobal, FontName: value);
                return true;
            case 's' when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fontSize) && fontSize > 0:
                code = new MicroDvdCode(length, MicroDvdCodeKind.FontSize, isGlobal, FontSize: fontSize);
                return true;
            case 'c' when TryParseMicroDvdColor(value, out var color):
                code = new MicroDvdCode(length, MicroDvdCodeKind.Color, isGlobal, Color: color);
                return true;
            case 'p' when content[0] == 'P' && TryParsePosition(value, out var position):
                code = new MicroDvdCode(length, MicroDvdCodeKind.Position, IsGlobal: true, Position: position);
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseStyle(string value, out SubtitleTextStyle style)
    {
        style = SubtitleTextStyle.None;
        foreach (var item in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            style |= item.ToLowerInvariant() switch
            {
                "b" => SubtitleTextStyle.Bold,
                "i" => SubtitleTextStyle.Italic,
                "u" => SubtitleTextStyle.Underline,
                "s" => SubtitleTextStyle.Strikeout,
                _ => SubtitleTextStyle.None
            };

            if (item.Length != 1 || item[0] is not ('b' or 'B' or 'i' or 'I' or 'u' or 'U' or 's' or 'S'))
            {
                return false;
            }
        }

        return value.Length == 0 || style != SubtitleTextStyle.None;
    }

    private static bool TryParseMicroDvdColor(string value, out Color color)
    {
        color = Color.Empty;
        if (value.Length != 7 || value[0] != '$' ||
            !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue) ||
            !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red))
        {
            return false;
        }

        color = Color.FromArgb(red, green, blue);
        return true;
    }

    private static bool TryParsePosition(string value, out Point position)
    {
        position = Point.Empty;
        var values = value.Split(',', StringSplitOptions.TrimEntries);
        if (values.Length != 2 ||
            !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            return false;
        }

        position = new Point(x, y);
        return true;
    }

    private static Color? TryReadHtmlColor(string content)
    {
        var match = Regex.Match(content, "\\bcolor\\s*=\\s*(?:\\\"(?<color>[^\\\"]+)\\\"|'(?<color>[^']+)'|(?<color>[^\\s>]+))", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            return ColorTranslator.FromHtml(match.Groups["color"].Value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsLegacyItalicMarker(char value, SubtitleRenderOptions options) =>
        (value == '/' && options.ItalicLinesStartingWithSlash) ||
        (value == '*' && options.ItalicLinesStartingWithAsterisk) ||
        (value == '#' && options.ItalicLinesStartingWithHash);

    private static void AddSpan(List<SubtitleEditorSpan> spans, int start, int length, SubtitleTextStyle style, bool isMarkup)
    {
        if (length == 0)
        {
            return;
        }

        if (spans.Count > 0)
        {
            var previous = spans[^1];
            if (previous.Start + previous.Length == start && previous.Style == style && previous.IsMarkup == isMarkup)
            {
                spans[^1] = previous with { Length = previous.Length + length };
                return;
            }
        }

        spans.Add(new SubtitleEditorSpan(start, length, style, isMarkup));
    }

    private readonly record struct MarkupAction(
        int Length,
        SubtitleTextStyle Enable,
        SubtitleTextStyle Disable,
        Color? Color = null,
        bool ResetColor = false);

    private enum MicroDvdCodeKind
    {
        Default,
        Style,
        FontName,
        FontSize,
        Color,
        Position
    }

    private readonly record struct MicroDvdCode(
        int Length,
        MicroDvdCodeKind Kind,
        bool IsGlobal,
        SubtitleTextStyle Style = SubtitleTextStyle.None,
        string? FontName = null,
        float? FontSize = null,
        Color? Color = null,
        Point? Position = null);
}
