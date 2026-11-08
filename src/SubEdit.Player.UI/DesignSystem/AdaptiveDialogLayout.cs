namespace SubEdit.Player.UI.DesignSystem;

public interface IAdaptiveDialogSurface
{
    Control View { get; }
    UiDesignTokens Tokens { get; }
    int MinimumClientHeight { get; }
    int MeasurePreferredHeight(int clientWidth);
    void ArrangeViewport();
}

/// <summary>Optional content-derived width policy; report windows keep their existing policy.</summary>
public interface IDialogMinimumMetrics
{
    int MinimumClientWidth { get; }
    event EventHandler? MetricsChanged;
}

/// <summary>Owns form subscriptions and fits measured content; it does not own the form.</summary>
public sealed class AdaptiveDialogLayout : IDisposable
{
    private readonly Form _form;
    private readonly IAdaptiveDialogSurface _shell;
    private readonly int _preferredWidth;
    private Rectangle _workingArea;
    private bool _fitting;
    private bool _disposed;
    private bool _fitPending;
    private bool _fitPosted;
    private bool _inSizeMove;
    private int _minimumOuterWidth;
    private bool _preparedBeforeShow;

    public AdaptiveDialogLayout(Form form, IAdaptiveDialogSurface shell, int preferredLogicalWidth)
    {
        _form = form;
        _shell = shell;
        _preferredWidth = preferredLogicalWidth;
        form.Shown += OnShown;
        form.HandleCreated += OnHandleCreated;
        form.DpiChanged += OnDpiChanged;
        form.FontChanged += OnFontChanged;
        form.ClientSizeChanged += OnSizeChanged;
        form.LocationChanged += OnLocationChanged;
        form.ResizeBegin += OnResizeBegin;
        form.ResizeEnd += OnResizeEnd;
        form.Disposed += OnDisposed;
        if (shell is IDialogMinimumMetrics metrics) metrics.MetricsChanged += OnMetricsChanged;
    }

    /// <summary>Optional natural reading-window height, e.g. for a normally scrollable inspector.</summary>
    public int? PreferredLogicalHeight { get; set; }
    public Func<int>? PreferredClientWidthProvider { get; set; }

    /// <summary>Opt in after eager content registration; fit at native handle creation, before visibility.</summary>
    public bool FitBeforeFirstShow { get; set; }
    private void OnHandleCreated(object? sender, EventArgs e)
    {
        if (!FitBeforeFirstShow || _preparedBeforeShow || _form.Visible) return;
        FitToContent();
        _preparedBeforeShow = true;
    }

    public void FitToContent()
    {
        if (_fitting || _disposed || _form.IsDisposed || !_form.IsHandleCreated) return;
        if (_inSizeMove) { RequestFit(); return; }
        _fitting = true;
        try
        {
            _workingArea = Screen.FromControl(_form).WorkingArea;
            var margin = UiDesignTokens.Pixels(_shell.Tokens.ScreenInset, _form.DeviceDpi);
            var chrome = _form.Size - _form.ClientSize;
            var contentMinimum = (_shell as IDialogMinimumMetrics)?.MinimumClientWidth;
            _minimumOuterWidth = Math.Min(contentMinimum is { } minimum ? minimum + chrome.Width
                : UiDesignTokens.Pixels(220, _form.DeviceDpi), _workingArea.Width);
            var desiredWidth = PreferredClientWidthProvider?.Invoke() ?? UiDesignTokens.Pixels(_preferredWidth, _form.DeviceDpi);
            var width = Math.Max(1, Math.Min(Math.Max(desiredWidth, _minimumOuterWidth - chrome.Width),
                _workingArea.Width - chrome.Width - 2 * margin));
            var availableHeight = Math.Max(1, _workingArea.Height - chrome.Height - 2 * margin);
            if (PreferredLogicalHeight is { } preferredHeight)
                availableHeight = Math.Min(availableHeight, UiDesignTokens.Pixels(preferredHeight, _form.DeviceDpi));
            var naturalHeight = _shell.MeasurePreferredHeight(width);
            _form.MinimumSize = Size.Empty;
            _form.ClientSize = new Size(width, Math.Min(naturalHeight, availableHeight));
            _shell.ArrangeViewport();
            SetMinimumSize();
            var left = Math.Clamp(_form.Left, _workingArea.Left, Math.Max(_workingArea.Left, _workingArea.Right - _form.Width));
            var top = Math.Clamp(_form.Top, _workingArea.Top, Math.Max(_workingArea.Top, _workingArea.Bottom - _form.Height));
            _form.Location = new Point(left, top);
        }
        finally { _fitting = false; }
    }

    private void SetMinimumSize()
    {
        var chrome = _form.Size - _form.ClientSize;
        var minHeight = _shell.MinimumClientHeight + chrome.Height;
        _form.MinimumSize = new Size(
            _minimumOuterWidth,
            Math.Min(minHeight, _workingArea.Height));
    }

    private void OnShown(object? sender, EventArgs e) { if (!_preparedBeforeShow) FitToContent(); }
    private void OnDpiChanged(object? sender, DpiChangedEventArgs e) => RequestFit();
    private void OnFontChanged(object? sender, EventArgs e) => RequestFit();
    private void OnMetricsChanged(object? sender, EventArgs e) => RequestFit();
    private void RequestFit()
    {
        if (_disposed || !_form.Visible || !_form.IsHandleCreated) return;
        _fitPending = true;
        PostPendingFit();
    }
    private void PostPendingFit()
    {
        if (!_fitPending || _fitPosted || _inSizeMove || _disposed || !_form.IsHandleCreated) return;
        _fitPosted = true;
        // BeginInvoke can run inside the native move/size modal loop. The request survives
        // that callback and is posted again at ResizeEnd, after Windows owns its final bounds.
        _form.BeginInvoke((Action)(() =>
        {
            _fitPosted = false;
            if (_disposed || _form.IsDisposed || _inSizeMove) return;
            _fitPending = false;

            if (_form.Visible) FitToContent();
        }));
    }
    private void OnResizeBegin(object? sender, EventArgs e)
    {
        _inSizeMove = true;
    }
    private void OnResizeEnd(object? sender, EventArgs e)
    {
        _inSizeMove = false;
        PostPendingFit();
    }
    private void OnSizeChanged(object? sender, EventArgs e)
    {
        if (_fitting || _fitPending || !_form.Visible || _disposed) return;
        _fitting = true;
        try { _shell.ArrangeViewport(); SetMinimumSize(); }
        finally { _fitting = false;  }
    }
    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_form.Visible && Screen.FromControl(_form).WorkingArea != _workingArea) RequestFit();
    }
    private void OnDisposed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _form.Shown -= OnShown;
        _form.HandleCreated -= OnHandleCreated;
        _form.DpiChanged -= OnDpiChanged;
        _form.FontChanged -= OnFontChanged;
        _form.ClientSizeChanged -= OnSizeChanged;
        _form.LocationChanged -= OnLocationChanged;
        _form.ResizeBegin -= OnResizeBegin;
        _form.ResizeEnd -= OnResizeEnd;
        _form.Disposed -= OnDisposed;
        if (_shell is IDialogMinimumMetrics metrics) metrics.MetricsChanged -= OnMetricsChanged;
    }
}
