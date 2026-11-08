namespace SubEdit.Player.WinForms.Controls;

internal sealed class DpiAwareStatusStrip : StatusStrip
{
    private const int WmDpiChangedBeforeParent = 0x02E2;
    private readonly Dictionary<ToolStripStatusLabel, Func<ToolStripStatusLabel, int>> _widths = new();
    private int? _messageOldDpi;

    public void AddFixed(ToolStripStatusLabel cell, int logicalWidth) =>
        AddMeasured(cell, _ => Math.Max(1, (int)Math.Round(logicalWidth * DeviceDpi / 96d)));

    public void AddMeasured(ToolStripStatusLabel cell, Func<ToolStripStatusLabel, int> measure)
    {
        _widths.Add(cell, measure);
        Items.Add(cell);
    }

    public void ApplyWidths()
    {
        foreach (var cell in Items.OfType<ToolStripStatusLabel>())
        {
            if (_widths.TryGetValue(cell, out var measure))
            {
                cell.Width = measure(cell);
            }
        }
    }

    protected override void RescaleConstantsForDpi(int oldDpi, int newDpi)
    {
        // Correct the lost old DPI only during a confirmed native transition.
        if (oldDpi == newDpi && _messageOldDpi is int captured && captured != newDpi)
        {
            oldDpi = captured;
        }

        base.RescaleConstantsForDpi(oldDpi, newDpi);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg != WmDpiChangedBeforeParent)
        {
            base.WndProc(ref message);
            return;
        }

        var previous = _messageOldDpi;
        _messageOldDpi = DeviceDpi;
        try
        {
            base.WndProc(ref message);
        }
        finally
        {
            _messageOldDpi = previous;
        }
    }
}
