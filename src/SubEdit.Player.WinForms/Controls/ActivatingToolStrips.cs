namespace SubEdit.Player.WinForms.Controls;

internal sealed class ActivatingToolStrip : ToolStrip
{
    private const int WmMouseActivate = 0x0021;
    private const int MaActivate = 1;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaActivate;
            return;
        }

        base.WndProc(ref m);
    }
}

internal sealed class ActivatingMenuStrip : MenuStrip
{
    private const int WmMouseActivate = 0x0021;
    private const int MaActivate = 1;
    private const int WmDpiChangedBeforeParent = 0x02E2;
    private int? _messageOldDpi;

    protected override void RescaleConstantsForDpi(int oldDpi, int newDpi)
    {
        // Some .NET 10 versions lose the old DPI before dispatching this callback.
        // Correct only a confirmed transition; leave valid framework arguments intact.
        if (oldDpi == newDpi && _messageOldDpi is int captured && captured != newDpi)
        {
            oldDpi = captured;
        }

        base.RescaleConstantsForDpi(oldDpi, newDpi);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaActivate;
            return;
        }

        if (m.Msg == WmDpiChangedBeforeParent)
        {
            var previous = _messageOldDpi;
            _messageOldDpi = DeviceDpi;
            try
            {
                base.WndProc(ref m);
            }
            finally
            {
                _messageOldDpi = previous;
            }

            return;
        }

        base.WndProc(ref m);
    }
}
