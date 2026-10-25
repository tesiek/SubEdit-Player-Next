using System.Text;

namespace SubEdit.Player.WinForms;

internal static class SubtitleItalicMarkerProcessor
{
    public static SubtitleItalicMarkerConversionResult Convert(
        string text,
        IReadOnlySet<char> markers,
        double framesPerSecond)
    {
        if (string.IsNullOrEmpty(text) || markers.Count == 0)
        {
            return new SubtitleItalicMarkerConversionResult(text, 0);
        }

        var output = new StringBuilder(text.Length);
        var changedLineCount = 0;
        var isSrtDocument = SubtitleFormatConverter.IsSrtDocument(text);
        var insideSrtCueText = false;

        for (var lineStart = 0; lineStart < text.Length;)
        {
            var lineEnd = lineStart;
            while (lineEnd < text.Length && text[lineEnd] is not ('\r' or '\n'))
            {
                lineEnd++;
            }

            var line = text[lineStart..lineEnd];
            var updatedLine = ConvertLine(
                line,
                markers,
                framesPerSecond,
                isSrtDocument,
                ref insideSrtCueText);
            if (!string.Equals(updatedLine, line, StringComparison.Ordinal))
            {
                changedLineCount++;
            }

            output.Append(updatedLine);
            if (lineEnd < text.Length)
            {
                output.Append(text[lineEnd]);
                lineEnd++;
                if (lineEnd < text.Length && text[lineEnd - 1] == '\r' && text[lineEnd] == '\n')
                {
                    output.Append(text[lineEnd]);
                    lineEnd++;
                }
            }

            lineStart = lineEnd;
        }

        return changedLineCount == 0
            ? new SubtitleItalicMarkerConversionResult(text, 0)
            : new SubtitleItalicMarkerConversionResult(output.ToString(), changedLineCount);
    }

    private static string ConvertLine(
        string line,
        IReadOnlySet<char> markers,
        double framesPerSecond,
        bool isSrtDocument,
        ref bool insideSrtCueText)
    {
        if (isSrtDocument)
        {
            if (SubtitleDocument.TryGetCueTiming(line, framesPerSecond, out _, out _))
            {
                insideSrtCueText = true;
                return line;
            }

            if (insideSrtCueText && string.IsNullOrWhiteSpace(line))
            {
                insideSrtCueText = false;
                return line;
            }

            return insideSrtCueText ? ConvertSubtitleText(line, markers) : line;
        }

        if (!SubtitleDocument.TryGetTmPlayerCue(line, out _, out var subtitleText) &&
            !SubtitleDocument.TryGetInlineCue(line, framesPerSecond, out _, out _, out subtitleText))
        {
            return line;
        }

        var subtitleTextStart = line.Length - subtitleText.Length;
        return line[..subtitleTextStart] + ConvertSubtitleText(subtitleText, markers);
    }

    private static string ConvertSubtitleText(string text, IReadOnlySet<char> markers)
    {
        var output = new StringBuilder(text.Length);
        for (var segmentStart = 0; segmentStart <= text.Length;)
        {
            var separator = text.IndexOf('|', segmentStart);
            var segmentEnd = separator >= 0 ? separator : text.Length;
            output.Append(ConvertSegment(text[segmentStart..segmentEnd], markers));
            if (separator < 0)
            {
                break;
            }

            output.Append('|');
            segmentStart = separator + 1;
        }

        return output.ToString();
    }

    private static string ConvertSegment(string segment, IReadOnlySet<char> markers)
    {
        if (segment.Length == 0)
        {
            return segment;
        }

        var prefixLength = SubtitleMarkup.GetLeadingMicroDvdCodesLength(segment);
        var prefix = segment[..prefixLength];
        var body = segment[prefixLength..];

        if (body.Length == 0)
        {
            return segment;
        }

        var removeLeadingMarker = markers.Contains(body[0]);
        var removeTrailingMarker = markers.Contains(body[^1]);
        if (!removeLeadingMarker && !removeTrailingMarker)
        {
            return segment;
        }

        var contentStart = removeLeadingMarker ? 1 : 0;
        var contentEnd = body.Length - (removeTrailingMarker ? 1 : 0);
        if (contentEnd <= contentStart)
        {
            return segment;
        }

        return $"{prefix}<i>{body[contentStart..contentEnd]}</i>";
    }
}

internal readonly record struct SubtitleItalicMarkerConversionResult(string Text, int ChangedLineCount);
