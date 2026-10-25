using System.Text;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private void SplitSubtitles()
    {
        if (!CanSplitSubtitles())
        {
            return;
        }

        var durationMilliseconds = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        var framesPerSecond = GetVideoFrameRate();
        var text = _subtitleEditor.Text;

        var splitResult = SubtitleSplitProcessor.Split(text, durationMilliseconds, framesPerSecond);
        if (splitResult.RemainderCueCount == 0)
        {
            return;
        }

        var defaultFileName = GetDefaultSplitFirstPartFileName(_currentSubtitlePath, _currentVideoPath, text);

        using var dialog = new SaveFileDialog
        {
            Title = "Zapisz pierwszą część pliku z napisami",
            Filter = "Pliki napisów (*.srt;*.sub;*.txt;*.mpl)|*.srt;*.sub;*.txt;*.mpl|Wszystkie pliki (*.*)|*.*",
            FileName = defaultFileName,
            DefaultExt = Path.GetExtension(defaultFileName).TrimStart('.'),
            AddExtension = true
        };

        var initialDirectory = !string.IsNullOrWhiteSpace(_currentSubtitlePath)
            ? Path.GetDirectoryName(_currentSubtitlePath)
            : !string.IsNullOrWhiteSpace(_currentVideoPath)
                ? Path.GetDirectoryName(_currentVideoPath)
                : null;
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var encoding = _subtitleEncoding ?? new UTF8Encoding(false);
            SubtitleFileWriter.Write(dialog.FileName, splitResult.FirstPartText, encoding, CreateSubtitleBackup);
        }
        catch (EncoderFallbackException)
        {
            MessageBox.Show(
                this,
                AnsiEncodingSaveErrorMessage,
                "Nie można zapisać pierwszej części napisów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Nie można zapisać pierwszej części napisów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Remainder staje się nowym/niezapisanym dokumentem, odłączonym od oryginalnego pliku:
        _currentSubtitlePath = null;
        _suggestedSubtitleSavePath = GetSuggestedRemainderPath(dialog.FileName);

        var viewState = _subtitleEditor.CaptureViewState();
        _subtitleEditor.ApplyFullDocumentEdit(splitResult.RemainderText, viewState with { Line = 0, Column = 0 });
        ClearRememberedSubtitleTimeScaleTransform();
        UpdateSubtitleDirtyState();
        RefreshSubtitleDocument();
        RefreshSubtitleInfoBar();
        UpdateSubtitleConversionAvailability();
        _subtitleEditor.Focus();
        ShowTemporaryFileStatus($"Podzielono napisy: zapisano {splitResult.FirstPartCueCount} do pierwszej części, pozostało {splitResult.RemainderCueCount}");
    }

    private bool CanSplitSubtitles()
    {
        if (!_playback.IsMediaOpen || string.IsNullOrWhiteSpace(_currentVideoPath))
        {
            return false;
        }

        var durationMilliseconds = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (durationMilliseconds <= 0)
        {
            return false;
        }

        var text = _subtitleEditor.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var framesPerSecond = GetVideoFrameRate();
        return SubtitleSplitProcessor.CanSplit(text, durationMilliseconds, framesPerSecond);
    }

    private void UpdateSplitSubtitleAvailability()
    {
        if (_splitSubtitlesMenuItem is not null)
        {
            _splitSubtitlesMenuItem.Enabled = CanSplitSubtitles();
        }
    }

    private static string GetDefaultSplitFirstPartFileName(
        string? currentSubtitlePath,
        string? currentVideoPath,
        string text)
    {
        if (!string.IsNullOrWhiteSpace(currentSubtitlePath))
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(currentSubtitlePath);
            var extension = Path.GetExtension(currentSubtitlePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = DetectSubtitleExtension(text);
            }

            if (fileNameWithoutExt.Contains("cd", StringComparison.OrdinalIgnoreCase) ||
                fileNameWithoutExt.Contains("part", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(currentSubtitlePath);
            }

            return $"{fileNameWithoutExt}_CD1{extension}";
        }

        var baseName = !string.IsNullOrWhiteSpace(currentVideoPath)
            ? Path.GetFileNameWithoutExtension(currentVideoPath)
            : "Napisy";

        var subtitleExt = DetectSubtitleExtension(text);

        if (baseName.Contains("cd", StringComparison.OrdinalIgnoreCase) ||
            baseName.Contains("part", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseName}{subtitleExt}";
        }

        return $"{baseName}_CD1{subtitleExt}";
    }

    private static string DetectSubtitleExtension(string text)
    {
        if (SubtitleFormatConverter.IsSrtDocument(text))
        {
            return ".srt";
        }

        return SubtitleTimingFormatter.Detect(text) switch
        {
            SubtitleTimingFormat.MicroDvd => ".sub",
            SubtitleTimingFormat.Mpl2 => ".mpl",
            SubtitleTimingFormat.TmPlayerLong or SubtitleTimingFormat.TmPlayerShort => ".txt",
            _ => ".txt"
        };
    }

    internal static string GetSuggestedRemainderPath(string savedFirstPartPath) =>
        SubtitleSplitProcessor.GetSuggestedRemainderPath(savedFirstPartPath);
}
