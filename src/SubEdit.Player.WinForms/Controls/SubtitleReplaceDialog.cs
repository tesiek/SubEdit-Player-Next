using SubEdit.Player.WinForms;

namespace SubEdit.Player.WinForms.Controls;

internal sealed class SubtitleReplaceDialog : SubEditDialog
{
    private readonly TextBox _queryTextBox;
    private readonly TextBox _replacementTextBox;
    private readonly CheckBox _matchCaseCheckBox;
    private readonly CheckBox _wholeWordCheckBox;

    public event EventHandler? FindNextRequested;
    public event EventHandler? ReplaceRequested;
    public event EventHandler? ReplaceAllRequested;

    public SubtitleReplaceDialog(string initialQuery, bool matchCase, bool wholeWord)
    {
        Text = "Znajdź i zamień";
        ClientSize = new Size(530, 160);

        var findLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 15),
            Text = "Znajdź:"
        };
        _queryTextBox = new TextBox
        {
            Location = new Point(96, 12),
            Size = new Size(280, 23),
            Text = initialQuery
        };
        var replaceLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 44),
            Text = "Zamień na:"
        };
        _replacementTextBox = new TextBox
        {
            Location = new Point(96, 41),
            Size = new Size(280, 23)
        };
        _matchCaseCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(15, 79),
            Text = "Uwzględniaj wielkość liter",
            Checked = matchCase
        };
        _wholeWordCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(15, 104),
            Text = "Tylko całe słowa",
            Checked = wholeWord
        };
        var findNextButton = new Button
        {
            Location = new Point(396, 10),
            Size = new Size(122, 25),
            Text = "Znajdź następny"
        };
        findNextButton.Click += (_, _) => FindNextRequested?.Invoke(this, EventArgs.Empty);
        var replaceButton = new Button
        {
            Location = new Point(396, 41),
            Size = new Size(122, 25),
            Text = "Zamień"
        };
        replaceButton.Click += (_, _) => ReplaceRequested?.Invoke(this, EventArgs.Empty);
        var replaceAllButton = new Button
        {
            Location = new Point(396, 72),
            Size = new Size(122, 25),
            Text = "Zamień wszystko",
            TextAlign = ContentAlignment.MiddleCenter
        };
        replaceAllButton.Click += (_, _) => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
        var closeButton = new Button
        {
            Location = new Point(396, 103),
            Size = new Size(122, 25),
            Text = "Anuluj"
        };
        closeButton.Click += (_, _) => Close();

        Controls.AddRange([
            findLabel, _queryTextBox, replaceLabel, _replacementTextBox, _matchCaseCheckBox, _wholeWordCheckBox,
            findNextButton, replaceButton, replaceAllButton, closeButton
        ]);
        AcceptButton = findNextButton;
        CancelButton = closeButton;
        Shown += (_, _) =>
        {
            _queryTextBox.Focus();
            _queryTextBox.SelectAll();
        };
    }

    public string Query => _queryTextBox.Text;

    public string Replacement => _replacementTextBox.Text;

    public bool MatchCase => _matchCaseCheckBox.Checked;

    public bool WholeWord => _wholeWordCheckBox.Checked;
}
