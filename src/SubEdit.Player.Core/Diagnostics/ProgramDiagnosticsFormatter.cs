using System.Globalization;
using System.Text;

namespace SubEdit.Player.Core.Diagnostics;

public static class ProgramDiagnosticsFormatter
{
    private const string Unavailable = "niedostępne";

    public static string Format(ProgramDiagnosticsSnapshot snapshot, bool loadingSystem = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var text = new StringBuilder();
        var program = snapshot.Program;
        text.AppendLine("[PROGRAM]");
        Field("Nazwa", Value(program.Name));
        Field("Wersja", VersionValue(program.ProductVersion));
        Field("Build Date", Value(program.BuildDate));
        Field("Commit", Value(program.BuildCommit));
        Field("Stan buildu", Clean(program.BuildDirtyState).ToUpperInvariant() switch
        {
            "CLEAN" => "CLEAN",
            "DIRTY" => "DIRTY",
            _ => "UNKNOWN"
        });
        Field("Architektura", Value(program.ProcessArchitecture));
        Field(".NET Runtime", Value(program.Runtime));

        text.AppendLine().AppendLine("[BIBLIOTEKI]");
        if (snapshot.Libraries.Count == 0)
            text.AppendLine(Unavailable);
        foreach (var library in snapshot.Libraries)
        {
            var details = Clean(library.Details);
            Field(Value(library.Name), VersionValue(library.Version)
                + (details.Length == 0 ? "" : $" [{details}]"));
        }

        text.AppendLine().AppendLine("[SYSTEM I SPRZĘT]");
        if (loadingSystem)
            text.AppendLine("Pobieranie danych systemowych...");
        var system = snapshot.System;
        Field("Windows", Value(system?.Windows));
        Field("Wersja", Value(system?.Version));
        Field("Build", Value(system?.Build));
        Field("CPU", Value(system?.Cpu));
        Field("RAM", system?.InstalledMemoryBytes is ulong bytes
            ? FormattableString.Invariant($"{bytes / 1073741824m:0.##} GiB ({bytes} bajtów)")
            : Unavailable);
        if (system is null || system.Gpus.Count == 0)
        {
            Field("GPU #1", Unavailable);
            Field("Sterownik GPU #1", Unavailable);
        }
        else
        {
            for (var i = 0; i < system.Gpus.Count; i++)
            {
                var number = (i + 1).ToString(CultureInfo.InvariantCulture);
                Field($"GPU #{number}", Value(system.Gpus[i].Name));
                Field($"Sterownik GPU #{number}", VersionValue(system.Gpus[i].DriverVersion));
            }
        }

        text.AppendLine().AppendLine("[AKTUALNY FILM]");
        var media = snapshot.Media;
        if (!media.IsOpen)
            text.AppendLine("Brak otwartego pliku.");
        else
        {
            Field("Nazwa", FileName(media.FileName));
            Field("Rozmiar", media.FileSizeBytes is >= 0
                ? media.FileSizeBytes.Value.ToString(CultureInfo.InvariantCulture) + " bajtów" : Unavailable);
            Field("Kontener", Value(media.Container));
            Field("Czas trwania", Duration(media.DurationMilliseconds));
            Field("Wideo", Value(media.VideoCodec));
            Field("Rozdzielczość", media.Width is > 0 && media.Height is > 0
                ? FormattableString.Invariant($"{media.Width} × {media.Height}") : Unavailable);
            Field("Audio", Value(media.Audio));
            Field("Tryb dekodowania", Value(media.DecodingMode));
            Field("Akceleracja sprzętowa", media.IsHardwareAccelerated switch
            {
                true => "Tak",
                false => "Nie",
                null => Unavailable
            });
        }

        text.AppendLine().AppendLine("[RAPORT]");
        Field("Utworzono", snapshot.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
        Field("Strefa czasowa", Value(snapshot.TimeZoneId));
        return text.ToString();

        void Field(string label, string value) => text.Append(label).Append(": ").AppendLine(value);
    }

    private static string Clean(string? value) => (value ?? "")
        .Replace('\r', ' ').Replace('\n', ' ')
        .Replace('\u0085', ' ').Replace('\u2028', ' ').Replace('\u2029', ' ').Trim();

    private static string Value(string? value)
    {
        var cleaned = Clean(value);
        return cleaned.Length == 0 || cleaned.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            ? Unavailable : cleaned;
    }

    private static string VersionValue(string? value)
    {
        var cleaned = Clean(value);
        // A DLL location is not a version and can disclose a user's directory names.
        return cleaned.Contains('/') || cleaned.Contains('\\') ? Unavailable : Value(cleaned);
    }

    private static string FileName(string? path)
    {
        var cleaned = Clean(path).Replace('\\', '/');
        var name = cleaned[(cleaned.LastIndexOf('/') + 1)..];
        // Also handle a Windows drive-relative path when formatting on another OS.
        if (name.Length >= 2 && char.IsLetter(name[0]) && name[1] == ':')
            name = name[2..];
        return Value(name);
    }

    private static string Duration(long? milliseconds)
    {
        if (milliseconds is null or < 0) return Unavailable;
        var value = milliseconds.Value;
        // Integer arithmetic preserves milliseconds and does not wrap at 24 hours.
        return FormattableString.Invariant(
            $"{value / 3600000:00}:{value / 60000 % 60:00}:{value / 1000 % 60:00}.{value % 1000:000}");
    }
}
