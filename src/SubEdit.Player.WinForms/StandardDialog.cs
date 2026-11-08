using SubEdit.Player.UI.DesignSystem;

namespace SubEdit.Player.WinForms;

internal abstract class StandardDialog : DialogWindow
{
    internal SettingsPageLayout Page { get; } = new();
    internal DialogShell Shell { get; }
    internal AdaptiveDialogLayout Adaptive { get; }
    private readonly UiFontOwner _fonts;
    private bool _initialFontPrepared;
    protected StandardDialog(int preferredWidth)
    {
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = UiDesignTokens.Current.DialogFont; BackColor = UiDesignTokens.Current.Background;
        ForeColor = UiDesignTokens.Current.Text; FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = ShowIcon = ShowInTaskbar = false;
        Shell = new DialogShell { Content = Page }; Controls.Add(Shell);
        Adaptive = new AdaptiveDialogLayout(this, Shell, preferredWidth);
        _fonts = new UiFontOwner(this) { SynchronizeDescendantFonts = true };
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        if (Modal && !_initialFontPrepared)
        {
            _initialFontPrepared = true;
            _fonts.SetSize(UiDesignTokens.Current.DialogFont.SizeInPoints);
        }
        // Opt in before Adaptive receives HandleCreated; leave modeless preparation alone.
        if (Modal) Adaptive.FitBeforeFirstShow = true;
        base.OnHandleCreated(e);
    }
    protected override void SetVisibleCore(bool value)
    {
        if (value && Modal && !Visible)
        {
            // ShowDialog has established its owner by this point. Prepare while hidden,
            // before Form.SetVisibleCore runs Load and the native ShowWindow.
            if (StartPosition == FormStartPosition.CenterParent && Owner is { IsDisposed: false } owner)
            {
                var area = Screen.FromControl(owner).WorkingArea;
                Location = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            }
            _fonts.SetSize(UiDesignTokens.Current.DialogFont.SizeInPoints);
            InitializeHandles(this);
            FitAndCenter();
        }
        base.SetVisibleCore(value);
    }
    private static void InitializeHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) InitializeHandles(child);
    }
    private void FitAndCenter()
    {
        Adaptive.FitToContent();
        if (StartPosition != FormStartPosition.CenterParent || Owner is not { IsDisposed: false } owner) return;
        var area = Screen.FromControl(owner).WorkingArea;
        Location = new Point(Math.Clamp(owner.Left + (owner.Width - Width) / 2, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(owner.Top + (owner.Height - Height) / 2, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }
    protected override void OnLoad(EventArgs e)
    {
        _fonts.SetSize(UiDesignTokens.Current.DialogFont.SizeInPoints);
        base.OnLoad(e);
        // Native autoscaling, CenterParent and Load handlers may change the metrics.
        // Verify the final size and center it before the first native visibility.
        if (Modal && !IsDisposed && DialogResult == DialogResult.None) FitAndCenter();
    }
    internal void SetUiFont(float points) => _fonts.SetSize(points);
    protected void ConfigureActions(Action? accept = null)
    {
        Shell.Actions.Configure(DialogActionProfile.CenteredOkCancel,
            new(DialogActionRole.Ok, "OK", accept is null ? DialogResult.OK : DialogResult.None),
            new(DialogActionRole.Cancel, "Anuluj", DialogResult.Cancel));
        var ok = Shell.Actions.Button(DialogActionRole.Ok);
        if (accept is not null) ok.Click += (_, _) => accept();
        AcceptButton = ok; CancelButton = Shell.Actions.Button(DialogActionRole.Cancel);
    }
    protected static TextBox Input(string text, int maxLength = 10) => new() { Text = text, MaxLength = maxLength };
    protected (RadioButton Selection, RadioButton From, RadioButton To) AddScope()
    {
        var scope = Page.AddSection("Obszar zmian napisów");
        return (scope.AddRadioOption("Według zaznaczenia", isChecked: true), scope.AddRadioOption("Od aktualnej linii"), scope.AddRadioOption("Do aktualnej linii"));
    }
    protected CheckBox AddSaveDefault() => Page.AddSection("Zapamiętanie ustawień").AddCheckBox("Zachowaj ustawienia jako domyślne");
    protected void BindOptionToolTips(ToolTip toolTip)
    {
        void Visit(Control parent)
        {
            if (parent is NativeOptionList options)
                foreach (var button in options.Controls.OfType<ButtonBase>())
                {
                    var text = toolTip.GetToolTip(button);
                    if (!string.IsNullOrEmpty(text)) toolTip.SetToolTip(options.CaptionFor(button), text);
                }
            foreach (Control child in parent.Controls) Visit(child);
        }
        Visit(Page);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) Adaptive.Dispose();
        base.Dispose(disposing);
        if (disposing) _fonts.Dispose();
    }
}
