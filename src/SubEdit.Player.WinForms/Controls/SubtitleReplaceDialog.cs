using SubEdit.Player.WinForms;

namespace SubEdit.Player.WinForms.Controls;

internal sealed class SubtitleReplaceDialog : StandardDialog
{
    private readonly TextBox _queryTextBox;
    private readonly TextBox _replacementTextBox;
    private readonly CheckBox _matchCaseCheckBox;
    private readonly CheckBox _wholeWordCheckBox;

    public event EventHandler? FindNextRequested;
    public event EventHandler? ReplaceRequested;
    public event EventHandler? ReplaceAllRequested;

    public SubtitleReplaceDialog(string initialQuery, bool matchCase, bool wholeWord) : base(530)
    {
        Text = "Znajdź i zamień";
        StartPosition = FormStartPosition.Manual;
        Adaptive.FitBeforeFirstShow = true;
        Shell.Actions.CompactContentButtons = true;
        var section = Page.AddSection("Wyszukiwanie i zamiana");
        _queryTextBox = Input(initialQuery, 32767);_replacementTextBox = Input("", 32767);
        section.AddField("Znajdź:", _queryTextBox, widthProfile: SubEdit.Player.UI.DesignSystem.FieldWidthProfile.Fill);
        section.AddField("Zamień na:", _replacementTextBox, widthProfile: SubEdit.Player.UI.DesignSystem.FieldWidthProfile.Fill);
        _matchCaseCheckBox = section.AddCheckBox("Uwzględniaj wielkość liter",matchCase);
        _wholeWordCheckBox = section.AddCheckBox("Tylko całe słowa",wholeWord);
        Shell.Actions.Configure(SubEdit.Player.UI.DesignSystem.DialogActionProfile.CenteredOkCancel,
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Secondary,"Znajdź następny"),
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Apply,"Zamień"),
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Yes,"Zamień wszystko"),
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Cancel,"Anuluj"));
        var find = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Secondary);
        find.Click += (_, _) => FindNextRequested?.Invoke(this,EventArgs.Empty);
        Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Apply).Click += (_, _) => ReplaceRequested?.Invoke(this,EventArgs.Empty);
        Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Yes).Click += (_, _) => ReplaceAllRequested?.Invoke(this,EventArgs.Empty);
        var close = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Cancel);close.Click += (_, _) => Close();
        AcceptButton = find;CancelButton = close;
        Shown += (_, _) => { _queryTextBox.Focus(); _queryTextBox.SelectAll(); };
    }

    public string Query => _queryTextBox.Text;
    internal void PrepareForShow(Form owner)
    {
        Owner = owner;
        var area = Screen.FromControl(owner).WorkingArea;
        // Select the owner's monitor before creating HWNDs. Native PMv2 supplies DPI.
        Location = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
        SetUiFont(SubEdit.Player.UI.DesignSystem.UiDesignTokens.Current.DialogFont.SizeInPoints);
        InitializeHandles(this);
        FitAndCenter();
    }

    private static void InitializeHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) InitializeHandles(child);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Load follows native initial autoscaling, but precedes the first visible ShowWindow.
        FitAndCenter();
    }

    private void FitAndCenter()
    {
        Adaptive.FitToContent();
        if (Owner is not { IsDisposed: false } owner) return;
        var area = Screen.FromControl(owner).WorkingArea;
        Location = new Point(Math.Clamp(owner.Left + (owner.Width - Width) / 2, area.Left, Math.Max(area.Left, area.Right - Width)),
            Math.Clamp(owner.Top + (owner.Height - Height) / 2, area.Top, Math.Max(area.Top, area.Bottom - Height)));
    }

    public string Replacement => _replacementTextBox.Text;

    public bool MatchCase => _matchCaseCheckBox.Checked;

    public bool WholeWord => _wholeWordCheckBox.Checked;
}
