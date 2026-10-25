using System.Runtime.InteropServices;

namespace SubEdit.Player.WinForms;

internal sealed class FullScreenWheelHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;
    private readonly Func<bool> _isActive;
    private readonly Func<Point, int, bool> _tryHandleWheel;
    private readonly HookProc _callback;
    private IntPtr _hook;

    public FullScreenWheelHook(Func<bool> isActive, Func<Point, int, bool> tryHandleWheel)
    {
        _isActive = isActive;
        _tryHandleWheel = tryHandleWheel;
        _callback = HookCallback;
    }

    public bool TryEnable()
    {
        if (_hook != IntPtr.Zero)
        {
            return true;
        }

        _hook = SetWindowsHookEx(WhMouseLl, _callback, GetModuleHandle(null), 0);
        return _hook != IntPtr.Zero;
    }

    public void Disable()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }

    public void Dispose() => Disable();

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == (IntPtr)WmMouseWheel && _isActive())
        {
            var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
            var delta = unchecked((short)(data.MouseData >> 16));
            if (_tryHandleWheel(new Point(data.Point.X, data.Point.Y), delta))
            {
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
