using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class FindLongSubtitleLineDialog : SubEditDialog
{
    private readonly TextBox _lengthTextBox;

    public FindLongSubtitleLineDialog(int initialLength)
    {
        Text = "Znajdź długi wiersz";
        ClientSize = new Size(330, 155);

        var groupBox = new GroupBox
        {
            Text = "Dłuższy niż",
            Location = new Point(12, 10),
            Size = new Size(306, 82)
        };
        var label = new Label
        {
            Text = "&Znaków:",
            Location = new Point(18, 33),
            Size = new Size(75, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        _lengthTextBox = new TextBox
        {
            Location = new Point(99, 33),
            Size = new Size(175, 23),
            MaxLength = 10,
            Text = Math.Max(1, initialLength).ToString(CultureInfo.InvariantCulture)
        };
        label.UseMnemonic = true;
        label.TabIndex = 0;
        _lengthTextBox.TabIndex = 1;
        groupBox.Controls.AddRange([label, _lengthTextBox]);

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(87, 112),
            Size = new Size(75, 25),
            TabIndex = 2
        };
        okButton.Click += (_, _) => AcceptValue();
        var cancelButton = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(168, 112),
            Size = new Size(75, 25),
            TabIndex = 3
        };

        Controls.AddRange([groupBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        Shown += (_, _) =>
        {
            _lengthTextBox.Focus();
            _lengthTextBox.SelectAll();
        };
    }

    public int MinimumLength { get; private set; }

    private void AcceptValue()
    {
        if (!int.TryParse(
                _lengthTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) ||
            value <= 0)
        {
            MessageBox.Show(
                this,
                "Podaj liczbę większą od zera.",
                "Nieprawidłowa wartość",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _lengthTextBox.Focus();
            _lengthTextBox.SelectAll();
            return;
        }

        MinimumLength = value;
        DialogResult = DialogResult.OK;
    }
}
