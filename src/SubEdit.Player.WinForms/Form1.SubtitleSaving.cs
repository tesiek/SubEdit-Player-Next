using System.Text;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    internal const string AnsiEncodingSaveErrorMessage =
        "Tekst zawiera znaki, których nie można zapisać w kodowaniu ANSI (Windows-1250).\r\n\r\nUsuń lub zamień te znaki albo zmień kodowanie napisów na UTF-8.";

    private void SaveSubtitle()
    {
        _ = TrySaveSubtitle();
    }


    private void ConfigureAutoSaveTimer()
    {
        _autoSaveTimer.Interval = _settings.Editor.AutoSaveMinutes * 60_000;
        _autoSaveTimer.Enabled = _settings.Editor.AutoSaveEnabled;
    }

    private void TryAutoSaveSubtitle()
    {
        if (!_isSubtitleDirty || string.IsNullOrWhiteSpace(_currentSubtitlePath))
        {
            return;
        }

        _ = TrySaveSubtitleToPath(_currentSubtitlePath);
    }

    private bool TrySaveSubtitle()
    {
        if (string.IsNullOrWhiteSpace(_currentSubtitlePath))
        {
            return TrySaveSubtitleAs();
        }

        return TrySaveSubtitleToPath(_currentSubtitlePath);
    }

    private void SaveSubtitleAs()
    {
        _ = TrySaveSubtitleAs();
    }

    private void SaveSubtitleAsSrt()
    {
        var document = ParseSubtitleDocument();
        if (SrtSubtitleExporter.HasNegativeTimestamps(document))
        {
            MessageBox.Show(
                this,
                "Napisy zawierają ujemne znaczniki czasu. Standardowy format SRT nie dopuszcza wartości ujemnych.\r\n\r\nPrzesuń napisy do wartości dodatnich przed eksportem.",
                "Eksport napisów SRT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var sourcePath = _currentSubtitlePath ?? _suggestedSubtitleSavePath;
        var fileName = sourcePath is { } path
            ? Path.ChangeExtension(Path.GetFileName(path), ".srt")
            : "Napisy.srt";
        using var dialog = new SaveFileDialog
        {
            Filter = "Napisy SubRipp (SRT)|*.srt",
            DefaultExt = "srt",
            AddExtension = true,
            Title = "Zapisz plik z napisami SRT",
            FileName = fileName
        };
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(sourcePath);
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SubtitleFileWriter.Write(dialog.FileName, SrtSubtitleExporter.Export(document), new UTF8Encoding(false));
        }
        catch (EncoderFallbackException)
        {
            MessageBox.Show(
                this,
                AnsiEncodingSaveErrorMessage,
                "Nie można zapisać napisów SRT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Nie można zapisać napisów SRT", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool TrySaveSubtitleAs()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Pliki napisów|*.srt;*.sub;*.txt;*.mpl|Wszystkie pliki|*.*",
            DefaultExt = Path.GetExtension(_currentSubtitlePath ?? _suggestedSubtitleSavePath ?? "Napisy.txt").TrimStart('.'),
            AddExtension = true,
            Title = "Zapisz plik z napisami",
            FileName = _currentSubtitlePath is { } path
                ? Path.GetFileName(path)
                : _suggestedSubtitleSavePath is { } suggestedPath
                    ? Path.GetFileName(suggestedPath)
                    : "Napisy.txt"
        };
        var initialPath = _currentSubtitlePath ?? _suggestedSubtitleSavePath;
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(initialPath);
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            return TrySaveSubtitleToPath(dialog.FileName);
        }

        return false;
    }

    private bool TrySaveSubtitleToPath(string subtitlePath)
    {
        try
        {
            var encoding = _subtitleEncoding ?? new UTF8Encoding(false);
            SubtitleFileWriter.Write(subtitlePath, _subtitleEditor.Text, encoding, CreateSubtitleBackup);
            _currentSubtitlePath = subtitlePath;
            _suggestedSubtitleSavePath = null;
            _subtitleEncoding = encoding;
            _savedSubtitleEncoding = encoding;
            _settings.Playback.LastSubtitlePath = subtitlePath;
            _subtitleEditor.SetSavePoint();
            _subtitleTextDirty = false;
            _subtitleStateDirty = false;
            UpdateSubtitleDirtyState();
            return true;
        }
        catch (EncoderFallbackException)
        {
            MessageBox.Show(
                this,
                AnsiEncodingSaveErrorMessage,
                "Nie można zapisać napisów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, "Nie można zapisać napisów", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void CreateSubtitleBackup(string subtitlePath)
    {
        if (!_settings.Editor.CreateBackupFile || !File.Exists(subtitlePath))
        {
            return;
        }

        var count = _settings.Editor.BackupFileCount;
        for (var index = count; index >= 2; index--)
        {
            var source = GetSubtitleBackupPath(subtitlePath, index - 1);
            var destination = GetSubtitleBackupPath(subtitlePath, index);
            if (File.Exists(source))
            {
                File.Copy(source, destination, true);
            }
        }

        File.Copy(subtitlePath, GetSubtitleBackupPath(subtitlePath, 1), true);
    }

    private static string GetSubtitleBackupPath(string subtitlePath, int index) =>
        $"{subtitlePath}.{index:D2}.bak";

    private void UpdateSubtitleDirtyState()
    {
        _subtitleTextDirty = _subtitleEditor.Modified;
        _subtitleStateDirty = CalculateSubtitleStateDirty();
        SetSubtitleDirty(_subtitleTextDirty || _subtitleStateDirty);
    }

    private bool CalculateSubtitleStateDirty()
    {
        if (_suggestedSubtitleSavePath is not null)
        {
            return true;
        }

        return IsSubtitleEncodingDirty();
    }

    private bool IsSubtitleEncodingDirty()
    {
        if (_savedSubtitleEncoding is null)
        {
            return _subtitleEncoding is not null;
        }

        if (_subtitleEncoding is null)
        {
            return false;
        }

        return SubtitleFileReader.GetEncodingKind(_subtitleEncoding) != SubtitleFileReader.GetEncodingKind(_savedSubtitleEncoding);
    }

    private void SetSubtitleDirty(bool isDirty)
    {
        _isSubtitleDirty = isDirty;
        ToolbarButton("toolbarSave").Enabled = isDirty;
        UpdateSubtitleHeader();
    }

    private void UpdateSubtitleHeader()
    {
        _editorHeader.ForeColor = _isSubtitleDirty ? Color.Red : Color.Black;
        if (string.IsNullOrWhiteSpace(_currentSubtitlePath))
        {
            _editorHeader.Text = "Napisy.txt";
            return;
        }

        var availableWidth = Math.Max(1, _editorPanel.ClientSize.Width - _editorHeader.Padding.Horizontal - 4);
        _editorHeader.Text = FitPathToWidth(_currentSubtitlePath, _editorHeader.Font, availableWidth);
    }

    private bool ConfirmSubtitleChanges()
    {
        if (!_isSubtitleDirty)
        {
            return true;
        }

        using var dialog = new QuestionDialog(
            "Aktualny plik tekstowy jest zmodyfikowany.\r\n\r\nCzy zachować zmiany?",
            includeCancel: true);
        return dialog.ShowDialog(this) switch
        {
            DialogResult.Yes => TrySaveSubtitle(),
            DialogResult.No => true,
            _ => false
        };
    }
}