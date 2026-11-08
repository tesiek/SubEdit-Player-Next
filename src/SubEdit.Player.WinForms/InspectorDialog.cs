using SubEdit.Player.UI.DesignSystem;

namespace SubEdit.Player.WinForms;

/// <summary>Production host of accepted UI #2; the concrete dialog owns data acquisition.</summary>
internal abstract class InspectorDialog : DialogWindow
{
    internal sealed class SessionPosition
    {
        internal (string Monitor, Point Offset)? LastMove { get; set; }
    }

    private const int WmMoving = 0x0216;
    private readonly UiFontOwner _fonts;
    private SessionPosition? _sessionPosition;
    private Point _moveStart;
    private bool _moving;
    internal InfoDialogLayout LayoutView { get; }
    internal InspectorContentLayout ReportContent { get; }
    internal AdaptiveDialogLayout Adaptive { get; }
    internal string ReportText { get; private set; } = "";

    protected InspectorDialog(string title, string summary, Action<string>? writeClipboard = null)
    {
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiDesignTokens.Current.DialogFont;
        BackColor = UiDesignTokens.Current.Background;
        ForeColor = UiDesignTokens.Current.Text;
        Text = title;
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        ShowIcon = ShowInTaskbar = MinimizeBox = MaximizeBox = false;
        ReportContent = new InspectorContentLayout();
        LayoutView = new InfoDialogLayout { Summary = summary };
        LayoutView.SetContent(ReportContent);
        if (writeClipboard is not null) ReportContent.CopyText = writeClipboard;
        ReportContent.WholeReportText = () => ReportText;
        LayoutView.Actions.Configure(DialogActionProfile.CenteredOkCancel,
            new(DialogActionRole.Secondary, "Kopiuj"), new(DialogActionRole.Ok, "Zamknij", DialogResult.OK));
        LayoutView.Actions.Button(DialogActionRole.Secondary).Click += (_, _) => CopyAll();
        var close = LayoutView.Actions.Button(DialogActionRole.Ok);
        close.Click += (_, _) => { if (!Modal) Close(); };
        AcceptButton = CancelButton = close;
        Controls.Add(LayoutView);
        Adaptive = new AdaptiveDialogLayout(this, LayoutView, 780) { PreferredLogicalHeight = 620 };
        _fonts = new UiFontOwner(this);
        _fonts.SetSize(UiDesignTokens.Current.DialogFont.SizeInPoints);
    }

    internal void PrepareForShow(Form owner, SessionPosition sessionPosition)
    {
        Owner = owner;
        _sessionPosition = sessionPosition;
        StartPosition = FormStartPosition.Manual;
        Adaptive.FitBeforeFirstShow = true;

        // Seed the HWND on the target screen so WinForms initializes it at that screen's DPI.
        var screen = ResolvePosition(owner).Screen;
        Location = ClampToWorkingArea(CenterIn(screen.WorkingArea, Size), Size, screen.WorkingArea);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_sessionPosition is null || Owner is not { IsDisposed: false } owner) return;

        // Native startup scaling has run. Use the fitted size before the first ShowWindow.
        Adaptive.FitToContent();
        var (screen, savedLocation) = ResolvePosition(owner);
        Location = ClampToWorkingArea(savedLocation ?? CenterIn(owner.Bounds, Size), Size, screen.WorkingArea);
    }

    private (Screen Screen, Point? SavedLocation) ResolvePosition(Form owner)
    {
        if (_sessionPosition?.LastMove is { } saved &&
            Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == saved.Monitor) is { } monitor)
        {
            return (monitor, monitor.Bounds.Location + (Size)saved.Offset);
        }

        return (Screen.FromControl(owner), null);
    }

    private static Point CenterIn(Rectangle bounds, Size size) =>
        new(bounds.Left + (bounds.Width - size.Width) / 2, bounds.Top + (bounds.Height - size.Height) / 2);

    private static Point ClampToWorkingArea(Point location, Size size, Rectangle area) =>
        new(Math.Clamp(location.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(location.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));

    protected override void OnResizeBegin(EventArgs e)
    {
        _moveStart = Location;
        _moving = false;
        base.OnResizeBegin(e);
    }

    protected override void WndProc(ref Message m)
    {
        // WM_MOVING comes from the user's move loop. Fit, DPI and edge resizing use other messages.
        if (m.Msg == WmMoving) _moving = true;
        base.WndProc(ref m);
    }

    protected override void OnResizeEnd(EventArgs e)
    {
        if (_moving && _sessionPosition is { } position && Location != _moveStart)
        {
            var screen = Screen.FromControl(this);
            position.LastMove = (screen.DeviceName, Location - (Size)screen.Bounds.Location);
        }
        _moving = false;
        base.OnResizeEnd(e);
    }

    protected void SetReport(string canonicalText, IReadOnlyList<InspectorSection> sections)
    {
        ReportText = canonicalText;
        ReportContent.SetSections(sections);
    }

    internal bool HandleTextShortcut(Keys key) => ReportContent.HandleShortcut(key);
    internal void CopyAll() => ReportContent.CopyAll();

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData is Keys.Enter or Keys.Escape && ReportContent.Report.ContextMenuStrip?.Visible != true)
        {
            DialogResult = DialogResult.OK;
            if (!Modal) Close();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Adaptive.Dispose();
        base.Dispose(disposing);
        if (disposing) _fonts.Dispose();
    }
}
