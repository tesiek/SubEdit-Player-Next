namespace SubEdit.Player.WinForms.Controls;

// The native menu dismisses on mouse-down, before ToolStripButton.Click. Intercept only
// dropdown-button clicks while a menu is open, so a second click cannot reopen it.
internal sealed class TransportDropdownController : IMessageFilter, IDisposable
{
    private readonly ToolStrip _toolbar;
    private readonly Form? _form;
    private readonly HashSet<ContextMenuStrip> _pendingDisposals = new();
    private ToolStripButton? _button;
    internal ContextMenuStrip? ActiveMenu { get; private set; }

    internal TransportDropdownController(ToolStrip toolbar)
    {
        _toolbar = toolbar;
        _form = toolbar.FindForm();
        toolbar.ParentChanged += OnModeChanged;
        toolbar.VisibleChanged += OnVisibilityChanged;
        toolbar.Disposed += OnDisposed;
        if (_form is not null) _form.FormClosing += OnFormClosing;
    }

    internal void Toggle(ContextMenuStrip menu, ToolStripButton button)
    {
        var same = ReferenceEquals(_button, button) && ActiveMenu?.Visible == true;
        Close();
        if (same) { menu.Dispose(); return; }
        ActiveMenu = menu;
        _button = button;
        button.Checked = true;
        menu.Closed += OnClosed;
        menu.Disposed += OnMenuDisposed;
        Application.AddMessageFilter(this);
        try
        {
            menu.Show(_toolbar, new Point(button.Bounds.Left, button.Bounds.Bottom));
            if (!menu.Visible) { Release(menu); menu.Dispose(); }
        }
        catch { Release(menu); menu.Dispose(); throw; }
    }

    public bool PreFilterMessage(ref Message m) =>
        (m.Msg is 0x0201 or 0x0203) && HandleMouseDown(Cursor.Position);

    internal bool HandleMouseDown(Point screenPoint)
    {
        if (ActiveMenu?.Visible != true) return false;
        var local = _toolbar.PointToClient(screenPoint);
        var target = _toolbar.GetItemAt(local) as PlaybackToolbarButton;
        if (target is null || !target.Enabled || !PlaybackToolbarIcons.HasMenu(target.IconCommand)) return false;
        var same = ReferenceEquals(target, _button);
        Close();
        if (!same) target.PerformClick();
        return true;
    }

    internal void Close()
    {
        var menu = ActiveMenu;
        if (menu is null) return;
        menu.Close();
        Release(menu);
    }

    private void OnClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        var menu = (ContextMenuStrip)sender!;
        Release(menu);
        if (_toolbar.IsHandleCreated && !_toolbar.IsDisposed)
        {
            _pendingDisposals.Add(menu);
            _toolbar.BeginInvoke((Action)(() => { _pendingDisposals.Remove(menu); menu.Dispose(); }));
        }
        else menu.Dispose();
    }

    private void OnMenuDisposed(object? sender, EventArgs e) => Release((ContextMenuStrip)sender!);

    private void Release(ContextMenuStrip menu)
    {
        menu.Closed -= OnClosed;
        menu.Disposed -= OnMenuDisposed;
        if (!ReferenceEquals(ActiveMenu, menu)) return;
        if (_button is not null) _button.Checked = false;
        _button = null;
        ActiveMenu = null;
        Application.RemoveMessageFilter(this);
    }

    private void OnModeChanged(object? sender, EventArgs e) => Close();
    private void OnVisibilityChanged(object? sender, EventArgs e) { if (!_toolbar.Visible) Close(); }
    private void OnFormClosing(object? sender, FormClosingEventArgs e) => Close();
    private void OnDisposed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        var menu = ActiveMenu;
        Close();
        menu?.Dispose();
        foreach (var pending in _pendingDisposals.ToArray()) pending.Dispose();
        _pendingDisposals.Clear();
        Application.RemoveMessageFilter(this);
        _toolbar.ParentChanged -= OnModeChanged;
        _toolbar.VisibleChanged -= OnVisibilityChanged;
        _toolbar.Disposed -= OnDisposed;
        if (_form is not null) _form.FormClosing -= OnFormClosing;
    }
}
