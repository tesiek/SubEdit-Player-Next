using System.Text;

namespace SubEdit.Player.WinForms;

internal static class SubtitleFileWriter
{
    public static void Write(
        string destinationPath,
        string text,
        Encoding encoding,
        Action<string>? beforeReplace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);

        _ = encoding.GetByteCount(text);

        var destinationFullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destinationFullPath);
        if (string.IsNullOrEmpty(directory))
        {
            directory = AppContext.BaseDirectory;
        }

        Directory.CreateDirectory(directory);

        var fileName = Path.GetFileName(destinationFullPath);
        var tempPath = Path.Combine(directory, $"{fileName}.{Path.GetRandomFileName()}.tmp");

        try
        {
            File.WriteAllText(tempPath, text, encoding);

            if (File.Exists(destinationFullPath) && beforeReplace is not null)
            {
                beforeReplace(destinationFullPath);
            }

            if (File.Exists(destinationFullPath))
            {
                try
                {
                    File.Replace(tempPath, destinationFullPath, null, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(tempPath, destinationFullPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempPath, destinationFullPath);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup: do not mask the primary exception.
                }
            }
        }
    }
}
