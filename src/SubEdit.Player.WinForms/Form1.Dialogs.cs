using System.Diagnostics;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Diagnostics;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private ProgramDiagnosticsDialog? _diagnosticsDialog;
    private SystemDiagnosticsCache? _diagnosticsSystemCache;

    private void ShowProgramDiagnostics()
    {
        if (_diagnosticsDialog is { IsDisposed: false } existing)
        {
            if (existing.WindowState == FormWindowState.Minimized) existing.WindowState = FormWindowState.Normal;
            existing.Activate();
            return;
        }
        var dialog = new ProgramDiagnosticsDialog(
            () => ProgramDiagnosticsCollector.Capture(_playback, _currentVideoPath,
                _settings.Playback.VideoDecodingMode.ToString(), _subtitleEditor.ScintillaVersion, _subtitleEditor.LexillaVersion),
            request => ProgramDiagnosticsCollector.CompleteAsync(request, _diagnosticsSystemCache ??= new()));
        _diagnosticsDialog = dialog;
        dialog.FormClosed += (_, _) => _diagnosticsDialog = null;
        dialog.Show(this);
    }

    private void ShowAboutDialog()
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }

    private void OpenSubEditWebsite() => OpenWebsite("https://subedit.com.pl/");

    private void OpenWebsite(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Nie można otworzyć odnośnika",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OpenHelpDocument(string fileName)
    {
        var documentPath = Path.Combine(AppContext.BaseDirectory, "doc", fileName);
        try
        {
            if (!File.Exists(documentPath))
            {
                throw new FileNotFoundException("Nie znaleziono dokumentu pomocy.", documentPath);
            }

            Process.Start(new ProcessStartInfo(documentPath) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Nie można otworzyć dokumentu pomocy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ShowVideoProperties()
    {
        var videoPath = _currentVideoPath;
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            MessageBox.Show(this, "Najpierw otwórz plik wideo.", "Właściwości pliku wideo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var snapshot = _playback.GetVideoFileProperties();
        if (snapshot is null)
        {
            MessageBox.Show(this, "Metadane pliku nie są jeszcze dostępne.", "Właściwości pliku wideo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new VideoPropertiesDialog(snapshot);
        dialog.ShowDialog(this);
    }
}
