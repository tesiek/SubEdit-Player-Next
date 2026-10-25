using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Playback;

namespace SubEdit.Player.WinForms.Diagnostics;

internal sealed record DiagnosticsRequest(ProgramDiagnosticsSnapshot Snapshot, string? MediaPath);

internal static class ProgramDiagnosticsCollector
{
    // Exact runtime dependencies, verified against the application's deps.json; no directory enumeration.
    internal static readonly string[] ManagedLibraries =
    [
        "FlyleafLib", "Flyleaf.FFmpeg.Bindings", "Scintilla.NET", "Vortice.D3DCompiler", "Vortice.Direct3D11",
        "Vortice.DirectComposition", "Vortice.MediaFoundation", "Vortice.XAudio2", "Vortice.Mathematics",
        "Vortice.Direct2D1", "Vortice.DirectX", "Vortice.DXGI", "SharpGen.Runtime", "SharpGen.Runtime.COM"
    ];

    internal static DiagnosticsRequest Capture(IPlaybackEngine playback, string? path, string decodingMode,
        string? scintillaVersion, string? lexillaVersion)
    {
        var createdAt = DateTimeOffset.Now;
        var build = ApplicationBuildInfo.Current;
        var program = new DiagnosticProgramInfo(
            typeof(ApplicationBuildInfo).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product,
            build.ProductVersion, build.BuildDate, build.BuildCommit, build.BuildDirtyState,
            RuntimeInformation.ProcessArchitecture.ToString(), RuntimeInformation.FrameworkDescription);
        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        var libraries = ManagedLibraries.Select(name =>
        {
            var assembly = loaded.FirstOrDefault(value => value.GetName().Name == name);
            if (assembly is null) return new DiagnosticLibrary(name, null, "niezaładowana");
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            var assemblyVersion = assembly.GetName().Version?.ToString();
            return new DiagnosticLibrary(name, version ?? assemblyVersion, $"załadowana; AssemblyVersion {assemblyVersion}");
        }).ToList();
        libraries.AddRange(PlaybackDiagnostics.CaptureFfmpegVersions());
        libraries.Add(new("Scintilla.dll", scintillaVersion, "native; wrapper"));
        libraries.Add(new("Lexilla.dll", lexillaVersion, "native; wrapper"));
        var media = PlaybackDiagnostics.Capture(playback, path, decodingMode);
        return new(new(program, libraries, media, createdAt, TimeZoneInfo.Local.Id), media.IsOpen ? path : null);
    }

    internal static async Task<ProgramDiagnosticsSnapshot> CompleteAsync(DiagnosticsRequest request, SystemDiagnosticsCache systemCache)
    {
        var systemTask = systemCache.GetAsync();
        // Even FileInfo and PE version-resource reads are kept off the UI thread.
        var filesTask = Task.Run(() =>
        {
            var size = request.MediaPath is null ? null : MediaInspector.TryGetFileSizeBytes(request.MediaPath);
            return (Size: size, Libraries: ReadKnownLibraryFiles(request.Snapshot.Libraries));
        });
        await Task.WhenAll(systemTask, filesTask).ConfigureAwait(false);
        var files = await filesTask.ConfigureAwait(false);
        return new(request.Snapshot.Program, files.Libraries,
            request.Snapshot.Media with { FileSizeBytes = files.Size },
            request.Snapshot.CreatedAt, request.Snapshot.TimeZoneId, await systemTask.ConfigureAwait(false));
    }

    private static IReadOnlyList<DiagnosticLibrary> ReadKnownLibraryFiles(IReadOnlyList<DiagnosticLibrary> libraries)
    {
        var nativePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
                if (module.ModuleName is "Scintilla.dll" or "Lexilla.dll") nativePaths[module.ModuleName] = module.FileName;
        }
        catch (Exception) { /* Known deployment paths remain available as a labelled fallback. */ }
        var result = new List<DiagnosticLibrary>();
        foreach (var library in libraries)
        {
            var native = library.Name is "Scintilla.dll" or "Lexilla.dll";
            if (!native && library.Version is not null) { result.Add(library); continue; }
            if (!native && !ManagedLibraries.Contains(library.Name)) { result.Add(library); continue; }
            try
            {
                var isLoaded = nativePaths.TryGetValue(library.Name, out var loadedPath);
                var path = isLoaded ? loadedPath! : native
                    ? Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", library.Name)
                    : Path.Combine(AppContext.BaseDirectory, library.Name + ".dll");
                var info = FileVersionInfo.GetVersionInfo(path);
                var version = !string.IsNullOrWhiteSpace(info.ProductVersion) ? info.ProductVersion : info.FileVersion;
                result.Add(new(library.Name, version, isLoaded ? "załadowana; metadane modułu" : "dostarczona; metadane pliku"));
            }
            catch (Exception) { result.Add(library); }
        }
        return result.AsReadOnly();
    }
}
