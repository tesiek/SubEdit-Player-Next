using SubEdit.Player.WinForms;

namespace SubEdit.Player.WinForms.Controls;

internal sealed class SubtitleFindDialog : SubEditDialog
{
    private readonly TextBox _queryTextBox;
    private readonly CheckBox _matchCaseCheckBox;
    private readonly CheckBox _wholeWordCheckBox;

    public event EventHandler? FindNextRequested;
    public event EventHandler? FindPreviousRequested;

    public SubtitleFindDialog(string initialQuery)
    {
        Text = "Znajdź tekst";
        ClientSize = new Size(370, 125);

        var prompt = new Label
        {
            AutoSize = true,
            Location = new Point(12, 15),
            Text = "Znajdź:"
        };
        _queryTextBox = new TextBox
        {
            Location = new Point(65, 12),
            Size = new Size(292, 23),
            Text = initialQuery
        };
        _matchCaseCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(15, 49),
            Text = "Uwzględniaj wielkość liter"
        };
        _wholeWordCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(15, 74),
            Text = "Tylko całe słowa"
        };
        var findButton = new Button
        {
            Location = new Point(200, 88),
            Size = new Size(75, 25),
            Text = "Znajdź (F3)"
        };
        findButton.Click += (_, _) => FindNextRequested?.Invoke(this, EventArgs.Empty);
        var cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(282, 88),
            Size = new Size(75, 25),
            Text = "Anuluj"
        };

        Controls.AddRange([prompt, _queryTextBox, _matchCaseCheckBox, _wholeWordCheckBox, findButton, cancelButton]);
        AcceptButton = findButton;
        CancelButton = cancelButton;
        Shown += (_, _) =>
        {
            _queryTextBox.Focus();
            _queryTextBox.SelectAll();
        };
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
