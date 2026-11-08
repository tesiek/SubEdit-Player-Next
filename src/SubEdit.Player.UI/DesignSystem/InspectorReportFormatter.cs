using System.Globalization;
using System.Text;

namespace SubEdit.Player.UI.DesignSystem;

internal static class InspectorReportFormatter
{
    public static string Format(IReadOnlyList<InspectorSection> sections, Font font, UiDesignTokens tokens)
    {
        var text = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 ");
        Escape(text, font.Name);
        text.Append(@";}}{\colortbl ;");
        Color(text, tokens.Text);
        Color(text, tokens.InspectorHeading);
        text.Append(@"}\viewkind4\uc1\f0\fs").Append((int)Math.Round(font.SizeInPoints * 2));
        var columnTwips = tokens.InspectorLabelWidth * 15;
        foreach (var section in sections)
        {
            text.Append(@"\pard\li0\fi0\sb180\sa90\cf2\b ");
            Escape(text, section.Title);
            text.Append(@"\b0\cf1\par ");
            foreach (var value in section.Values)
            {
                text.Append(@"\pard\li").Append(columnTwips).Append(@"\fi-").Append(columnTwips)
                    .Append(@"\tx").Append(columnTwips).Append(@"\sb0\sa60\cf1 ");
                Escape(text, value.Name);
                text.Append(@"\tab ");
                Escape(text, value.Value);
                text.Append(@"\par ");
            }
        }
        return text.Append('}').ToString();
    }

    private static void Color(StringBuilder text, Color color) => text.Append(@"\red").Append(color.R)
        .Append(@"\green").Append(color.G).Append(@"\blue").Append(color.B).Append(';');

    private static void Escape(StringBuilder text, string value)
    {
        foreach (var character in value)
        {
            if (character is '\\' or '{' or '}') text.Append('\\').Append(character);
            else if (character == '\n') text.Append(@"\line ");
            else if (character != '\r' && character < 128) text.Append(character);
            else if (character != '\r') text.Append(@"\u").Append(unchecked((short)character).ToString(CultureInfo.InvariantCulture)).Append('?');
        }
    }
}
