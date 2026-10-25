using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SubEdit.Player.WinForms.Controls;

// Prototype B: only the isolated copy uses this owner-painted volume control.
internal sealed class VolumeTrackBar : Control
{
    private int _minimum, _value;
    private int _maximum = 100;
    private int _smallChange = 5, _largeChange = 5;
    private bool _dragging, _hot;

    public VolumeTrackBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AutoSize = false;
        Size = new Size(82, 18);
        BackColor = SystemColors.Control;
        AccessibleName = "Głośność";
        AccessibleRole = AccessibleRole.Slider;
    }

    [DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value) return;
            _minimum = value;
            _maximum = Math.Max(_maximum, value);
            ReconcileRange();
        }
    }

    [DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value) return;
            _maximum = value;
            _minimum = Math.Min(_minimum, value);
            ReconcileRange();
        }
    }

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set => SetValue(value, scroll: false);
    }

    [DefaultValue(5)]
    public int SmallChange
    {
        get => _smallChange;
        set { ArgumentOutOfRangeException.ThrowIfNegative(value); _smallChange = value; }
    }

    [DefaultValue(5)]
    public int LargeChange
    {
        get => _largeChange;
        set { ArgumentOutOfRangeException.ThrowIfNegative(value); _largeChange = value; }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<int>? WheelStep { get; set; }

    public event EventHandler? ValueChanged;
    public event EventHandler? Scroll;
    protected override bool ShowFocusCues => false;

    private bool SetValue(int value, bool scroll)
    {
        var next = Math.Clamp(value, _minimum, _maximum);
        if (next == _value) return false;
        _value = next;
        Invalidate();
        ValueChanged?.Invoke(this, EventArgs.Empty);
        if (scroll) Scroll?.Invoke(this, EventArgs.Empty);
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        return true;
    }

    private void ReconcileRange()
    {
        if (!SetValue(_value, scroll: false)) Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !Enabled) { base.OnMouseDown(e); return; }
        _dragging = true;
        Capture = true;
        if (!SetValueFromX(e.X))
        {
            // Existing behavior: an unchanged click still displays the volume OSD.
            Scroll?.Invoke(this, EventArgs.Empty);
            Invalidate(); // pressed-state change, not an unchanged Value repaint
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) { SetValueFromX(e.X); return; }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !_dragging) { base.OnMouseUp(e); return; }
        SetValueFromX(e.X);
        _dragging = false;
        Capture = false;
        Invalidate();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture && _dragging)
        {
            _dragging = false;
            Invalidate();
        }
        base.OnMouseCaptureChanged(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (!_hot) { _hot = true; Invalidate(); }
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hot) { _hot = false; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _dragging = false;
            _hot = false;
            if (Capture) Capture = false;
        }
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x020A) // Preserve the existing consuming WM_MOUSEWHEEL route.
        {
            WheelStep?.Invoke(unchecked((short)((m.WParam.ToInt64() >> 16) & 0xffff)));
            return;
        }
        base.WndProc(ref m);
    }

    private bool SetValueFromX(int x) => SetValue(
        ValueAtX(x, Geometry(ClientSize, DeviceDpi, _minimum, _maximum, _value), _minimum, _maximum), scroll: true);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // OnPaint covers the complete client area in the same managed back buffer.
        // No WM_PAINT/WM_ERASEBKGND interception or parent repaint is involved.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = Parent is ToolStrip { Renderer: ToolStripProfessionalRenderer renderer }
            ? renderer.ColorTable.ToolStripGradientMiddle : BackColor;
        e.Graphics.Clear(background);
        var geometry = Geometry(ClientSize, DeviceDpi, _minimum, _maximum, _value);
        if (geometry.Thumb.IsEmpty) return;
        using var trackFill = new SolidBrush(Enabled ? Color.FromArgb(244, 244, 244) : SystemColors.ControlLight);
        using var trackBorder = new Pen(Enabled ? Color.FromArgb(145, 145, 145) : SystemColors.GrayText);
        using var thumbFill = new SolidBrush(!Enabled ? SystemColors.ControlLight :
            _dragging ? Color.FromArgb(210, 210, 210) : _hot ? Color.FromArgb(246, 246, 246) : Color.FromArgb(238, 238, 238));
        using var thumbBorder = new Pen(Enabled ? Color.FromArgb(90, 90, 90) : SystemColors.GrayText);
        e.Graphics.FillRectangle(trackFill, geometry.Track);
        DrawBorder(e.Graphics, trackBorder, geometry.Track);
        e.Graphics.FillRectangle(thumbFill, geometry.Thumb);
        DrawBorder(e.Graphics, thumbBorder, geometry.Thumb);
        base.OnPaint(e);
    }

    private static void DrawBorder(Graphics graphics, Pen pen, Rectangle bounds) =>
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

    internal readonly record struct SliderGeometry(Rectangle Track, Rectangle Thumb, int StartX, int EndX);

    internal static SliderGeometry Geometry(Size size, int dpi, int minimum, int maximum, int value)
    {
        if (size.Width <= 0 || size.Height <= 0) return default;
        int Scale(int logical) => Math.Max(1, (int)Math.Round(logical * Math.Max(1, dpi) / 96d));
        var thumbWidth = Math.Min(size.Width, Scale(8));
        var thumbHeight = Math.Min(size.Height, Scale(14));
        var leftHalf = thumbWidth / 2;
        var maximumCenter = size.Width - (thumbWidth - leftHalf);
        // Preserve the 5px end-center inset at 100%, scaling it with the painted thumb.
        var start = Math.Clamp(Scale(5), leftHalf, maximumCenter);
        var end = Math.Clamp(size.Width - Scale(5), start, maximumCenter);
        var range = (long)maximum - minimum;
        var fraction = range == 0 ? 0 : Math.Clamp((value - (double)minimum) / range, 0, 1);
        var center = start + (int)Math.Round(fraction * (end - start));
        var trackHeight = Math.Min(size.Height, Scale(5));
        return new(new Rectangle(start, (size.Height - trackHeight) / 2, Math.Max(1, end - start + 1), trackHeight),
            new Rectangle(center - leftHalf, (size.Height - thumbHeight) / 2, thumbWidth, thumbHeight), start, end);
    }

    internal static int ValueAtX(int x, SliderGeometry geometry, int minimum, int maximum)
    {
        var span = geometry.EndX - geometry.StartX;
        if (span <= 0) return minimum;
        var fraction = Math.Clamp((x - (double)geometry.StartX) / span, 0, 1);
        return (int)(minimum + (long)Math.Round(((long)maximum - minimum) * fraction));
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new VolumeAccessibleObject(this);

    private sealed class VolumeAccessibleObject(VolumeTrackBar owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => AccessibleRole.Slider;
        [AllowNull]
        public override string Value
        {
            get => owner.Value.ToString(CultureInfo.InvariantCulture);
            set
            {
                if (owner.Enabled && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    owner.Value = number;
            }
        }
    }
}
