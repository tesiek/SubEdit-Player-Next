using System.Globalization;
using System.Text;

namespace SubEdit.Player.Core.Playback;

public sealed record EmbeddedTrackCandidate(int Id, string? Language, string? Description, string? Name);

public static class AutomaticSubtitleSelector
{
    private static readonly string[] ExternalSubtitleExtensions = [".txt", ".srt", ".sub", ".mpl"];
    private static readonly string[] NonStandardSubtitleMarkers =
    [
        "forced", "sdh", "hearing impaired", "hearing-impaired", "commentary", "komentarz",
        "signs", "songs", "signs songs", "dla nieslyszacych", "nieslyszacych"
    ];

    public static string? SelectExternalSubtitle(string videoPath, IEnumerable<string> subtitlePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(subtitlePaths);

        var videoName = Path.GetFileNameWithoutExtension(videoPath);
        if (string.IsNullOrWhiteSpace(videoName))
        {
            return null;
        }

        return subtitlePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new
            {
                Path = path,
                ExtensionRank = GetExternalExtensionRank(Path.GetExtension(path)),
                FileName = Path.GetFileNameWithoutExtension(path)
            })
            .Where(item => item.ExtensionRank >= 0 && !string.IsNullOrWhiteSpace(item.FileName))
            .OrderBy(item => item.ExtensionRank)
            .ThenBy(item => string.Equals(item.FileName, videoName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(item => GetNameSimilarity(videoName, item.FileName))
            .ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    public static int? SelectPreferredAudio(IEnumerable<EmbeddedTrackCandidate> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        return tracks
            .Where(track => track.Id >= 0)
            .OrderBy(track => GetAudioLanguageRank(track.Language))
            .ThenBy(track => track.Id)
            .Select(track => GetAudioLanguageRank(track.Language) < 2 ? track.Id : (int?)null)
            .FirstOrDefault(id => id is not null);
    }

    public static bool IsPolishAudio(EmbeddedTrackCandidate track) => IsPolish(track.Language);

    public static int? SelectPreferredSubtitle(IEnumerable<EmbeddedTrackCandidate> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        return tracks
            .Where(track => track.Id >= 0)
            .OrderBy(track => GetSubtitleRank(track))
            .ThenBy(track => track.Id)
            .Select(track => GetSubtitleRank(track) < 4 ? track.Id : (int?)null)
            .FirstOrDefault(id => id is not null);
    }

    private static int GetExternalExtensionRank(string extension) =>
        Array.FindIndex(ExternalSubtitleExtensions, item =>
            string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));

    private static int GetAudioLanguageRank(string? language) =>
        IsPolish(language) ? 0 : IsEnglish(language) ? 1 : 2;

    private static int GetSubtitleRank(EmbeddedTrackCandidate track)
    {
        var isNonStandard = IsNonStandardSubtitle(track);
        if (IsPolish(track.Language)) return isNonStandard ? 1 : 0;
        if (IsEnglish(track.Language)) return isNonStandard ? 3 : 2;
        return 4;
    }

    private static bool IsNonStandardSubtitle(EmbeddedTrackCandidate track)
    {
        var description = Normalize($"{track.Description} {track.Name}");
        if (NonStandardSubtitleMarkers.Any(marker => description.Contains(marker, StringComparison.Ordinal)))
        {
            return true;
        }

        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => word is "hi" or "cc");
    }

    private static bool IsPolish(string? language) => MatchesLanguage(language, "pl", "pol", "polish");

    private static bool IsEnglish(string? language) => MatchesLanguage(language, "en", "eng", "english");

    private static bool MatchesLanguage(string? language, params string[] acceptedValues)
    {
        var normalized = Normalize(language);
        return acceptedValues.Contains(normalized, StringComparer.Ordinal);
    }

    private static int GetNameSimilarity(string left, string right)
    {
        var normalizedLeft = Normalize(left).Replace(" ", string.Empty, StringComparison.Ordinal);
        var normalizedRight = Normalize(right).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0;
        }

        var commonPrefixLength = 0;
        while (commonPrefixLength < normalizedLeft.Length &&
               commonPrefixLength < normalizedRight.Length &&
               normalizedLeft[commonPrefixLength] == normalizedRight[commonPrefixLength])
        {
            commonPrefixLength++;
        }

        var leftTokens = Normalize(left).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightTokens = Normalize(right).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var commonTokens = leftTokens.Intersect(rightTokens, StringComparer.Ordinal).Count();
        return commonTokens * 10_000 + commonPrefixLength * 100 - LevenshteinDistance(normalizedLeft, normalizedRight);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;
            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + (left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var characters = new List<char>(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            characters.Add(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return string.Join(' ', new string(characters.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
