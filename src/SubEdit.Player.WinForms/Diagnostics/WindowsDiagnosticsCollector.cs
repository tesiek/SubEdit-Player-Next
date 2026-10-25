using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SubEdit.Player.Core.Diagnostics;

namespace SubEdit.Player.WinForms.Diagnostics;

internal static class WindowsDiagnosticsCollector
{
    // Called only by a diagnostics request, on a worker thread. No startup initialization.
    internal static DiagnosticSystemInfo Read()
    {
        var version = Environment.OSVersion.Version;
        var cpu = TryRead(() =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return (key?.GetValue("ProcessorNameString") as string)?.Trim();
        });
        var memory = TryRead<ulong?>(() => GetPhysicallyInstalledSystemMemory(out var kibibytes) && kibibytes > 0
            ? checked(kibibytes * 1024) : null);
        string? windows = null;
        var gpus = new List<DiagnosticGpu>();
        object? locator = null, services = null;
        try
        {
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is not null) locator = Activator.CreateInstance(type);
            if (locator is not null)
            {
                services = ((dynamic)locator).ConnectServer(".", @"root\cimv2");
                // Caption is queried because Registry ProductName can still say Windows 10 on Windows 11.
                TryRead(() => Query(services!, "SELECT Caption FROM Win32_OperatingSystem", row =>
                    windows = ReadProperty(row, "Caption")));
                var detected = new List<DiagnosticGpu>();
                if (TryRead(() => Query(services!, "SELECT Name, DriverVersion FROM Win32_VideoController", row =>
                    detected.Add(new(ReadProperty(row, "Name"), ReadProperty(row, "DriverVersion"))))))
                    gpus.AddRange(detected);
            }
        }
        catch (Exception) { /* Missing/restricted WMI must not discard other sources. */ }
        finally { Release(services); Release(locator); }
        return new(windows, version.ToString(), version.Build.ToString(System.Globalization.CultureInfo.InvariantCulture), cpu, memory, gpus);
    }

    private static string? ReadProperty(object row, string name)
    {
        object? properties = null, property = null;
        try
        {
            properties = ((dynamic)row).Properties_;
            property = ((dynamic)properties).Item(name);
            return Convert.ToString(((dynamic)property).Value, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
        }
        catch (Exception) { return null; }
        finally { Release(property); Release(properties); }
    }

    private static bool Query(object services, string query, Action<object> read)
    {
        object? rows = null;
        try
        {
            // Keep the enumerator resettable: ForwardOnly fails through .NET's COM IEnumerable adapter.
            rows = ((dynamic)services).ExecQuery(query, "WQL", 16); // ReturnImmediately.
            foreach (object row in (IEnumerable)rows)
            {
                try { read(row); }
                finally { Release(row); }
            }
            return true;
        }
        finally { Release(rows); }
    }

    private static T? TryRead<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception) { return default; }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}

/// <summary>One shared in-flight query; only complete hardware results survive subsequent requests.</summary>
internal sealed class SystemDiagnosticsCache(Func<DiagnosticSystemInfo>? reader = null)
{
    private readonly object _gate = new();
    private readonly Func<DiagnosticSystemInfo> _reader = reader ?? WindowsDiagnosticsCollector.Read;
    private DiagnosticSystemInfo? _cached;
    private Task<DiagnosticSystemInfo>? _pending;

    internal Task<DiagnosticSystemInfo> GetAsync()
    {
        lock (_gate)
        {
            if (_cached is not null) return Task.FromResult(_cached);
            return _pending ??= Task.Run(Read);
        }
    }

    private DiagnosticSystemInfo Read()
    {
        DiagnosticSystemInfo result;
        try { result = _reader(); }
        catch (Exception) { result = new(); }
        lock (_gate)
        {
            if (IsComplete(result)) _cached = result;
            _pending = null;
        }
        return result;
    }

    private static bool IsComplete(DiagnosticSystemInfo result) =>
        !string.IsNullOrWhiteSpace(result.Windows) && !string.IsNullOrWhiteSpace(result.Version) &&
        !string.IsNullOrWhiteSpace(result.Build) && !string.IsNullOrWhiteSpace(result.Cpu) && result.InstalledMemoryBytes is > 0 &&
        result.Gpus.Count > 0 && result.Gpus.All(gpu => !string.IsNullOrWhiteSpace(gpu.Name) && !string.IsNullOrWhiteSpace(gpu.DriverVersion));
}
