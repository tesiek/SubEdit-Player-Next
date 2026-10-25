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
