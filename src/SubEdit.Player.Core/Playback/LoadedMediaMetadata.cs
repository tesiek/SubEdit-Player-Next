using System.Globalization;

namespace SubEdit.Player.Core.Playback;

/// <summary>Interpret already loaded metadata, without opening or probing media.</summary>
public static class LoadedMediaMetadata
{
    // File-properties display only; does not change language selection or playback policy.
    public static string? PropertiesLanguageName(string? originalCode, string? resolvedName = null)
    {
        var code = originalCode?.Trim();
        var resolved = string.IsNullOrWhiteSpace(resolvedName) || resolvedName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            ? null : resolvedName;
        return code?.ToLowerInvariant() switch
        {
            "khk-cyrl" or "khk_cyrl" or "khk (cyrl)" => "Mongolian (Khalkha, Cyrillic)",
            "khk" => "Mongolian (Khalkha)",
            null or "" or "und" => resolved,
            _ => resolved ?? code
        };
    }

    public static string? ContainerName(string? demuxer, string path, string? longName)
    {
        if (string.Equals(demuxer, "matroska,webm", StringComparison.OrdinalIgnoreCase))
        {
            // FFmpeg exposes one demuxer for both formats, not the EBML DocType.
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".mkv" => "Matroska (MKV)", ".webm" => "WebM", _ => "Matroska / WebM"
            };
        }
        return string.IsNullOrWhiteSpace(longName) ? demuxer : longName;
    }

    public static long? StreamBitrate(long codecBitrate, IEnumerable<KeyValuePair<string, string>> metadata, long streamBitrate = 0)
    {
        if (codecBitrate > 0) return codecBitrate;
        long? tagged = null;
        var conflict = false;
        foreach (var (key, value) in metadata)
        {
            // Matroska BPS tags describe this stream, not the container's total bitrate.
            if (!key.Equals("BPS", StringComparison.OrdinalIgnoreCase) &&
                !key.StartsWith("BPS-", StringComparison.OrdinalIgnoreCase)) continue;
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
                parsed < 1 || parsed > long.MaxValue) continue;
            var bitrate = (long)decimal.Round(parsed, 0, MidpointRounding.AwayFromZero);
            if (tagged.HasValue && tagged != bitrate) conflict = true;
            tagged = bitrate;
        }
        return !conflict && tagged.HasValue ? tagged : streamBitrate > 0 ? streamBitrate : null;
    }
}
