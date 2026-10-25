using System.Runtime.InteropServices;
using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.WinForms.Diagnostics;

namespace SubEdit.Player.WinForms;

internal sealed class ProgramDiagnosticsDialog : SubEditDialog
{
    private readonly TextBox _report;
    private readonly Action<string> _writeClipboard;
    internal string ReportText => _report.Text;
    internal Task? LoadingTask { get; private set; }

    internal ProgramDiagnosticsDialog(Func<DiagnosticsRequest> capture,
        Func<DiagnosticsRequest, Task<ProgramDiagnosticsSnapshot>> complete, Action<string>? writeClipboard = null)
    {
        Text = "Diagnostyka programu";
        ClientSize = new Size(780, 650);
        FormBorderStyle = FormBorderStyle.Sizable;
        _writeClipboard = writeClipboard ?? (text => Clipboard.SetText(text, TextDataFormat.UnicodeText));
        _report = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true,
            ShortcutsEnabled = true, Dock = DockStyle.Fill, HideSelection = false,
            Text = "Pobieranie danych systemowych...", BackColor = SystemColors.Window,
            Margin = Padding.Empty
        };
        var selectionItem = new ToolStripMenuItem("Kopiuj zaznaczenie");
        selectionItem.Click += (_, _) => CopySelection();
        var context = new ContextMenuStrip();
        context.Items.Add(selectionItem);
        context.Opening += (_, _) => selectionItem.Enabled = _report.SelectionLength > 0;
        _report.ContextMenuStrip = context;
        _report.KeyDown += (_, e) =>
        {
            if (!HandleTextShortcut(e.KeyData)) return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        };
        var copy = new Button { Text = "Kopiuj", AutoSize = true };
        copy.Click += (_, _) => CopyAll();
        var close = new Button { Text = "Zamknij", AutoSize = true };
        close.Click += (_, _) => Close();
        var buttons = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.None, WrapContents = false };
        buttons.Controls.AddRange([copy, close]);
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 48, ColumnCount = 1, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(buttons);
        Padding = new Padding(12);
        Controls.Add(_report);
        Controls.Add(footer);
        CancelButton = close;
        FormClosed += (_, _) => context.Dispose();
        // Posting from Shown lets Show() return before any snapshot or system query is started.
        Shown += (_, _) => BeginInvoke((Action)(() =>
        {
            if (!IsDisposed && !Disposing) LoadingTask = LoadReportAsync(capture, complete);
        }));
    }

    private async Task LoadReportAsync(Func<DiagnosticsRequest> capture, Func<DiagnosticsRequest, Task<ProgramDiagnosticsSnapshot>> complete)
    {
        ProgramDiagnosticsSnapshot? snapshot = null;
        try
        {
            var request = capture();
            snapshot = request.Snapshot;
            _report.Text = ProgramDiagnosticsFormatter.Format(snapshot, loadingSystem: true);
            var final = await complete(request).ConfigureAwait(false);
            Publish(ProgramDiagnosticsFormatter.Format(final));
        }
        catch (Exception)
        {
            Publish(snapshot is null ? "Dane diagnostyczne są niedostępne." : ProgramDiagnosticsFormatter.Format(snapshot));
        }
    }

    private void Publish(string text)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing) return;
                _report.Text = text;
                _report.Select(0, 0);
            }));
        }
        catch (InvalidOperationException) { /* Closed between completion and posting. */ }
    }

    internal bool HandleTextShortcut(Keys key)
    {
        if (key == (Keys.Control | Keys.A)) { _report.SelectAll(); return true; }
        if (key == (Keys.Control | Keys.C)) { CopySelection(); return true; }
        return false;
    }

    internal void CopyAll() => Copy(ReportText);
    private void CopySelection() { if (_report.SelectionLength > 0) Copy(_report.SelectedText); }

    private void Copy(string text)
    {
        try { _writeClipboard(text); }
        catch (Exception exception) when (exception is ExternalException or ThreadStateException)
        {
            MessageBox.Show(this, "Nie można skopiować informacji do schowka. Spróbuj ponownie.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(area.Width, 480 * DeviceDpi / 96), Math.Min(area.Height, 300 * DeviceDpi / 96));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width), Math.Clamp(Top, area.Top, area.Bottom - Height));
    }
}
