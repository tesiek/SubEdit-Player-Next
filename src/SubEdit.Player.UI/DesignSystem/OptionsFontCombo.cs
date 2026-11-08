using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Native font selector with batched population when its handle is created.</summary>
public sealed class OptionsFontCombo : ComboBox
{
    private const int WindowStyle = -16;
    private const int VisibleStyle = 0x10000000;

    public OptionsFontCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        Sorted = true;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        var hwnd = Handle;
        var original = GetWindowLong(hwnd, WindowStyle);
        BeginUpdate();
        try { base.OnHandleCreated(e); }
        finally
        {
            EndUpdate();
            // WM_SETREDRAW can change WS_VISIBLE while this HWND is being created.
            var current = GetWindowLong(hwnd, WindowStyle);
            if ((current & VisibleStyle) != (original & VisibleStyle))
                SetWindowLong(hwnd, WindowStyle, (current & ~VisibleStyle) | (original & VisibleStyle));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
