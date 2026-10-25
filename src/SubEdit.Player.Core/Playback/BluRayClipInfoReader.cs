using System.Text;

namespace SubEdit.Player.Core.Playback;

/// <summary>
/// Informacje o pojedynczym strumieniu odczytanym z pliku Blu-ray Clip Information (.clpi).
/// </summary>
/// <param name="Pid">MPEG-TS Elementary Stream PID (np. 0x1100, 0x120F).</param>
/// <param name="CodingType">Typ kodowania strumienia (np. 0x81 AC3, 0x90 PGS).</param>
/// <param name="LanguageCode">Trzyliterowy kod języka ISO 639-2 (np. "pol", "eng") lub null, jeśli niedostępny.</param>
public sealed record BluRayClipStreamInfo(int Pid, byte CodingType, string? LanguageCode);

/// <summary>
/// Odczytuje metadane strumieni z plików informacyjnych klipów Blu-ray (.clpi).
/// </summary>
public static class BluRayClipInfoReader
{
    /// <summary>
    /// Sprawdza, czy odtwarzany plik znajduje się w strukturze katalogów Blu-ray (...\BDMV\STREAM\xxxxx.m2ts)
    /// i zwraca ścieżkę do odpowiadającego mu pliku .clpi (...\BDMV\CLIPINF\xxxxx.clpi).
    /// </summary>
    public static string? FindMatchingClpiPath(string? mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return null;
        }

        try
        {
            var ext = Path.GetExtension(mediaPath);
            if (!string.Equals(ext, ".m2ts", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".mts", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var streamDir = Path.GetDirectoryName(mediaPath);
            if (string.IsNullOrWhiteSpace(streamDir))
            {
                return null;
            }

            var streamDirName = Path.GetFileName(streamDir);
            if (!string.Equals(streamDirName, "STREAM", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var bdmvDir = Path.GetDirectoryName(streamDir);
            if (string.IsNullOrWhiteSpace(bdmvDir))
            {
                return null;
            }

            var bdmvDirName = Path.GetFileName(bdmvDir);
            if (!string.Equals(bdmvDirName, "BDMV", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(mediaPath);
            var clpiCandidate = Path.Combine(bdmvDir, "CLIPINF", fileNameWithoutExt + ".clpi");
            if (File.Exists(clpiCandidate))
            {
                return clpiCandidate;
            }

            // Fallback dla innych wielkości liter na systemach wrażliwych na wielkość znaków
            var clipinfDir = Path.Combine(bdmvDir, "CLIPINF");
            if (!Directory.Exists(clipinfDir))
            {
                clipinfDir = Path.Combine(bdmvDir, "clipinf");
            }

            if (Directory.Exists(clipinfDir))
            {
                var match = Directory.EnumerateFiles(clipinfDir, fileNameWithoutExt + ".*")
                    .FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".clpi", StringComparison.OrdinalIgnoreCase));
                if (match != null && File.Exists(match))
                {
                    return match;
                }
            }
        }
        catch
        {
            // Bezpieczny fallback w razie błędu wejścia-wyjścia
        }

        return null;
    }

    /// <summary>
    /// Odczytuje metadane strumieni (PID, typ kodowania, kod języka) z pliku .clpi.
    /// W razie błędu, nieobsługiwanego formatu lub uszkodzonego pliku zwraca pusty słownik bez rzucania wyjątków.
    /// </summary>
    public static IReadOnlyDictionary<int, BluRayClipStreamInfo> ReadClipInfo(string? clpiPath)
    {
        var result = new Dictionary<int, BluRayClipStreamInfo>();
        if (string.IsNullOrWhiteSpace(clpiPath))
        {
            return result;
        }

        try
        {
            if (!File.Exists(clpiPath))
            {
                return result;
            }

            byte[] data = File.ReadAllBytes(clpiPath);
            return ParseClipInfoData(data);
        }
        catch
        {
            // Fail-safe w razie błędu odczytu pliku
            return result;
        }
    }

    /// <summary>
    /// Parsuje zawartość binarną pliku CLPI (struktura nagłówka HDMV / CLPI oraz sekcja ProgramInfo).
    /// </summary>
    public static IReadOnlyDictionary<int, BluRayClipStreamInfo> ParseClipInfoData(byte[] data)
    {
        var result = new Dictionary<int, BluRayClipStreamInfo>();
        try
        {
            if (data == null || data.Length < 40)
            {
                return result;
            }

            string type = Encoding.ASCII.GetString(data, 0, 4);
            if (!string.Equals(type, "HDMV", StringComparison.Ordinal) &&
                !string.Equals(type, "CLPI", StringComparison.Ordinal))
            {
                return result;
            }

            // Offset 0x0C (12): ProgramInfo_start_address (uint32, big-endian)
            int programInfoOffset = (data[12] << 24) | (data[13] << 16) | (data[14] << 8) | data[15];
            if (programInfoOffset < 40 || programInfoOffset + 6 > data.Length)
            {
                return result;
            }

            int pos = programInfoOffset;
            int length = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            pos += 4;
            pos += 1; // 1 byte reserved
            int numProgSeq = data[pos++];

            for (int ps = 0; ps < numProgSeq && pos + 8 <= data.Length; ps++)
            {
                pos += 4; // SPN_program_sequence_start
                pos += 2; // program_map_PID
                int numStreams = data[pos++];
                pos += 1; // 1 byte reserved

                for (int s = 0; s < numStreams && pos + 3 <= data.Length; s++)
                {
                    int pid = (data[pos] << 8) | data[pos + 1];
                    pos += 2;
                    int streamCodingLen = data[pos++];
                    int nextStreamPos = pos + streamCodingLen;

                    if (pos < data.Length)
                    {
                        byte codingType = data[pos++];
                        string? lang = null;

                        switch (codingType)
                        {
                            case 0x80: // LPCM Audio
                            case 0x81: // AC-3 / Dolby Digital Audio
                            case 0x82: // DTS Audio
                            case 0x83: // TrueHD Audio
                            case 0x84: // Dolby Digital Plus (E-AC3)
                            case 0x85: // DTS-HD HRA Audio
                            case 0x86: // DTS-HD MA Audio
                            case 0xA1: // Secondary E-AC3 Audio
                            case 0xA2: // Secondary DTS-HD Audio
                                if (pos + 4 <= data.Length && pos + 4 <= nextStreamPos)
                                {
                                    pos += 1; // audio_format (4 bity) + sampling_frequency (4 bity)
                                    lang = ExtractLanguageCode(data, pos);
                                }
                                break;

                            case 0x90: // Presentation Graphics (PGS) Subtitle
                            case 0x91: // Interactive Graphics (IGS)
                                if (pos + 3 <= data.Length && pos + 3 <= nextStreamPos)
                                {
                                    lang = ExtractLanguageCode(data, pos);
                                }
                                break;

                            case 0x92: // Text Subtitle
                                if (pos + 4 <= data.Length && pos + 4 <= nextStreamPos)
                                {
                                    pos += 1; // char_code (1 bajt)
                                    lang = ExtractLanguageCode(data, pos);
                                }
                                break;
                        }

                        result[pid] = new BluRayClipStreamInfo(pid, codingType, lang);
                    }

                    pos = Math.Min(nextStreamPos, data.Length);
                }
            }
        }
        catch
        {
            // Fail-safe w razie uszkodzonych offsetów lub danych w pliku binarnym
        }

        return result;
    }

    private static string? ExtractLanguageCode(byte[] data, int offset)
    {
        if (offset + 3 > data.Length)
        {
            return null;
        }

        char c0 = (char)data[offset];
        char c1 = (char)data[offset + 1];
        char c2 = (char)data[offset + 2];

        if (char.IsLetter(c0) && char.IsLetter(c1) && char.IsLetter(c2))
        {
            string code = string.Create(3, (c0, c1, c2), static (span, state) =>
            {
                span[0] = char.ToLowerInvariant(state.c0);
                span[1] = char.ToLowerInvariant(state.c1);
                span[2] = char.ToLowerInvariant(state.c2);
            });

            if (!string.Equals(code, "und", StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return null;
    }
}
