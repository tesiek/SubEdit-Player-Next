namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Fits Options at the owner monitor DPI and refits after a real monitor or font change.</summary>
public sealed class OptionsFit : IDisposable
{
    private readonly Form form;
    private readonly OptionsDialogLayout layout;
    private Rectangle workingArea;
    private int minimumOuterWidth;
    private bool fitting;
    private bool resizing;
    private bool pending;
    private bool posted;
    private bool disposed;

    public OptionsFit(Form form, OptionsDialogLayout layout)
    {
        this.form = form;
        this.layout = layout;
        form.FontChanged += OnFontChanged;
        form.DpiChanged += OnDpiChanged;
        form.LocationChanged += OnLocationChanged;
        form.ClientSizeChanged += OnSizeChanged;
        form.ResizeBegin += OnResizeBegin;
        form.ResizeEnd += OnResizeEnd;
    }

    public void HoldInitialLayout() => layout.ArrangementDeferred = true;

    public void CompleteInitialLayout()
    {
        layout.ArrangementDeferred = false;
        Fit();
    }

    private void Fit()
    {
        if (fitting || disposed || !form.IsHandleCreated) return;
        fitting = true;
        var deferred = layout.ArrangementDeferred;
        layout.ArrangementDeferred = true;
        try
        {
            workingArea = Screen.FromControl(form).WorkingArea;
            var margin = UiDesignTokens.Pixels(layout.Tokens.ScreenInset, form.DeviceDpi);
            var chrome = form.Size - form.ClientSize;
            int width, naturalHeight;
            using (OptionsMeasureContext.Begin(form, layout))
            {
                var minimumClientWidth = layout.MinimumClientWidth;
                minimumOuterWidth = Math.Min(minimumClientWidth + chrome.Width, workingArea.Width);
                width = Math.Max(1, Math.Min(layout.MeasurePreferredClientWidth(minimumClientWidth),
                    workingArea.Width - chrome.Width - 2 * margin));
                naturalHeight = layout.MeasurePreferredHeight(width);
            }
            var available = Math.Max(1, workingArea.Height - chrome.Height);
            // Use the entire real work area before introducing a scrollbar.
            var height = Math.Min(naturalHeight, available);
            form.MinimumSize = Size.Empty;
            form.ClientSize = new Size(width, height);
            layout.ArrangementDeferred = false;
            layout.ArrangeViewport();
            SetMinimumSize();
            form.Location = new Point(
                Math.Clamp(form.Left, workingArea.Left, Math.Max(workingArea.Left, workingArea.Right - form.Width)),
                Math.Clamp(form.Top, workingArea.Top, Math.Max(workingArea.Top, workingArea.Bottom - form.Height)));
        }
        finally
        {
            layout.ArrangementDeferred = deferred;
            fitting = false;
        }
    }

    private void SetMinimumSize()
    {
        var chrome = form.Size - form.ClientSize;
        form.MinimumSize = new Size(minimumOuterWidth, Math.Min(layout.MinimumClientHeight + chrome.Height, workingArea.Height));
    }

    private void OnFontChanged(object? sender, EventArgs args) => RequestFit();
    private void OnDpiChanged(object? sender, DpiChangedEventArgs args) => RequestFit();
    private void OnResizeBegin(object? sender, EventArgs args) => resizing = true;

    private void OnResizeEnd(object? sender, EventArgs args)
    {
        resizing = false;
        PostFit();
    }

    private void OnLocationChanged(object? sender, EventArgs args)
    {
        if (form.Visible && Screen.FromControl(form).WorkingArea != workingArea) RequestFit();
    }

    private void OnSizeChanged(object? sender, EventArgs args)
    {
        if (fitting || pending || disposed || !form.Visible) return;
        fitting = true;
        try
        {
            layout.ArrangeViewport();
            SetMinimumSize();
        }
        finally { fitting = false; }
    }

    private void RequestFit()
    {
        if (disposed || fitting || !form.Visible || !form.IsHandleCreated) return;
        pending = true;
        PostFit();
    }

    private void PostFit()
    {
        if (!pending || posted || resizing || disposed || !form.IsHandleCreated) return;
        posted = true;
        form.BeginInvoke((Action)(() =>
        {
            posted = false;
            if (resizing || disposed || form.IsDisposed) return;
            pending = false;
            if (form.Visible) Fit();
        }));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        form.FontChanged -= OnFontChanged;
        form.DpiChanged -= OnDpiChanged;
        form.LocationChanged -= OnLocationChanged;
        form.ClientSizeChanged -= OnSizeChanged;
        form.ResizeBegin -= OnResizeBegin;
        form.ResizeEnd -= OnResizeEnd;
    }
}
