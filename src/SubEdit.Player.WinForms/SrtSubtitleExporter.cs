using System.Globalization;
using System.Text;

namespace SubEdit.Player.WinForms;

internal static class SrtSubtitleExporter
{
    public static bool HasNegativeTimestamps(SubtitleDocument document) =>
        document.Cues.Any(c => c.StartMilliseconds < 0 || c.EndMilliseconds < 0);

    public static string Export(SubtitleDocument document)
    {
        if (HasNegativeTimestamps(document))
        {
            throw new InvalidOperationException("Nie można wyeksportować napisów SRT zawierających ujemne czasy. Format SRT wymaga nieujemnych znaczników czasu.");
        }

        var blocks = new List<string>(document.Cues.Count);
        for (var index = 0; index < document.Cues.Count; index++)
        {
            var cue = document.Cues[index];
            var endMilliseconds = Math.Max(cue.StartMilliseconds, cue.EndMilliseconds);
            var lines = string.Join("\r\n", cue.Lines.Select(ExportLine));
            blocks.Add(
                $"{index + 1}\r\n" +
                $"{FormatTimestamp(cue.StartMilliseconds)} --> {FormatTimestamp(endMilliseconds)}\r\n" +
                lines);
        }

        return string.Join("\r\n\r\n", blocks);
    }

    private static string ExportLine(SubtitleLine line)
    {
        var text = new StringBuilder();
        foreach (var run in line.Runs)
        {
            var prefix = new StringBuilder();
            var suffix = new StringBuilder();
            AddTag(SubtitleTextStyle.Bold, "b");
            AddTag(SubtitleTextStyle.Italic, "i");
            AddTag(SubtitleTextStyle.Underline, "u");
            AddTag(SubtitleTextStyle.Strikeout, "s");

            if (run.Color is { } color)
            {
                prefix.Append($"<font color=\"#{color.R:X2}{color.G:X2}{color.B:X2}\">");
                suffix.Insert(0, "</font>");
            }

            text.Append(prefix).Append(run.Text).Append(suffix);

            void AddTag(SubtitleTextStyle style, string tag)
            {
                if (!run.Style.HasFlag(style))
                {
                    return;
                }

                prefix.Append('<').Append(tag).Append('>');
                suffix.Insert(0, $"</{tag}>");
            }
        }

        return text.ToString();
    }

    private static string FormatTimestamp(long milliseconds) =>
        SubtitleTimingFormatter.FormatSrtTimestamp(milliseconds, ',');
}
