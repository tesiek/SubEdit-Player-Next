namespace SubEdit.Player.WinForms.Controls;

internal sealed class PlaybackToolbarButton : ToolStripButton
{
    private ToolStrip? _dpiOwner;
    private int _dpi = 96;

    internal PlaybackToolbarCommand IconCommand { get; }

    internal PlaybackToolbarButton(PlaybackToolbarCommand command, string toolTip, bool checkOnClick = false)
    {
        IconCommand = command;
        Name = command.ToString();
        DisplayStyle = ToolStripItemDisplayStyle.Image;
        ToolTipText = toolTip;
        AccessibleName = toolTip;
        CheckOnClick = checkOnClick;
        AutoSize = false;
        ApplyDpi(96);
    }

    protected override void OnOwnerChanged(EventArgs e)
    {
        DetachDpiOwner();
        base.OnOwnerChanged(e);
        _dpiOwner = Owner;
        if (_dpiOwner is not null)
        {
            _dpiOwner.DpiChangedAfterParent += OnOwnerDpiChanged;
            _dpiOwner.HandleCreated += OnOwnerDpiChanged;
        }
        ApplyDpi(Owner?.DeviceDpi ?? 96);
    }

    private void OnOwnerDpiChanged(object? sender, EventArgs e) => ApplyDpi(Owner?.DeviceDpi ?? 96);

    internal void ApplyDpi(int dpi)
    {
        _dpi = Math.Max(96, dpi);
        Size = PlaybackToolbarIcons.ButtonSize(IconCommand, _dpi);
        Margin = new Padding(0, PlaybackToolbarIcons.Scale(1, _dpi), 0, PlaybackToolbarIcons.Scale(2, _dpi));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // ToolStrip paints its background; only this button's state and vector symbol are overlaid.
        var active = Checked || Pressed;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        if (active || (Selected && Enabled))
        {
            var background = !Enabled ? Color.FromArgb(233, 236, 239) : active
                ? PlaybackToolbarIcons.ActiveBackground : Color.FromArgb(240, 243, 246);
            var border = Enabled && active ? Color.FromArgb(166, 199, 240) : Color.FromArgb(214, 222, 229);
            using var brush = new SolidBrush(background);
            using var pen = new Pen(border);
            e.Graphics.FillRectangle(brush, bounds);
            e.Graphics.DrawRectangle(pen, bounds);
        }
        var scale = _dpi / 96f * 1.1f;
        var glyphWidth = (PlaybackToolbarIcons.HasMenu(IconCommand) ? 26 : 20) * scale;
        var glyphHeight = 20 * scale;
        var iconBounds = new RectangleF((Width - glyphWidth) / 2, (Height - glyphHeight) / 2, glyphWidth, glyphHeight);
        var color = !Enabled ? PlaybackToolbarIcons.Disabled : active ? PlaybackToolbarIcons.Active : PlaybackToolbarIcons.Normal;
        PlaybackToolbarIcons.Draw(e.Graphics, IconCommand, iconBounds, color,
            muted: IconCommand == PlaybackToolbarCommand.Volume && Checked);
        if (Owner?.Focused == true && Selected)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(bounds, -3, -3));
    }

    private void DetachDpiOwner()
    {
        if (_dpiOwner is null) return;
        _dpiOwner.DpiChangedAfterParent -= OnOwnerDpiChanged;
        _dpiOwner.HandleCreated -= OnOwnerDpiChanged;
        _dpiOwner = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) DetachDpiOwner();
        base.Dispose(disposing);
    }
}
