namespace SubEdit.Player.WinForms;

internal sealed class QuestionDialog : SubEditDialog
{
    public QuestionDialog(string message, bool includeCancel, string yesText = "Tak", string noText = "Nie")
    {
        var isExtendedMessage = message.Count(character => character == '\n') >= 3;
        var isCompactMessage = !isExtendedMessage &&
                               TextRenderer.MeasureText(message, Font).Width <= 220;
        Text = "Pytanie";
        ClientSize = isExtendedMessage
            ? new Size(372, 150)
            : new Size(isCompactMessage ? 300 : 360, 137);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowOnly;

        var questionIcon = new PictureBox
        {
            Image = SystemIcons.Question.ToBitmap(),
            Location = new Point(21, isExtendedMessage ? 20 : 25),
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        var messageLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(3, 3, 21, 3),
            Location = new Point(64, isExtendedMessage ? 17 : 24),
            Text = message
        };

        var buttonPanel = new Panel
        {
            BackColor = SystemColors.Control,
            Dock = DockStyle.Bottom,
            Height = isExtendedMessage ? 44 : 48
        };

        var yesButton = CreateButton(
            yesText,
            DialogResult.Yes,
            isCompactMessage ? 74 : includeCancel ? 94 : isExtendedMessage ? 202 : 174);
        var noButton = CreateButton(
            noText,
            DialogResult.No,
            isCompactMessage ? 154 : includeCancel ? 174 : isExtendedMessage ? 284 : 254);
        buttonPanel.Controls.AddRange([yesButton, noButton]);

        if (includeCancel)
        {
            var cancelButton = CreateButton("Anuluj", DialogResult.Cancel, 254);
            buttonPanel.Controls.Add(cancelButton);
            CancelButton = cancelButton;
        }
        else
        {
            CancelButton = noButton;
        }

        Controls.Add(questionIcon);
        Controls.Add(messageLabel);
        Controls.Add(buttonPanel);

        AcceptButton = yesButton;
    }

    private static Button CreateButton(string text, DialogResult result, int left)
    {
        return new Button
        {
            DialogResult = result,
            Location = new Point(left, 12),
            Size = new Size(72, 25),
            Text = text,
            UseVisualStyleBackColor = true
        };
    }
}
