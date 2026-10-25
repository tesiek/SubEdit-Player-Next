using FlyleafLib.Controls.WinForms;

namespace SubEdit.Player.WinForms.Playback;

internal sealed class FlyleafVideoHost : IVideoHost
{
    private readonly Panel _container;
    private readonly SubEditFlyleafHost _flyleafHost;
    private readonly ContextMenuStrip _contextMenu;
    private DragEventHandler? _fileDragEnterHandler;
    private DragEventHandler? _fileDragDropHandler;
    private int _horizontalOffset;
    private int _verticalElevation;
    private int _zoomPercent = 100;
    private bool _disposed;

    public FlyleafVideoHost(
        Panel container,
        FlyleafPlaybackEngine playbackEngine,
        ContextMenuStrip contextMenu)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(playbackEngine);
        ArgumentNullException.ThrowIfNull(contextMenu);

        _container = container;
        _contextMenu = contextMenu;
        _flyleafHost = new SubEditFlyleafHost
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Player = playbackEngine.Player,
            ContextMenuStrip = contextMenu,
            KeyBindings = false,
            ToggleFullScreenOnDoubleClick = false,
            PanMoveOnCtrl = false,
            PanZoomOnCtrlWheel = false,
            PanRotateOnShiftWheel = false,
            DragMove = false
        };
        _flyleafHost.Resize += OnFlyleafHostResize;

        _container.Controls.Add(_flyleafHost);
        _flyleafHost.BringToFront();
    }

    public event EventHandler? VideoResized;

    public Control HostControl => _flyleafHost;

    public void ApplyVerticalOffset(int elevation)
    {
        if (_disposed)
        {
            return;
        }

        _verticalElevation = elevation;
        UpdatePanYOffset();
    }

    public void ApplyHorizontalOffset(int offset)
    {
        if (_disposed)
        {
            return;
        }

        _horizontalOffset = offset;
        UpdatePanXOffset();
    }

    public void ApplyZoom(int zoomPercent)
    {
        if (_disposed)
        {
            return;
        }

        _zoomPercent = Math.Max(10, zoomPercent);
        UpdateZoom();
    }

    private static double ConvertPixelPanToNormalizedOffset(int pixelOffset, int dimension)
    {
        if (dimension <= 0 || pixelOffset == 0)
        {
            return 0.0;
        }

        var basePan = (double)pixelOffset / dimension;
        return basePan < 0
            ? Math.BitDecrement(basePan)
            : Math.BitIncrement(basePan);
    }

    private void UpdatePanXOffset()
    {
        if (_disposed || _flyleafHost.Player is not { } player)
        {
            return;
        }

        var rendererWidth = player.Renderer?.ControlWidth ?? 0;
        if (rendererWidth <= 0)
        {
            rendererWidth = _flyleafHost.ClientSize.Width > 0
                ? _flyleafHost.ClientSize.Width
                : _container.ClientSize.Width;
        }

        player.Config.Video.PanXOffset = ConvertPixelPanToNormalizedOffset(_horizontalOffset, rendererWidth);
    }

    private void UpdatePanYOffset()
    {
        if (_disposed || _flyleafHost.Player is not { } player)
        {
            return;
        }

        var rendererHeight = player.Renderer?.ControlHeight ?? 0;
        if (rendererHeight <= 0)
        {
            rendererHeight = _flyleafHost.ClientSize.Height > 0
                ? _flyleafHost.ClientSize.Height
                : _container.ClientSize.Height;
        }

        player.Config.Video.PanYOffset = ConvertPixelPanToNormalizedOffset(-_verticalElevation, rendererHeight);
    }

    private void UpdateZoom()
    {
        if (_disposed || _flyleafHost.Player is not { } player)
        {
            return;
        }

        player.Config.Video.Zoom = _zoomPercent;
    }

    public void ShowContextMenuCentered()
    {
        if (_disposed || _flyleafHost.ContextMenuStrip is not { } menu)
        {
            return;
        }

        menu.Show(
            _flyleafHost,
            new Point(_flyleafHost.ClientSize.Width / 2, _flyleafHost.ClientSize.Height / 2));
    }

    public void Focus() => _flyleafHost.Focus();

    public void RegisterFileDrop(DragEventHandler dragEnter, DragEventHandler dragDrop)
    {
        ArgumentNullException.ThrowIfNull(dragEnter);
        ArgumentNullException.ThrowIfNull(dragDrop);

        if (_disposed)
        {
            return;
        }

        if (_fileDragEnterHandler is not null)
        {
            _flyleafHost.FileDragEnter -= _fileDragEnterHandler;
        }

        if (_fileDragDropHandler is not null)
        {
            _flyleafHost.FileDragDrop -= _fileDragDropHandler;
        }

        _fileDragEnterHandler = dragEnter;
        _fileDragDropHandler = dragDrop;

        _flyleafHost.FileDragEnter += _fileDragEnterHandler;
        _flyleafHost.FileDragDrop += _fileDragDropHandler;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _flyleafHost.Resize -= OnFlyleafHostResize;
        if (_fileDragEnterHandler is not null)
        {
            _flyleafHost.FileDragEnter -= _fileDragEnterHandler;
            _fileDragEnterHandler = null;
        }

        if (_fileDragDropHandler is not null)
        {
            _flyleafHost.FileDragDrop -= _fileDragDropHandler;
            _fileDragDropHandler = null;
        }

        if (_flyleafHost.Player is { } player)
        {
            player.Config.Video.PanXOffset = 0;
            player.Config.Video.PanYOffset = 0;
            player.Config.Video.Zoom = 100;
        }

        _flyleafHost.Player = null!;
        _flyleafHost.ContextMenuStrip = null;
        _container.Controls.Remove(_flyleafHost);
        _flyleafHost.Dispose();
        _contextMenu.Dispose();
        VideoResized = null;
    }

    private void OnFlyleafHostResize(object? sender, EventArgs e)
    {
        UpdatePanXOffset();
        UpdatePanYOffset();
        VideoResized?.Invoke(this, e);
    }
}
