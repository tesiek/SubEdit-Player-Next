namespace SubEdit.Player.WinForms;

internal sealed class VideoInputMessageFilter : IMessageFilter
{
    private const int WmLButtonDoubleClick = 0x0203;
    private const int WmKeyDown = 0x0100;
    private readonly Func<bool> _isVideoInputEnabled;
    private readonly Func<Point, bool> _isVideoSurface;
    private readonly Action _toggleFullScreen;
    private readonly Action _showOptions;

    public VideoInputMessageFilter(
        Func<bool> isVideoInputEnabled,
        Func<Point, bool> isVideoSurface,
        Action toggleFullScreen,
        Action showOptions)
    {
        _isVideoInputEnabled = isVideoInputEnabled;
        _isVideoSurface = isVideoSurface;
        _toggleFullScreen = toggleFullScreen;
        _showOptions = showOptions;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (!_isVideoInputEnabled())
        {
            return false;
        }

        if (message.Msg == WmKeyDown && (Keys)message.WParam == Keys.F11)
        {
            _toggleFullScreen();
            return true;
        }

        if (message.Msg == WmKeyDown && (Keys)message.WParam == Keys.F12)
        {
            _showOptions();
            return true;
        }

        if (message.Msg != WmLButtonDoubleClick || !_isVideoSurface(Control.MousePosition))
        {
            return false;
        }

        _toggleFullScreen();
        return true;
    }
}
