using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Width-constrained content with one native vertical scrollbar and a clipped viewport.</summary>
public sealed class VerticalContentViewport : UserControl
{
    private readonly TableLayoutPanel _root = new();
    private readonly Panel _clip = new();
    private readonly VScrollBar _scroll = new();
    private IDialogContent? _content;
    private bool _arranging;
    private readonly bool _arrangeFullWidthFirst;

    public VerticalContentViewport() : this(false) { }

    internal VerticalContentViewport(bool arrangeFullWidthFirst)
    {
        _arrangeFullWidthFirst = arrangeFullWidthFirst;
        AutoScaleMode = AutoScaleMode.Inherit;
        Margin = Padding.Empty;
        _root.Dock = DockStyle.Fill;
        _root.Margin = Padding.Empty;
        _root.ColumnCount = 2;
        _root.RowCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _clip.Dock = DockStyle.Fill;
        _clip.Margin = Padding.Empty;
        _scroll.Dock = DockStyle.Fill;
        _scroll.Margin = Padding.Empty;
        _scroll.Visible = false;
        _scroll.ValueChanged += (_, _) => PositionContent();
        _root.Controls.Add(_clip, 0, 0);
        _root.Controls.Add(_scroll, 1, 0);
        Controls.Add(_root);
    }

    [Browsable(false)]
    public bool IsScrollVisible => _scroll.Visible;
    [Browsable(false)]
    public Size AvailableContentSize => _clip.ClientSize;
    [Browsable(false)]
    public int ContentHeight { get; private set; }
    [Browsable(false)]
    public int ScrollOffset => _scroll.Value;
    [Browsable(false)]
    public Rectangle ScrollbarBounds => RectangleToClient(_scroll.RectangleToScreen(_scroll.ClientRectangle));

    public void SetContent(IDialogContent? content)
    {
        if (_content is not null)
        {
            Watch(_content.View, false);
            _clip.Controls.Remove(_content.View);
        }
        _content = content;
        if (content is not null)
        {
            _clip.Controls.Add(content.View);
            Watch(content.View, true);
        }
    }

    public void ArrangeContent()
    {
        if (_arranging || _content is null) return;
        _arranging = true;
        try
        {
            var fullWidth = Math.Max(1, ClientSize.Width);
            ContentHeight = _arrangeFullWidthFirst
                ? _content.MeasureForWidth(fullWidth)
                : _content.MeasureHeightForWidth(fullWidth);
            var needsScroll = ContentHeight > ClientSize.Height;
            var barWidth = needsScroll ? GetSystemMetricsForDpi(2, (uint)DeviceDpi) : 0;
            var gap = needsScroll ? UiDesignTokens.Pixels(UiDesignTokens.Current.ScrollbarGap, DeviceDpi) : 0;
            if (!_arrangeFullWidthFirst || needsScroll)
                ContentHeight = _content.MeasureForWidth(Math.Max(1, fullWidth - barWidth - gap));
            _root.ColumnStyles[1].Width = barWidth + gap;
            _scroll.Margin = new Padding(gap, 0, 0, 0);
            _scroll.Visible = needsScroll;
            _root.PerformLayout();
            _scroll.LargeChange = Math.Max(1, ClientSize.Height);
            _scroll.SmallChange = Math.Max(1, Font.Height);
            _scroll.Maximum = Math.Max(0, ContentHeight - 1);
            ScrollTo(needsScroll ? _scroll.Value : 0);
        }
        finally { _arranging = false; }
    }

    public void ScrollTo(int offset)
    {
        _scroll.Value = Math.Clamp(offset, 0, Math.Max(0, ContentHeight - ClientSize.Height));
        PositionContent();
    }

    private void PositionContent()
    {
        if (_content is not null) _content.View.Location = new Point(0, -_scroll.Value);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ApplyMouseWheel(e);
        base.OnMouseWheel(e);
    }

    private void OnContentWheel(object? sender, MouseEventArgs e)
    {
        if (sender is ListControl) return; // Native ComboBox/ListBox owns its wheel input.
        ApplyMouseWheel(e);
    }

    private void ApplyMouseWheel(MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs { Handled: true }) return;
        if (IsScrollVisible)
        {
            var lines = SystemInformation.MouseWheelScrollLines;
            var step = lines < 0 ? AvailableContentSize.Height : Math.Max(1, lines) * Font.Height;
            ScrollTo(ScrollOffset - e.Delta / SystemInformation.MouseWheelScrollDelta * step);
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }
    }

    private void OnContentEnter(object? sender, EventArgs e)
    {
        if (!IsScrollVisible || sender is not Control control || control is not (TextBoxBase or ButtonBase or ListControl or UpDownBase)) return;
        var bounds = _clip.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        if (bounds.Top < 0) ScrollTo(ScrollOffset + bounds.Top);
        else if (bounds.Bottom > _clip.Height) ScrollTo(ScrollOffset + bounds.Bottom - _clip.Height);
    }
    private void OnAdded(object? sender, ControlEventArgs e) { if (e.Control is { } child) Watch(child, true); }
    private void OnRemoved(object? sender, ControlEventArgs e) { if (e.Control is { } child) Watch(child, false); }
    private void Watch(Control control, bool attach)
    {
        if (attach) { control.Enter += OnContentEnter; control.MouseWheel += OnContentWheel; control.ControlAdded += OnAdded; control.ControlRemoved += OnRemoved; }
        else { control.Enter -= OnContentEnter; control.MouseWheel -= OnContentWheel; control.ControlAdded -= OnAdded; control.ControlRemoved -= OnRemoved; }
        foreach (Control child in control.Controls) Watch(child, attach);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _content is not null) Watch(_content.View, false);
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
