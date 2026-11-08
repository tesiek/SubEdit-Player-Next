using System.ComponentModel;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>UI #2: native RichEdit body between a fixed summary and shared actions.</summary>
public sealed class InfoDialogLayout : UserControl, IAdaptiveDialogSurface
{
    private readonly TableLayoutPanel _root = new();
    private readonly Label _summary = new();
    private InspectorContentLayout? _content;
    private (int Width, Font Font, string Text, int Dpi)? _headerKey;
    private int _headerHeight;
    private bool _arranging;

    public InfoDialogLayout()
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        _root.Dock = DockStyle.Fill;
        _root.ColumnCount = 1;
        _root.RowCount = 3;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute));
        _summary.Dock = DockStyle.Fill;
        _summary.UseMnemonic = false;
        _summary.Margin = Padding.Empty;
        Actions = new DialogActionBar { Dock = DockStyle.Fill, TabIndex = 1 };
        _root.Controls.Add(_summary, 0, 0);
        _root.Controls.Add(Actions, 0, 2);
        Controls.Add(_root);
        SizeChanged += (_, _) => ArrangeViewport();
    }

    [Browsable(false)] public DialogActionBar Actions { get; }
    [Browsable(false)] public Control View => this;
    [Browsable(false)] public Control Header => _summary;
    [Browsable(false)] public UiDesignTokens Tokens => UiDesignTokens.Current;
    [Browsable(false)] public int MinimumClientHeight => _headerHeight + Actions.Height + Tokens.Inset(UiVisualProfile.Standard, DeviceDpi) * 2 + 30;
    [DefaultValue("")] public string Summary { get => _summary.Text; set { _summary.Text = value; _headerKey = null; } }

    public void SetContent(InspectorContentLayout content)
    {
        if (_content is not null) _root.Controls.Remove(_content);
        _content = content;
        content.Margin = Padding.Empty;
        _root.Controls.Add(content, 0, 1);
    }

    public int MeasurePreferredHeight(int clientWidth) => UiDesignTokens.Pixels(620, DeviceDpi);

    private int MeasureHeader(int width)
    {
        var key = (width, Font, _summary.Text, DeviceDpi);
        if (_headerKey == key) return _headerHeight;
        _headerKey = key;
        _headerHeight = TextRenderer.MeasureText(_summary.Text, Font,
            new Size(Math.Max(1, width), int.MaxValue), TextFormatFlags.WordBreak).Height
            + UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi);
        return _headerHeight;
    }

    public void ArrangeViewport()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            var inset = Tokens.Inset(UiVisualProfile.Standard, DeviceDpi);
            _root.Padding = new Padding(inset);
            var width = Math.Max(1, ClientSize.Width - 2 * inset);
            _root.RowStyles[0].Height = MeasureHeader(width);
            var gap = UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi);
            Actions.Margin = new Padding(0, gap, 0, 0);
            _root.RowStyles[2].Height = Actions.MeasureForWidth(width) + gap;
            // Docked RichTextBox wraps natively. Resizing never rebuilds report text or per-row controls.
        }
        finally { _arranging = false; }
    }
}
