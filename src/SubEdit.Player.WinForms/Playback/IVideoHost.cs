namespace SubEdit.Player.WinForms.Playback;

internal interface IVideoHost : IDisposable
{
    event EventHandler? VideoResized;

    Control HostControl { get; }

    void ApplyVerticalOffset(int elevation);

    void ApplyHorizontalOffset(int offset);

    void ApplyZoom(int zoomPercent);

    void ShowContextMenuCentered();

    void Focus();

    void RegisterFileDrop(DragEventHandler dragEnter, DragEventHandler dragDrop);
}
