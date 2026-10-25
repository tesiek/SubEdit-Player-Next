using System.Windows.Forms;
using FlyleafLib.Controls.WinForms;

namespace SubEdit.Player.WinForms.Playback;

internal sealed class SubEditFlyleafHost : FlyleafHost
{
    public event DragEventHandler? FileDragEnter;
    public event DragEventHandler? FileDragDrop;

    public SubEditFlyleafHost()
    {
        OpenOnDrop = false;
        SwapOnDrop = false;
        SwapDragEnterOnShift = false;
        AllowDrop = false;
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        FileDragEnter?.Invoke(this, drgevent);
    }

    protected override void OnDragDrop(DragEventArgs drgevent)
    {
        FileDragDrop?.Invoke(this, drgevent);
    }
}
