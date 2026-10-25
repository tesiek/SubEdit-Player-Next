using System.Text;

namespace SubEdit.Player.WinForms;

internal sealed record SubtitleFileContent(string Text, Encoding Encoding);

internal enum SubtitleEncodingKind
{
    Ansi,
    Utf8,
    Utf8Bom,
    Utf16BigEndianBom,
    Utf16LittleEndianBom
}

internal static class SubtitleFileReader
{
    public static SubtitleFileContent Read(string subtitlePath)
    {
        var bytes = File.ReadAllBytes(subtitlePath);
        if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
        {
            var encoding = new UnicodeEncoding(false, true);
            return new SubtitleFileContent(encoding.GetString(bytes, 2, bytes.Length - 2), encoding);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
        {
            var encoding = new UnicodeEncoding(true, true);
            return new SubtitleFileContent(encoding.GetString(bytes, 2, bytes.Length - 2), encoding);
        }

        var hasUtf8Bom = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf;
        var utf8 = new UTF8Encoding(hasUtf8Bom, true);
        var offset = hasUtf8Bom ? 3 : 0;
        try
        {
            return new SubtitleFileContent(utf8.GetString(bytes, offset, bytes.Length - offset), utf8);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1250, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
            return new SubtitleFileContent(encoding.GetString(bytes), encoding);
        }
    }

    public static Encoding CreateEncoding(SubtitleEncodingKind encodingKind)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return encodingKind switch
        {
            SubtitleEncodingKind.Ansi => Encoding.GetEncoding(
                1250,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ReplacementFallback),
            SubtitleEncodingKind.Utf8 => new UTF8Encoding(false, true),
            SubtitleEncodingKind.Utf8Bom => new UTF8Encoding(true, true),
            SubtitleEncodingKind.Utf16BigEndianBom => new UnicodeEncoding(true, true, true),
            SubtitleEncodingKind.Utf16LittleEndianBom => new UnicodeEncoding(false, true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingKind))
        };
    }

    public static SubtitleEncodingKind GetEncodingKind(Encoding? encoding)
    {
        if (encoding is null)
        {
            return SubtitleEncodingKind.Utf8;
        }

        return encoding.CodePage switch
        {
            1200 => SubtitleEncodingKind.Utf16LittleEndianBom,
            1201 => SubtitleEncodingKind.Utf16BigEndianBom,
            65001 when encoding.GetPreamble().Length > 0 => SubtitleEncodingKind.Utf8Bom,
            65001 => SubtitleEncodingKind.Utf8,
            _ => SubtitleEncodingKind.Ansi
        };
    }

}
