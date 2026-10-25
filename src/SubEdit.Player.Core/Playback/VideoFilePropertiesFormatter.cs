using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SubEdit.Player.Core.Playback;

public sealed record VideoFilePropertiesSection(string Heading, IReadOnlyList<(string Label, string Value)> Rows);

/// <summary>One representation for both the dialog and its complete clipboard report.</summary>
public static class VideoFilePropertiesFormatter
{
    public static IReadOnlyList<VideoFilePropertiesSection> GetSections(VideoFileProperties file)
    {
        var sections = new List<VideoFilePropertiesSection>
        {
            Section("OGÓLNE",
                ("Nazwa pliku", Value(file.FileName)), ("Kontener", Value(file.Container)),
                ("Rozmiar", Size(file.FileSizeBytes)), ("Czas trwania", Time(file.DurationMilliseconds)),
                ("Średni bitrate pliku", Rate(file.Bitrate)),
                ("Ścieżki wideo", file.Video.Count.ToString()), ("Ścieżki audio", file.Audio.Count.ToString()),
                ("Ścieżki napisów", file.Subtitles.Count.ToString()), ("Rozdziały", file.Chapters.Count.ToString()))
        };
        for (var i = 0; i < file.Video.Count; i++)
        {
            var video = file.Video[i];
            var rows = new List<(string Label, string Value)>
            {
                ("Kodek", Value(MediaCodecDisplay.VideoName(video.Stream.Codec))),
                ("Profil", Value(MediaCodecDisplay.Profile(video.Stream.Profile, video.Level)))
            };
            rows.AddRange([
                ("Rozdzielczość", video.Width is > 0 && video.Height is > 0 ? $"{video.Width} × {video.Height}" : "—"),
                ("Proporcje obrazu", AspectRatio(video)),
                ("FPS", FrameRate(video)),
                ("Bitrate", Rate(video.Bitrate)), ("Format pikseli", Value(MediaCodecDisplay.PixelFormat(video.PixelFormat))),
                ("Głębia bitowa", video.BitDepth is > 0 ? $"{video.BitDepth} bit" : "—"),
                ("Przestrzeń barw", Value(MediaCodecDisplay.Color(video.ColorSpace))),
                ("Barwy podstawowe", Value(MediaCodecDisplay.Color(video.ColorPrimaries))),
                ("Charakterystyka transferu", Value(MediaCodecDisplay.Color(video.ColorTransfer))),
                ("Zakres barw", Value(MediaCodecDisplay.Color(video.ColorRange))),
                ("HDR / SDR", Value(video.DynamicRange))]);
            sections.Add(new VideoFilePropertiesSection($"WIDEO #{i + 1}", rows.AsReadOnly()));
        }
        if (file.Video.Count == 0) sections.Add(Section("WIDEO", ("", "Brak")));
        for (var i = 0; i < file.Audio.Count; i++)
        {
            var audio = file.Audio[i];
            var rows = new List<(string Label, string Value)>();
            var format = MediaCodecDisplay.AudioFormat(audio.Stream.Codec, audio.CodecVariant, audio.Stream.Profile);
            rows.Add(("Format", Value(format)));
            rows.AddRange([
                ("Kodek", Value(MediaCodecDisplay.AudioName(audio.Stream.Codec, audio.CodecVariant))),
                ("Profil", Value(audio.Stream.Profile)),
                ("Język", Value(audio.Stream.Language))]);
            if (!string.IsNullOrWhiteSpace(audio.Stream.Title) && audio.Stream.Title.Trim() != "—")
                rows.Add(("Tytuł", audio.Stream.Title));
            rows.Add(("Domyślna", Flag(audio.Stream.IsDefault)));
            if (audio.Stream.IsForced == true) rows.Add(("Wymuszona", "Tak"));
            rows.AddRange([("Kanały", Channels(audio)), ("Próbkowanie", audio.SampleRate is > 0 ? $"{audio.SampleRate} Hz" : "—"),
                ("Bitrate", Rate(audio.Bitrate))]);
            sections.Add(new VideoFilePropertiesSection($"AUDIO #{i + 1}", rows.AsReadOnly()));
        }
        if (file.Audio.Count == 0) sections.Add(Section("AUDIO", ("", "Brak")));
        var subtitles = file.Subtitles.Select((subtitle, i) => ("", SubtitleRow(subtitle, i + 1))).ToArray();
        sections.Add(Section($"NAPISY — {file.Subtitles.Count} ŚCIEŻEK", subtitles.Length > 0 ? subtitles : [("", "Brak")]));
        var chapters = file.Chapters.Select((chapter, i) =>
            (Time(chapter.PositionMilliseconds), string.IsNullOrWhiteSpace(chapter.Name) ? $"Rozdział #{i + 1}" : chapter.Name)).ToArray();
        sections.Add(Section($"ROZDZIAŁY — {file.Chapters.Count}", chapters.Length > 0 ? chapters : [("", "Brak")]));
        return sections.AsReadOnly();
    }

    public static string FormatText(VideoFileProperties file) => FormatText(GetSections(file));

    public static string FormatText(IReadOnlyList<VideoFilePropertiesSection> sections)
    {
        var text = new StringBuilder();
        foreach (var section in sections)
        {
            if (text.Length > 0) text.AppendLine();
            text.Append('[').Append(section.Heading).AppendLine("]");
            foreach (var (label, value) in section.Rows)
                text.AppendLine(string.IsNullOrEmpty(label) ? value : $"{label}: {value}");
        }
        return text.ToString();
    }

    public static string Channels(AudioStreamProperties audio)
    {
        if (audio.Channels is not > 0) return "—";
        var count = audio.Channels.Value;
        var noun = count == 1 ? "kanał"
            : count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14) ? "kanały" : "kanałów";
        var text = $"{count} {noun}";
        if (audio.IsChannelLayoutConfirmed && !string.IsNullOrWhiteSpace(audio.ChannelLayout))
            return $"{text} ({MediaCodecDisplay.EffectiveLayout(audio.ChannelLayout)})";
        return text;
    }

    private static string AspectRatio(VideoStreamProperties video)
    {
        if (video.DisplayAspectRatioNumerator is not > 0 || video.DisplayAspectRatioDenominator is not > 0)
            return "—";
        var numerator = (decimal)video.DisplayAspectRatioNumerator.Value;
        var denominator = (decimal)video.DisplayAspectRatioDenominator.Value;
        // Exact comparisons, not recognition from the rounded display value.
        if (numerator * 9 == denominator * 16) return "16:9";
        if (numerator * 3 == denominator * 4) return "4:3";
        return (numerator / denominator).ToString("0.00", CultureInfo.InvariantCulture) + ":1";
    }

    private static string FrameRate(VideoStreamProperties video)
    {
        if (video.FrameRateNumerator.HasValue || video.FrameRateDenominator.HasValue)
        {
            if (video.FrameRateNumerator is not > 0 || video.FrameRateDenominator is not > 0) return "—";
            var numerator = video.FrameRateNumerator.Value;
            var denominator = video.FrameRateDenominator.Value;
            var text = ((double)numerator / denominator).ToString("0.###", CultureInfo.CurrentCulture);
            return numerator % denominator == 0 ? text : $"{text} ({numerator}/{denominator})";
        }
        return video.FramesPerSecond is > 0 && double.IsFinite(video.FramesPerSecond.Value)
            ? video.FramesPerSecond.Value.ToString("0.###", CultureInfo.CurrentCulture) : "—";
    }

    private static string SubtitleRow(SubtitleStreamProperties subtitle, int number)
    {
        var stream = subtitle.Stream;
        var parts = new List<string>
        {
            Value(LoadedMediaMetadata.PropertiesLanguageName(stream.Language)),
            Value(MediaCodecDisplay.SubtitleName(stream.Codec))
        };
        var title = stream.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title)) parts.Add(title);
        if (stream.IsDefault == true) parts.Add("Domyślna");
        if (stream.IsForced == true) parts.Add("Wymuszona");
        if (subtitle.IsHearingImpaired == true &&
            !Regex.IsMatch(title ?? "", @"\bSDH\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            parts.Add("SDH");
        return $"#{number} — {string.Join(" | ", parts)}";
    }

    private static VideoFilePropertiesSection Section(string heading, params (string Label, string Value)[] rows) =>
        new(heading, Array.AsReadOnly(rows));
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    private static string Flag(bool? flag) => flag is null ? "—" : flag.Value ? "Tak" : "Nie";
    private static string Rate(long? value) => value is > 0
        ? $"{decimal.Round(value.Value / 1000m, 0, MidpointRounding.AwayFromZero)} kb/s" : "—";
    private static string Size(long? bytes) => bytes is >= 0
        ? $"{(bytes.Value / 1024).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ')} KB" : "—";
    private static string Time(long? milliseconds)
    {
        if (milliseconds is null or < 0) return "—";
        var value = milliseconds.Value;
        return $"{value / 3600000:00}:{value / 60000 % 60:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }
}
