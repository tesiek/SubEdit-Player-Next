using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>UI #2 only: compensate initial system-DPI rendering, then let PMv2 own scaling.</summary>
internal sealed class InspectorRichTextBox : RichTextBox
{
    private readonly InspectorZoomState _dpi = new();
    private bool _applying;

    internal void ApplyDpiZoom(bool nativeDpiNotification = false)
    {
        if (!IsHandleCreated || Disposing || _applying) return;
        var windowDpi = GetDpiForWindow(Handle);
        var systemDpi = GetDpiForSystem();
        if (windowDpi == 0 || systemDpi == 0) return;
        var desired = _dpi.Update(windowDpi, systemDpi, nativeDpiNotification);
        _applying = true;
        try
        {
            // Absolute assignment, never multiply the previous zoom. A real native DPI
            // transition refreshes RichEdit's implicit rendering DPI; explicit zoom is then 1.
            if (Math.Abs(ZoomFactor - desired) > .0001f) ZoomFactor = desired;
        }
        finally { _applying = false; }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDpiZoom();
    }
    protected override void OnHandleDestroyed(EventArgs e)
    {
        _dpi.Reset();
        base.OnHandleDestroyed(e);
    }
    protected override void WndProc(ref Message m)
    {
        var message = m.Msg;
        // Also covers a nested first Paint during native initialization. No RTF/font work
        // occurs here; once initialized this is just a branch, with no DPI polling in Paint.
        if (message == 0x000f && !_dpi.Initialized) ApplyDpiZoom();
        base.WndProc(ref m);
        if (message == 0x02e3) ApplyDpiZoom(nativeDpiNotification: true);
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
}

internal sealed class InspectorZoomState
{
    private uint _referenceDpi;
    private uint _lastWindowDpi;
    internal bool Initialized => _referenceDpi != 0;
    internal float Update(uint windowDpi, uint systemDpi, bool nativeDpiNotification)
    {
        if (!Initialized) { _referenceDpi = systemDpi; _lastWindowDpi = windowDpi; }
        if (nativeDpiNotification && windowDpi != _lastWindowDpi)
        {
            // Do not mistake a duplicate/initial AFTERPARENT for a monitor transition.
            _referenceDpi = windowDpi;
            _lastWindowDpi = windowDpi;
        }
        return (float)windowDpi / _referenceDpi;
    }
    internal void Reset() { _referenceDpi = 0; _lastWindowDpi = 0; }
}
