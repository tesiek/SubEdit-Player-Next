using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

public interface IDialogContent
{
    Control View { get; }
    int MeasureForWidth(int width);
    int MeasureHeightForWidth(int width) => MeasureForWidth(width);
    int MinimumContentWidth { get; }
    event EventHandler? MetricsChanged;
}

public sealed class DialogShell : UserControl, IAdaptiveDialogSurface, IDialogMinimumMetrics
{
    private readonly TableLayoutPanel _root = new();
    private readonly VerticalContentViewport _viewport = new();
    private IDialogContent? _content;
    private bool _arranging;

    public DialogShell()
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        _root.Dock = DockStyle.Fill;
        _root.Margin = Padding.Empty;
        _root.ColumnCount = 1;
        _root.RowCount = 2;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute));
        _viewport.Dock = DockStyle.Fill;
        _viewport.Margin = Padding.Empty;
        _viewport.TabIndex = 0;
        Actions = new DialogActionBar { Dock = DockStyle.Fill, TabIndex = 1, UseNativeButtonMetrics = true };
        _root.Controls.Add(_viewport, 0, 0);
        _root.Controls.Add(Actions, 0, 1);
        Controls.Add(_root);
        SizeChanged += (_, _) => ArrangeViewport();
    }

    [Browsable(false)]
    public DialogActionBar Actions { get; }
    [Browsable(false)]
    public VerticalContentViewport Viewport => _viewport;
    [Browsable(false)]
    public Control View => this;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public UiDesignTokens Tokens { get; set; } = UiDesignTokens.Current;
    [DefaultValue(UiVisualProfile.Standard)]
    public UiVisualProfile Profile { get; set; }
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IDialogContent? Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            if (_content is not null) _content.MetricsChanged -= OnContentMetricsChanged;
            _content = value;
            if (_content is not null) _content.MetricsChanged += OnContentMetricsChanged;
            if (value is SettingsPageLayout page) page.Tokens = Tokens;
            _viewport.SetContent(value);
            ArrangeViewport();
            OnContentMetricsChanged(this, EventArgs.Empty);
        }
    }

    [Browsable(false)]
    public int PreferredBodyHeight { get; private set; }

    [Browsable(false)]
    public int MinimumClientHeight => Actions.Height + 2 * Tokens.Inset(Profile, DeviceDpi)
        + UiDesignTokens.Pixels(Tokens.SectionGap + 16, DeviceDpi);

    public event EventHandler? MetricsChanged;
    private void OnContentMetricsChanged(object? sender, EventArgs e) => MetricsChanged?.Invoke(this, EventArgs.Empty);

    [Browsable(false)]
    public int MinimumClientWidth => 2 * Tokens.Inset(Profile, DeviceDpi) + Math.Max(
        (_content?.MinimumContentWidth ?? 0) + GetSystemMetricsForDpi(2, (uint)DeviceDpi)
            + UiDesignTokens.Pixels(Tokens.ScrollbarGap, DeviceDpi),
        Actions.Buttons.Values.Select(button => TextRenderer.MeasureText(button.Text, button.Font).Width
            + button.Padding.Horizontal + UiDesignTokens.Pixels(Tokens.ActionGap * 2, DeviceDpi)).DefaultIfEmpty(0).Max());

    private int ContentWidth(int clientWidth) => Math.Max(1, clientWidth - 2 * Tokens.Inset(Profile, DeviceDpi));

    public int MeasurePreferredHeight(int clientWidth)
    {
        var inset = Tokens.Inset(Profile, DeviceDpi);
        var actionsHeight = Actions.MeasureForWidth(Math.Max(1, clientWidth - inset * 2));
        return (_content?.MeasureHeightForWidth(ContentWidth(clientWidth)) ?? 0) + inset * 2
            + UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi) + actionsHeight;
    }

    public void ArrangeViewport()
    {
        if (_arranging || IsDisposed) return;
        _arranging = true;
        try
        {
            var inset = Tokens.Inset(Profile, DeviceDpi);
            _root.Padding = new Padding(inset);
            Actions.Tokens = Tokens;
            var gap = UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi);
            Actions.Margin = new Padding(0, gap, 0, 0);
            var actionHeight = Actions.MeasureForWidth(Math.Max(1, ClientSize.Width - inset * 2));
            _root.RowStyles[1].Height = actionHeight + gap;
            _root.PerformLayout();
            if (_content is null) return;
            _viewport.ArrangeContent();
            PreferredBodyHeight = _viewport.ContentHeight;
        }
        finally { _arranging = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _content is not null) _content.MetricsChanged -= OnContentMetricsChanged;
        base.Dispose(disposing);
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int index, uint dpi);

}
