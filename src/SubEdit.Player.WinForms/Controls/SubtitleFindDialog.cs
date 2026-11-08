using SubEdit.Player.WinForms;

namespace SubEdit.Player.WinForms.Controls;

internal sealed class SubtitleFindDialog : StandardDialog
{
    private readonly TextBox _queryTextBox;
    private readonly CheckBox _matchCaseCheckBox;
    private readonly CheckBox _wholeWordCheckBox;

    public event EventHandler? FindNextRequested;
    public event EventHandler? FindPreviousRequested;

    public SubtitleFindDialog(string initialQuery) : base(400)
    {
        Text = "Znajdź tekst";
        Shell.Actions.CompactContentButtons = true;
        var section = Page.AddSection("Wyszukiwanie");
        _queryTextBox = Input(initialQuery, 32767);section.AddField("Znajdź:", _queryTextBox, 290);
        _matchCaseCheckBox = section.AddCheckBox("Uwzględniaj wielkość liter");
        _wholeWordCheckBox = section.AddCheckBox("Tylko całe słowa");
        Shell.Actions.Configure(SubEdit.Player.UI.DesignSystem.DialogActionProfile.CenteredOkCancel,
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Secondary, "Znajdź (F3)"),
            new(SubEdit.Player.UI.DesignSystem.DialogActionRole.Cancel, "Anuluj", DialogResult.Cancel));
        var find = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Secondary);
        find.Click += (_, _) => FindNextRequested?.Invoke(this, EventArgs.Empty);
        AcceptButton = find;CancelButton = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Cancel);
        Shown += (_, _) => { _queryTextBox.Focus(); _queryTextBox.SelectAll(); };
    }

    public string Query => _queryTextBox.Text;

    public bool MatchCase => _matchCaseCheckBox.Checked;

    public bool WholeWord => _wholeWordCheckBox.Checked;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Shift | Keys.F3))
        {
            FindPreviousRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F3)
        {
            FindNextRequested?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
