using System.Runtime.InteropServices;
using SubEdit.Player.Core.Playback;

namespace SubEdit.Player.WinForms;

internal sealed class VideoPropertiesDialog : SubEditDialog
{
    private readonly Panel _viewport;
    private readonly TableLayoutPanel _content;
    private readonly Panel _footer;
    private readonly Button _ok;
    private readonly Button _copy;
    private readonly Font _headingFont;
    private bool _layingOut;
    internal string ReportText { get; }

    public VideoPropertiesDialog(VideoFileProperties snapshot)
    {
        Text = "Właściwości pliku wideo";
        ClientSize = new Size(750, 750);
        FormBorderStyle = FormBorderStyle.Sizable;
        _headingFont = new Font(Font, FontStyle.Bold);
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        ReportText = VideoFilePropertiesFormatter.FormatText(sections);

        _viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, TabStop = true };
        _content = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2, GrowStyle = TableLayoutPanelGrowStyle.AddRows,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        _content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _content.SuspendLayout();
        foreach (var section in sections)
        {
            var heading = NewLabel(section.Heading);
            heading.Font = _headingFont;
            heading.ForeColor = SystemColors.HotTrack;
            heading.Padding = new Padding(0, 14, 0, 5);
            AddRow(heading);
            AddRow(new Label
            {
                Height = 1, Dock = DockStyle.Top, BackColor = SystemColors.ControlDark,
                Margin = new Padding(0, 0, 0, 7)
            });
            foreach (var (label, value) in section.Rows)
            {
                if (string.IsNullOrEmpty(label)) AddRow(NewLabel(value));
                else
                {
                    var row = _content.RowCount++;
                    _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    _content.Controls.Add(NewLabel(label), 0, row);
                    _content.Controls.Add(NewLabel(value), 1, row);
                }
            }
        }
        _content.ResumeLayout(false);
        _viewport.Controls.Add(_content);
        _ok = new Button { Text = "OK", DialogResult = DialogResult.OK, TabIndex = 0 };
        _copy = new Button { Text = "Kopiuj", TabIndex = 1 };
        _copy.Click += (_, _) => CopyReport();
        _footer = new Panel { Dock = DockStyle.Bottom };
        _footer.Controls.AddRange([_ok, _copy]);
        Controls.Add(_viewport);
        Controls.Add(_footer);
        AcceptButton = _ok;
        CancelButton = _ok;
        _viewport.SizeChanged += (_, _) => LayoutContent();
        _footer.SizeChanged += (_, _) => LayoutButtons();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToWorkingArea();
        LayoutButtons();
        LayoutContent();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitToWorkingArea();
        LayoutButtons();
        LayoutContent();
    }

    private void FitToWorkingArea()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MaximumSize = area.Size;
        MinimumSize = new Size(Math.Min(ScalePixels(500), area.Width), Math.Min(ScalePixels(320), area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width),
            Math.Clamp(Top, area.Top, area.Bottom - Height));
    }

    private void LayoutContent()
    {
        if (_layingOut) return;
        _layingOut = true;
        try
        {
            var padding = ScalePixels(15);
            // Reserve scrollbar space even when hidden; content cannot overflow horizontally.
            var width = Math.Max(1, _viewport.ClientSize.Width - 2 * padding - SystemInformation.VerticalScrollBarWidth);
            var firstColumn = Math.Min(ScalePixels(165), width / 2);
            _content.SuspendLayout();
            _content.Location = new Point(padding + _viewport.AutoScrollPosition.X, padding + _viewport.AutoScrollPosition.Y);
            _content.MinimumSize = new Size(width, 0);
            _content.MaximumSize = new Size(width, 0);
            _content.ColumnStyles[0].Width = firstColumn;
            foreach (Control control in _content.Controls)
            {
                if (control is not Label { AutoSize: true } label) continue;
                var columnWidth = _content.GetColumnSpan(control) == 2 ? width
                    : _content.GetColumn(control) == 0 ? firstColumn : width - firstColumn;
                label.MaximumSize = new Size(Math.Max(1, columnWidth - label.Margin.Horizontal), 0);
            }
            _content.ResumeLayout(true);
            _viewport.AutoScrollMinSize = new Size(0, _content.Height + 2 * padding);
        }
        finally { _layingOut = false; }
    }

    private void LayoutButtons()
    {
        var width = ScalePixels(84);
        var height = Math.Max(ScalePixels(27), _ok.GetPreferredSize(Size.Empty).Height);
        var gap = ScalePixels(10);
        _footer.Height = height + ScalePixels(24);
        var left = Math.Max(0, (_footer.ClientSize.Width - 2 * width - gap) / 2);
        _ok.SetBounds(left, ScalePixels(12), width, height);
        _copy.SetBounds(left + width + gap, ScalePixels(12), width, height);
    }

    private void CopyReport()
    {
        try { Clipboard.SetText(ReportText, TextDataFormat.UnicodeText); }
        catch (Exception exception) when (exception is ExternalException or ThreadStateException)
        {
            MessageBox.Show(this, "Nie można skopiować informacji do schowka. Spróbuj ponownie.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AddRow(Control control)
    {
        var row = _content.RowCount++;
        _content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _content.Controls.Add(control, 0, row);
        _content.SetColumnSpan(control, 2);
    }

    private static Label NewLabel(string text) => new()
    {
        Text = text, AutoSize = true, UseMnemonic = false,
        Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 2, 8, 4)
    };

    private int ScalePixels(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96d);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _headingFont.Dispose();
    }
}
