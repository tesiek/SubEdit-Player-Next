namespace SubEdit.Player.WinForms.Controls;

internal sealed class EditorToolbarButton : ToolStripButton
{
    private readonly EditorToolbarPresentation _presentation;
    internal EditorToolbarIcon Icon { get; }

    internal EditorToolbarButton(string name, string toolTip, EditorToolbarIcon icon)
    {
        Icon = icon;
        Name = name;
        ToolTipText = toolTip;
        AccessibleName = toolTip;
        DisplayStyle = ToolStripItemDisplayStyle.Image;
        AutoSize = false;
        _presentation = new EditorToolbarPresentation(this, icon);
        _presentation.ApplyDpi(96);
    }

    internal void ApplyDpi(int dpi) => _presentation.ApplyDpi(dpi);
    protected override void OnOwnerChanged(EventArgs e) { base.OnOwnerChanged(e); _presentation?.ChangeOwner(Owner); }
    protected override void OnPaint(PaintEventArgs e) => _presentation.Paint(e.Graphics, Checked || Pressed, Selected);
    protected override void Dispose(bool disposing) { if (disposing) _presentation?.Dispose(); base.Dispose(disposing); }
}

internal sealed class EditorToolbarSplitButton : ToolStripSplitButton
{
    private readonly EditorToolbarPresentation _presentation;

    internal EditorToolbarSplitButton(string name, string toolTip)
    {
        Name = name;
        ToolTipText = toolTip;
        AccessibleName = toolTip;
        DisplayStyle = ToolStripItemDisplayStyle.Image;
        AutoSize = false;
        _presentation = new EditorToolbarPresentation(this, EditorToolbarIcon.Find);
        _presentation.ApplyDpi(96);
    }

    internal void ApplyDpi(int dpi) => _presentation.ApplyDpi(dpi);
    protected override void OnOwnerChanged(EventArgs e) { base.OnOwnerChanged(e); _presentation?.ChangeOwner(Owner); }
    protected override void OnPaint(PaintEventArgs e) =>
        _presentation.Paint(e.Graphics, ButtonPressed || DropDownButtonPressed || DropDown.Visible, Selected);
    protected override void Dispose(bool disposing) { if (disposing) _presentation?.Dispose(); base.Dispose(disposing); }
}

// Shared upper-toolbar presentation for action buttons and the existing Find split button.
// Uses the accepted transport palette/scale, without changing its rendering or behavior.
internal sealed class EditorToolbarPresentation(ToolStripItem item, EditorToolbarIcon icon) : IDisposable
{
    private ToolStrip? _owner;
    private int _dpi = 96;

    internal void ChangeOwner(ToolStrip? owner)
    {
        Dispose();
        _owner = owner;
        if (owner is not null)
        {
            owner.HandleCreated += OnDpiChanged;
            owner.DpiChangedAfterParent += OnDpiChanged;
        }
        ApplyDpi(owner?.DeviceDpi ?? 96);
    }

    private void OnDpiChanged(object? sender, EventArgs e) => ApplyDpi(_owner?.DeviceDpi ?? 96);

    internal void ApplyDpi(int dpi)
    {
        _dpi = Math.Max(96, dpi);
        item.Size = new Size(Scale(item is ToolStripSplitButton ? 41 : 29), Scale(29));
        item.Margin = new Padding(0, Scale(1), 0, Scale(2));
        if (item is ToolStripSplitButton split) split.DropDownButtonWidth = Scale(9);
        item.Invalidate();
    }

    private int Scale(int value) => PlaybackToolbarIcons.Scale(value, _dpi);

    internal void Paint(Graphics graphics, bool active, bool selected)
    {
        var bounds = new Rectangle(0, 0, item.Width - 1, item.Height - 1);
        if (active || (selected && item.Enabled))
        {
            using var fill = new SolidBrush(!item.Enabled ? Color.FromArgb(233, 236, 239) : active
                ? PlaybackToolbarIcons.ActiveBackground : Color.FromArgb(240, 243, 246));
            using var border = new Pen(item.Enabled && active ? Color.FromArgb(166, 199, 240) : Color.FromArgb(214, 222, 229));
            graphics.FillRectangle(fill, bounds);
            graphics.DrawRectangle(border, bounds);
        }
        var area = item is ToolStripSplitButton split ? split.ButtonBounds : new Rectangle(Point.Empty, item.Size);
        var size = 22f * _dpi / 96f;
        EditorToolbarIcons.Draw(graphics, icon,
            new RectangleF(area.Left + (area.Width - size) / 2, (item.Height - size) / 2, size, size), item.Enabled, active);
        if (item is ToolStripSplitButton dropdown)
        {
            var arrowArea = dropdown.DropDownButtonBounds;
            var center = arrowArea.Left + arrowArea.Width / 2f;
            var y = item.Height / 2f;
            var unit = _dpi / 96f;
            using var brush = new SolidBrush(!item.Enabled ? PlaybackToolbarIcons.Disabled : active ? PlaybackToolbarIcons.Active : PlaybackToolbarIcons.Normal);
            using var pen = new Pen(Color.FromArgb(214, 222, 229));
            graphics.DrawLine(pen, arrowArea.Left, Scale(4), arrowArea.Left, item.Height - Scale(4));
            graphics.FillPolygon(brush, [new PointF(center - 2.5f * unit, y - unit), new PointF(center + 2.5f * unit, y - unit), new PointF(center, y + 2 * unit)]);
        }
        if (item.Owner?.Focused == true && selected)
            ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(bounds, -3, -3));
    }

    public void Dispose()
    {
        if (_owner is null) return;
        _owner.HandleCreated -= OnDpiChanged;
        _owner.DpiChangedAfterParent -= OnDpiChanged;
        _owner = null;
    }
}
