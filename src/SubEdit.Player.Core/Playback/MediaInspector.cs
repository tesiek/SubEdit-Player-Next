namespace SubEdit.Player.Core.Playback;

public static class MediaInspector
{
    public static long? TryGetFileSizeBytes(string videoPath)
    {
        try { return new FileInfo(videoPath).Length; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    public static string TryGetFileSize(string videoPath)
    {
        try
        {
            return FormatFileSize(new FileInfo(videoPath).Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public static string NormalizeVideoCodecName(string? codecName)
    {
        if (string.IsNullOrWhiteSpace(codecName))
        {
            return string.Empty;
        }

        var trimmed = codecName.Trim();

        if (string.Equals(trimmed, "h264", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "h.264", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "avc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "avc1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "V_MPEG4/ISO/AVC", StringComparison.OrdinalIgnoreCase))
        {
            return "H.264";
        }

        if (string.Equals(trimmed, "h265", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "h.265", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "hevc", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "hvc1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "V_MPEGH/ISO/HEVC", StringComparison.OrdinalIgnoreCase))
        {
            return "H.265";
        }

        if (string.Equals(trimmed, "mpeg2video", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "mpeg2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "mpeg-2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "V_MPEG2", StringComparison.OrdinalIgnoreCase))
        {
            return "MPEG2";
        }

        return trimmed;
    }

    public static string GetFourCc(uint fourCc) => DecodeFourCc(fourCc);

    private static string DecodeFourCc(uint fourCc)
    {
        if (fourCc == 0)
        {
            return string.Empty;
        }

        Span<char> characters = stackalloc char[4];
        for (var index = 0; index < characters.Length; index++)
        {
            var b = (byte)((fourCc >> (index * 8)) & 0xff);
            if (b is < 0x20 or > 0x7e)
            {
                return string.Empty;
            }

            characters[index] = (char)b;
        }

        return new string(characters).Trim();
    }

    private static string FormatFileSize(long bytes)
    {
        const long megabyte = 1024 * 1024;
        const long gigabyte = megabyte * 1024;
        return bytes >= gigabyte
            ? $"{bytes / (double)gigabyte:0.0} GB"
            : $"{bytes / (double)megabyte:0.0} MB";
    }
}
