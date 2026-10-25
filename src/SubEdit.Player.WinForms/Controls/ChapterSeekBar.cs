namespace SubEdit.Player.WinForms;

internal enum SeekBarInteractionState
{
    Idle,
    Pressed,
    Dragging
}

internal sealed class ChapterSeekBar : Control
{
    private const int TrackHeight = 7;
    private const int HorizontalInset = 9;
    private const int ThumbWidth = 8;
    private const int ThumbHeight = 14;
    private const int DragSeekIntervalMilliseconds = 200;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private long _durationMilliseconds;
    private IReadOnlyList<long> _chapterMarkers = Array.Empty<long>();
    private SeekBarInteractionState _interactionState = SeekBarInteractionState.Idle;
    private Rectangle _dragBox;
    private int? _intendedValue;
    private bool _hasPreviewSeek;
    private int? _lastPreviewSeekValue;
    private bool _isCommitting;
    private bool _isCancelled;
    private readonly System.Windows.Forms.Timer _dragSeekTimer = new() { Interval = DragSeekIntervalMilliseconds };

    public ChapterSeekBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AutoSize = false;
        Height = 22;
        BackColor = Color.FromArgb(212, 212, 212);
        _dragSeekTimer.Tick += (_, _) => RequestDragSeek();
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Action<int>? WheelStep { get; set; }

    internal event EventHandler? InteractionStarted;

    internal event EventHandler? PreviewSeekRequested;

    internal event EventHandler? SeekCommitted;

    internal event EventHandler? ValueChanged;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal SeekBarInteractionState InteractionState => _interactionState;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool IsInteracting => _interactionState != SeekBarInteractionState.Idle;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool IsDragging => _interactionState == SeekBarInteractionState.Dragging;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int? IntendedValue => _intendedValue;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal Size DragSize { get; set; } = SystemInformation.DragSize;

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Minimum
    {
        get => _minimum;
        set
        {
            if (_minimum == value)
            {
                return;
            }

            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            Value = _value;
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Maximum
    {
        get => _maximum;
        set
        {
            if (_maximum == value)
            {
                return;
            }

            _maximum = Math.Max(_minimum, value);
            Value = _value;
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal int Value
    {
        get => _value;
        set
        {
            var boundedValue = Math.Clamp(value, _minimum, _maximum);
            if (_value == boundedValue)
            {
                return;
            }

            _value = boundedValue;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal long DurationMilliseconds
    {
        get => _durationMilliseconds;
        set
        {
            var boundedValue = Math.Max(0, value);
            if (_durationMilliseconds == boundedValue)
            {
                return;
            }

            _durationMilliseconds = boundedValue;
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal IReadOnlyList<long> ChapterMarkers
    {
        get => _chapterMarkers;
        set
        {
            _chapterMarkers = value ?? Array.Empty<long>();
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isCancelled = false;
            _isCommitting = false;
            _hasPreviewSeek = false;
            _lastPreviewSeekValue = null;
            var dragSize = DragSize;
            _dragBox = new Rectangle(
                e.X - (dragSize.Width / 2),
                e.Y - (dragSize.Height / 2),
                dragSize.Width,
                dragSize.Height);
            _interactionState = SeekBarInteractionState.Pressed;
            Capture = true;
            SetValueFromX(e.X);
            _intendedValue = _value;
            InteractionStarted?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_interactionState == SeekBarInteractionState.Pressed)
        {
            if (!_dragBox.Contains(e.Location))
            {
                _interactionState = SeekBarInteractionState.Dragging;
                SetValueFromX(e.X);
                _intendedValue = _value;
                StartPreviewSeekTimerIfNeeded();
            }
            else
            {
                SetValueFromX(e.X);
                _intendedValue = _value;
            }

            return;
        }

        if (_interactionState == SeekBarInteractionState.Dragging)
        {
            SetValueFromX(e.X);
            _intendedValue = _value;
            StartPreviewSeekTimerIfNeeded();
            return;
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _interactionState != SeekBarInteractionState.Idle && !_isCancelled)
        {
            SetValueFromX(e.X);
            _intendedValue = _value;
            CommitInteraction();
            return;
        }

        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);

        if (!Capture && _interactionState != SeekBarInteractionState.Idle && !_isCancelled && !_isCommitting)
        {
            CommitInteraction();
        }
    }

    internal void CancelInteraction()
    {
        if (_interactionState == SeekBarInteractionState.Idle && !_isCancelled)
        {
            return;
        }

        _isCancelled = true;
        _dragSeekTimer.Stop();
        _interactionState = SeekBarInteractionState.Idle;
        _intendedValue = null;
        _hasPreviewSeek = false;
        _lastPreviewSeekValue = null;

        if (Capture)
        {
            Capture = false;
        }
    }

    private void CommitInteraction()
    {
        if (_interactionState == SeekBarInteractionState.Idle || _isCancelled || _isCommitting)
        {
            return;
        }

        _isCommitting = true;
        try
        {
            _dragSeekTimer.Stop();
            _interactionState = SeekBarInteractionState.Idle;

            if (Capture)
            {
                Capture = false;
            }

            SeekCommitted?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _intendedValue = null;
            _hasPreviewSeek = false;
            _lastPreviewSeekValue = null;
            _isCommitting = false;
            _isCancelled = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dragSeekTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        WheelStep?.Invoke(e.Delta);
    }

    // DrawRectangle includes the far border pixel. Expose existing geometry without
    // changing painting, values or interaction, so the adjacent group can align to it.
    internal int MaximumThumbRightPixel => Math.Min(ClientSize.Width - 1, GetPositionX(_maximum) + ThumbWidth / 2);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        var track = GetTrackRectangle();
        using var trackFill = new SolidBrush(Color.FromArgb(244, 244, 244));
        using var trackBorder = new Pen(Color.FromArgb(145, 145, 145));
        graphics.FillRectangle(trackFill, track);
        graphics.DrawRectangle(trackBorder, track);

        var thumbValue = _intendedValue ?? _value;
        var thumbX = GetPositionX(thumbValue);
        var filledWidth = Math.Clamp(thumbX - track.Left, 0, track.Width);
        if (filledWidth > 0)
        {
            using var progress = new SolidBrush(Color.FromArgb(170, 190, 205));
            graphics.FillRectangle(progress, track.Left + 1, track.Top + 1, filledWidth, track.Height - 1);
        }

        DrawChapterMarkers(graphics, track);
        DrawThumb(graphics, thumbX);
    }

    private void DrawChapterMarkers(Graphics graphics, Rectangle track)
    {
        if (_durationMilliseconds <= 0 || _chapterMarkers.Count == 0)
        {
            return;
        }

        using var markerPen = new Pen(Color.FromArgb(115, 115, 115));
        foreach (var marker in _chapterMarkers)
        {
            if (marker <= 0 || marker >= _durationMilliseconds)
            {
                continue;
            }

            var markerX = track.Left + (int)Math.Round(
                marker / (double)_durationMilliseconds * track.Width);
            graphics.DrawLine(markerPen, markerX, track.Top - 3, markerX, track.Bottom + 3);
        }
    }

    private void DrawThumb(Graphics graphics, int centerX)
    {
        var thumb = new Rectangle(
            centerX - ThumbWidth / 2,
            Math.Max(0, (Height - ThumbHeight) / 2),
            ThumbWidth,
            ThumbHeight);
        thumb = Rectangle.Intersect(thumb, ClientRectangle);
        using var thumbFill = new SolidBrush(Color.FromArgb(238, 238, 238));
        using var thumbBorder = new Pen(Color.FromArgb(90, 90, 90));
        graphics.FillRectangle(thumbFill, thumb);
        graphics.DrawRectangle(thumbBorder, thumb);
    }

    private Rectangle GetTrackRectangle()
    {
        var width = Math.Max(1, ClientSize.Width - 2 * HorizontalInset);
        var top = Math.Max(0, (ClientSize.Height - TrackHeight) / 2);
        return new Rectangle(HorizontalInset, top, width, TrackHeight);
    }

    private int GetPositionX(int value)
    {
        var track = GetTrackRectangle();
        var range = _maximum - _minimum;
        var ratio = range == 0 ? 0d : (value - _minimum) / (double)range;
        return track.Left + (int)Math.Round(Math.Clamp(ratio, 0d, 1d) * track.Width);
    }

    private void SetValueFromX(int x)
    {
        var track = GetTrackRectangle();
        var ratio = track.Width <= 0
            ? 0d
            : Math.Clamp((x - track.Left) / (double)track.Width, 0d, 1d);
        var calculatedValue = _minimum + (int)Math.Round((_maximum - _minimum) * ratio);
        _intendedValue = calculatedValue;
        Value = calculatedValue;
    }

    private void RequestDragSeek()
    {
        if (_interactionState != SeekBarInteractionState.Dragging)
        {
            _dragSeekTimer.Stop();
            return;
        }

        var targetValue = _intendedValue ?? _value;
        if (_hasPreviewSeek && _lastPreviewSeekValue == targetValue)
        {
            _dragSeekTimer.Stop();
            return;
        }

        _lastPreviewSeekValue = targetValue;
        _hasPreviewSeek = true;
        PreviewSeekRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StartPreviewSeekTimerIfNeeded()
    {
        var targetValue = _intendedValue ?? _value;
        if (!_dragSeekTimer.Enabled && (!_hasPreviewSeek || _lastPreviewSeekValue != targetValue))
        {
            _dragSeekTimer.Start();
        }
    }

    internal void SimulateMouseDown(int x, int y, MouseButtons button = MouseButtons.Left) =>
        OnMouseDown(new MouseEventArgs(button, 1, x, y, 0));

    internal void SimulateMouseMove(int x, int y) =>
        OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, x, y, 0));

    internal void SimulateMouseUp(int x, int y, MouseButtons button = MouseButtons.Left) =>
        OnMouseUp(new MouseEventArgs(button, 1, x, y, 0));

    internal void SimulateDragSeekTimerTick() =>
        RequestDragSeek();

    internal void SimulateCaptureLoss()
    {
        if (Capture)
        {
            Capture = false;
        }
        else
        {
            OnMouseCaptureChanged(EventArgs.Empty);
        }
    }
}
