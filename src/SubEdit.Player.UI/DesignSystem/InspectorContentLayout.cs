using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

public sealed record InspectorValue(string Name, string Value);
public sealed record InspectorSection(string Title, IReadOnlyList<InspectorValue> Values);

/// <summary>One native, selectable, read-only report. RichEdit owns wrapping and scrolling.</summary>
public sealed class InspectorContentLayout : UserControl
{
    private readonly InspectorRichTextBox _report = new();
    private IReadOnlyList<InspectorSection> _sections;
    private readonly ContextMenuStrip _menu = new();
    private string? _formatKey;
    private bool _formatPending;
    private bool _formatting;

    public InspectorContentLayout() : this([]) { }
    public InspectorContentLayout(IReadOnlyList<InspectorSection> sections)
    {
        _sections = sections;
        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        _report.Dock = DockStyle.Fill;
        _report.BorderStyle = BorderStyle.None;
        _report.BackColor = UiDesignTokens.Current.InspectorBackground;
        _report.ForeColor = UiDesignTokens.Current.Text;
        _report.ReadOnly = true;
        _report.WordWrap = true;
        _report.ScrollBars = RichTextBoxScrollBars.Vertical;
        _report.DetectUrls = false;
        _report.HideSelection = false;
        _report.ShortcutsEnabled = true;
        _report.AccessibleName = "Treść raportu";
        _report.KeyDown += (_, e) =>
        {
            if (!HandleShortcut(e.KeyData)) return;
            e.Handled = e.SuppressKeyPress = true;
        };
        var copy = _menu.Items.Add("Kopiuj zaznaczenie", null, (_, _) => CopySelection());
        _menu.Items.Add("Zaznacz wszystko", null, (_, _) => _report.SelectAll());
        _menu.Opening += (_, _) => copy.Enabled = _report.SelectionLength > 0;
        _report.ContextMenuStrip = _menu;
        Controls.Add(_report);
        FontChanged += (_, _) => RequestFormat();
        DpiChangedAfterParent += (_, _) => RequestFormat();
        _report.HandleCreated += (_, _) => RequestFormat();
        _report.FontChanged += (_, _) => RequestFormat();
        _report.ForeColorChanged += (_, _) => RequestFormat();
    }

    [Browsable(false)]
    public RichTextBox Report => _report;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<string> CopyText { get; set; } = text => Clipboard.SetText(text);

    /// <summary>Canonical production text; selection copies exactly the selected text.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string>? WholeReportText { get; set; }

    public void SetSections(IReadOnlyList<InspectorSection> sections)
    {
        _sections = sections;
        RequestFormat();
    }


    public string GetReportText() => _report.Text;
    public void CopySelection() { if (_report.SelectionLength > 0) CopySafely(_report.SelectedText); }
    public void CopyAll() => CopySafely(WholeReportText?.Invoke() ?? GetReportText());
    private void CopySafely(string text)
    {
        try { CopyText(text); }
        catch (Exception error) when (error is ExternalException or ThreadStateException)
        {
            MessageBox.Show(FindForm(), "Nie można skopiować informacji do schowka. Spróbuj ponownie.",
                "Kopiowanie raportu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    public bool HandleShortcut(Keys key)
    {
        if (key == (Keys.Control | Keys.A)) { _report.SelectAll(); return true; }
        if (key == (Keys.Control | Keys.C)) { CopySelection(); return true; }
        return false;
    }
    public void ScrollToEnd() => SendMessage(_report.Handle, 0x0115, new IntPtr(7), IntPtr.Zero);

    private void FormatReport()
    {
        // RTF \fs is in absolute half-points, unlike WinForms' effective Font after a DPI transition.
        var logicalFont = UiFontOwner.LogicalFont(this);
        var key = $"{logicalFont.Name}|{logicalFont.SizeInPoints}|{DeviceDpi}";
        if (_formatKey == key) return;
        _formatKey = key;
        _formatting = true;
        try
        {
            var start = _report.SelectionStart;
            var length = _report.SelectionLength;
            var rtf = InspectorReportFormatter.Format(_sections, logicalFont, UiDesignTokens.Current);
            _report.ApplyDpiZoom();
            _report.Rtf = rtf;
            _report.ApplyDpiZoom();
            _report.Select(Math.Min(start, _report.TextLength), Math.Min(length, Math.Max(0, _report.TextLength - start)));
            _menu.Font = Font;
        }
        finally { _formatting = false; }
    }

    private void RequestFormat()
    {
        if (_formatting) return;
        _formatKey = null;
        if (_formatPending || !_report.IsHandleCreated || IsDisposed) return;
        _formatPending = true;
        // Native Font/ForeColor propagation can replace character formatting.
        // Apply RTF once after that lifecycle step, never in Resize/Paint.
        _report.BeginInvoke((Action)(() =>
        {
            _formatPending = false;
            if (!IsDisposed && !Disposing) FormatReport();
        }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu.Dispose();
        base.Dispose(disposing);
    }
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
