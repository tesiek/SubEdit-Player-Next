namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Destroy an owned dialog HWND before Form.Dispose detaches its native owner.</summary>
public class DialogWindow : Form
{
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed && IsHandleCreated)
        {
            // Form.Dispose removes Owner before Control.Dispose destroys the window.
            // For ShowInTaskbar=false that temporarily attaches a live window to TaskbarOwner,
            // which can change its DPI and scale the entire UI during destruction.
            DestroyHandle();
            // WM_DESTROY normally clears the Visible bit while Control.Disposing=true.
            // Here destruction precedes base.Dispose: synchronize managed visibility only
            // after the HWND is gone. This does not hide a live native window.
            if (Visible) base.SetVisibleCore(false);
        }
        base.Dispose(disposing);
    }
}
