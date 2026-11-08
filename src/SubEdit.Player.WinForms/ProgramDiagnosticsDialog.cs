using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms.Diagnostics;

namespace SubEdit.Player.WinForms;

internal sealed class ProgramDiagnosticsDialog : InspectorDialog
{
    private readonly CancellationTokenSource _lifetime = new();
    internal Task? LoadingTask { get; private set; }

    internal ProgramDiagnosticsDialog(Func<DiagnosticsRequest> capture,
        Func<DiagnosticsRequest, Task<ProgramDiagnosticsSnapshot>> complete, Action<string>? writeClipboard = null)
        : base("Diagnostyka programu", "Informacje o programie, bibliotekach, systemie i aktualnym filmie", writeClipboard)
    {
        SetReport("Pobieranie danych systemowych...", [new("RAPORT", [new("", "Pobieranie danych systemowych...")])]);
        Shown += (_, _) => BeginInvoke((Action)(() =>
        {
            if (!IsDisposed && !Disposing) LoadingTask = LoadReportAsync(capture, complete);
        }));
    }

    private async Task LoadReportAsync(Func<DiagnosticsRequest> capture, Func<DiagnosticsRequest, Task<ProgramDiagnosticsSnapshot>> complete)
    {
        var cancellation = _lifetime.Token;
        ProgramDiagnosticsSnapshot? snapshot = null;
        try
        {
            var request = capture();
            snapshot = request.Snapshot;
            ShowSnapshot(snapshot, loading: true);
            // Cancel only this dialog's wait, never the shared system diagnostics operation.
            var final = await complete(request).WaitAsync(cancellation).ConfigureAwait(false);
            Publish(() => ShowSnapshot(final));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            Publish(() =>
            {
                if (snapshot is not null) ShowSnapshot(snapshot);
                else SetReport("Dane diagnostyczne są niedostępne.", [new("RAPORT", [new("", "Dane diagnostyczne są niedostępne.")])]);
            });
        }
    }

    private void ShowSnapshot(ProgramDiagnosticsSnapshot snapshot, bool loading = false)
    {
        SetReport(ProgramDiagnosticsFormatter.Format(snapshot, loading), ProgramDiagnosticsFormatter.GetSections(snapshot, loading)
            .Select(section => new InspectorSection(section.Heading, section.Rows.Select(row => new InspectorValue(row.Label, row.Value)).ToArray())).ToArray());
    }

    private void Publish(Action update)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing || _lifetime.IsCancellationRequested) return;
                update();
                ReportContent.Report.Select(0, 0);
            }));
        }
        catch (InvalidOperationException) { /* Closed between completion and posting. */ }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _lifetime.Cancel();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed) _lifetime.Cancel();
        base.Dispose(disposing);
        if (disposing) _lifetime.Dispose();
    }
}
