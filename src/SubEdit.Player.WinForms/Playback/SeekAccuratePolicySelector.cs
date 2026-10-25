using System;
using System.Collections.Generic;
using FlyleafLib.MediaPlayer;

namespace SubEdit.Player.WinForms.Playback;

/// <summary>
/// Selects the appropriate <see cref="SeekAccurateMode"/> (Conditional vs Legacy) based on the actual demuxer/container format name reported by FFmpeg/Flyleaf.
/// </summary>
public static class SeekAccuratePolicySelector
{
    private static readonly HashSet<string> ConditionalFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        // Matroska / WebM
        "matroska",
        "webm",

        // QuickTime / MP4 / ISO base media
        "mov",
        "mp4",
        "m4a",
        "3gp",
        "3g2",
        "mj2",

        // AVI
        "avi"
    };

    private static readonly HashSet<string> KnownLegacyFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        // MPEG Transport Stream
        "mpegts"
    };

    /// <summary>
    /// Evaluates the FFmpeg demuxer / format name string (which can be a comma-separated list like "mov,mp4,m4a,3gp,3g2,mj2")
    /// and returns the recommended <see cref="SeekAccurateMode"/>.
    /// Unrecognized, empty, or null format strings safely fall back to <see cref="SeekAccurateMode.Legacy"/>.
    /// </summary>
    public static SeekAccurateMode Select(string? formatName)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return SeekAccurateMode.Legacy;
        }

        var tokens = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return SeekAccurateMode.Legacy;
        }

        // If any token matches a format known to require Legacy, prioritize Legacy for safety.
        foreach (var token in tokens)
        {
            if (KnownLegacyFormats.Contains(token))
            {
                return SeekAccurateMode.Legacy;
            }
        }

        // If any token matches a format confirmed for Conditional, use Conditional.
        foreach (var token in tokens)
        {
            if (ConditionalFormats.Contains(token))
            {
                return SeekAccurateMode.Conditional;
            }
        }

        // Safe fallback for unknown / unconfirmed formats
        return SeekAccurateMode.Legacy;
    }
}
