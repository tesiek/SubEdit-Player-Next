using System.Text;

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

    public static string GetCodecName(
        string? videoPath,
        string? originalFourCc,
        string? codecDescription)
    {
        var isAvi = string.Equals(Path.GetExtension(videoPath), ".avi", StringComparison.OrdinalIgnoreCase);
        if (isAvi)
        {
            var aviFourCc = !string.IsNullOrWhiteSpace(originalFourCc)
                ? originalFourCc
                : TryReadAviVideoFourCc(videoPath);

            if (!string.IsNullOrWhiteSpace(aviFourCc))
            {
                return aviFourCc.ToUpperInvariant();
            }
        }

        var matroskaCodecId = TryReadMatroskaVideoCodecId(videoPath);
        if (!string.IsNullOrWhiteSpace(matroskaCodecId))
        {
            return NormalizeVideoCodecName(NormalizeMatroskaCodecId(matroskaCodecId));
        }

        var rawCodecName = string.IsNullOrWhiteSpace(codecDescription)
            ? (originalFourCc ?? string.Empty)
            : codecDescription.Split([' ', '-', '('], StringSplitOptions.RemoveEmptyEntries)[0];

        return NormalizeVideoCodecName(rawCodecName);
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

    private static string NormalizeMatroskaCodecId(string codecId)
    {
        if (codecId.StartsWith("V_MPEGH", StringComparison.OrdinalIgnoreCase))
        {
            return "H.265";
        }

        if (codecId.StartsWith("V_MPEG4/ISO/AVC", StringComparison.OrdinalIgnoreCase))
        {
            return "H.264";
        }

        if (codecId.StartsWith("V_MPEG2", StringComparison.OrdinalIgnoreCase))
        {
            return "MPEG2";
        }

        return codecId.Length > 2 ? codecId[2..] : codecId;
    }

    private static string? TryReadMatroskaVideoCodecId(string? videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) ||
            Path.GetExtension(videoPath) is not (".mkv" or ".webm"))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(videoPath);
            var buffer = new byte[Math.Min(stream.Length, 2 * 1024 * 1024)];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            for (var index = 0; index < bytesRead - 2; index++)
            {
                if (buffer[index] != (byte)'V' || buffer[index + 1] != (byte)'_')
                {
                    continue;
                }

                var end = index;
                while (end < bytesRead && end - index < 80 && buffer[end] is >= 0x20 and <= 0x7e)
                {
                    end++;
                }

                var codecId = Encoding.ASCII.GetString(buffer, index, end - index);
                if (codecId.StartsWith("V_", StringComparison.Ordinal))
                {
                    return codecId;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static string? TryReadAviVideoFourCc(string? videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) ||
            !string.Equals(Path.GetExtension(videoPath), ".avi", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(videoPath);
            var buffer = new byte[64 * 1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            for (var index = 0; index <= bytesRead - 16; index++)
            {
                if (Encoding.ASCII.GetString(buffer, index, 4) == "strh" &&
                    Encoding.ASCII.GetString(buffer, index + 8, 4) == "vids")
                {
                    return Encoding.ASCII.GetString(buffer, index + 12, 4).Trim();
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return null;
    }

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
