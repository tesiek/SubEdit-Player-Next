using System.Text;
using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void MergeSubtitles()
    {
        if (!_playback.IsMediaOpen || _isStopped || _playback.State != PlaybackState.Paused)
        {
            MessageBox.Show(
                this,
                "Film musi być zatrzymany (Pauza) na początku dowolnego dialogu z następnego pliku napisów.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var positionMilliseconds = _playback.PositionMilliseconds;

        using var dialog = new OpenFileDialog
        {
            Title = "Dołącz plik napisów (film w jednej części)",
            Filter = "Pliki napisów (*.srt;*.sub;*.mpl;*.txt)|*.srt;*.sub;*.mpl;*.txt|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true
        };

        var initialDirectory = !string.IsNullOrWhiteSpace(_currentSubtitlePath) && Directory.Exists(Path.GetDirectoryName(_currentSubtitlePath))
            ? Path.GetDirectoryName(_currentSubtitlePath)
            : !string.IsNullOrWhiteSpace(_currentVideoPath) && Directory.Exists(Path.GetDirectoryName(_currentVideoPath))
                ? Path.GetDirectoryName(_currentVideoPath)
                : null;

        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentSubtitlePath) &&
            string.Equals(
                Path.GetFullPath(dialog.FileName),
                Path.GetFullPath(_currentSubtitlePath),
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "Nie można dołączyć aktualnie edytowanego pliku napisów do niego samego.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        SubtitleFileContent importedFile;
        try
        {
            importedFile = SubtitleFileReader.Read(dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Błąd",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        var activeText = _subtitleEditor.Text;
        var activeFormat = SubtitleMergeProcessor.DetectLogicalFormat(activeText);
        if (activeFormat is null)
        {
            MessageBox.Show(
                this,
                "Nie udało się rozpoznać formatu aktualnie edytowanych napisów.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var importedFormat = SubtitleMergeProcessor.DetectLogicalFormat(importedFile.Text);
        if (importedFormat is null)
        {
            MessageBox.Show(
                this,
                "Nie udało się rozpoznać formatu dołączanego pliku napisów.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (activeFormat != importedFormat)
        {
            MessageBox.Show(
                this,
                $"Format dołączanego pliku napisów ({GetFormatDisplayName(importedFormat.Value)}) różni się od formatu aktualnego dokumentu ({GetFormatDisplayName(activeFormat.Value)}).\n\nNajpierw przekonwertuj plik lub wybierz napisy w tym samym formacie.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var parseResult = SubtitleMergeProcessor.ParseForMerge(importedFile.Text, importedFormat.Value);
        if (parseResult.Cues.Count == 0)
        {
            MessageBox.Show(
                this,
                "Brak poprawnych napisów do dołączenia w wybranym pliku.",
                "Informacja",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var lineDialog = new MergeSubtitleSelectLineDialog(
            parseResult.DisplayItems,
            _rememberedSubtitleTimeScaleTransform.HasValue);

        if (lineDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var selectedCueIndex = lineDialog.SelectedCueIndex;
        var ignoreScale = lineDialog.IgnoreRememberedScale;
        double? scale = (!ignoreScale && _rememberedSubtitleTimeScaleTransform.HasValue)
            ? _rememberedSubtitleTimeScaleTransform.Value.Scale
            : null;

        var framesPerSecond = GetVideoFrameRate();
        var mergeResult = SubtitleMergeProcessor.Merge(
            activeText,
            importedFile.Text,
            activeFormat.Value,
            selectedCueIndex,
            positionMilliseconds,
            scale,
            framesPerSecond);

        var viewState = _subtitleEditor.CaptureViewState();
        _subtitleEditor.ApplyFullDocumentEdit(mergeResult.MergedText, viewState);
        RefreshSubtitleDocument();
        RefreshSubtitleInfoBar();

        if (ignoreScale && _rememberedSubtitleTimeScaleTransform.HasValue)
        {
            ClearRememberedSubtitleTimeScaleTransform();
        }
    }

    private bool CanMergeSubtitles()
    {
        return _playback.IsMediaOpen && !string.IsNullOrWhiteSpace(_subtitleEditor.Text);
    }

    private void UpdateMergeSubtitleAvailability()
    {
        if (_mergeSubtitlesMenuItem is not null)
        {
            _mergeSubtitlesMenuItem.Enabled = CanMergeSubtitles();
        }
    }

    private static string GetFormatDisplayName(SubtitleLogicalFormat format) => format switch
    {
        SubtitleLogicalFormat.Srt => "SubRip",
        SubtitleLogicalFormat.MicroDvd => "MicroDVD",
        SubtitleLogicalFormat.Mpl2 => "MPL2",
        SubtitleLogicalFormat.TmPlayer => "TMPlayer",
        _ => format.ToString()
    };
}
