namespace SubEdit.Player.WinForms;

internal enum UnknownSubtitleLineAction
{
    Break,
    TreatAsSubtitleText,
    IgnoreAll,
    DeleteAll
}

internal sealed class UnknownSubtitleLineDialog : SubEditDialog
{
    private readonly Dictionary<UnknownSubtitleLineAction, RadioButton> _actionButtons;
    private readonly Bitmap _warningIcon = SystemIcons.Warning.ToBitmap();

    public UnknownSubtitleLineDialog(bool allowTreatAsSubtitleText = false)
    {
        Text = "Błąd formatu w aktualnej linii";
        ClientSize = new Size(540, 210);

        var icon = new PictureBox
        {
            Image = _warningIcon,
            Location = new Point(15, 15),
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.AutoSize
        };
        var message = new Label
        {
            Text = "Program znalazł napis o nieznanym formacie!",
            Location = new Point(65, 24),
            AutoSize = true
        };

        _actionButtons = new()
        {
            [UnknownSubtitleLineAction.Break] = AddOption(
                "Przerwij, ustawiając kursor na nierozpoznanej linii napisu (zalecane)", 55),
            [UnknownSubtitleLineAction.TreatAsSubtitleText] = AddOption(
                "Kontynuuj, traktując nierozpoznane linie jako tekst napisu",
                80,
                enabled: allowTreatAsSubtitleText),
            [UnknownSubtitleLineAction.IgnoreAll] = AddOption(
                "Kontynuuj, IGNORUJĄC wszystkie nierozpoznane linie", 105),
            [UnknownSubtitleLineAction.DeleteAll] = AddOption(
                "Kontynuuj, USUWAJĄC wszystkie nierozpoznane linie", 130)
        };
        _actionButtons[UnknownSubtitleLineAction.Break].Checked = true;

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(190, 169),
            Size = new Size(75, 25)
        };
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(271, 169),
            Size = new Size(75, 25)
        };

        Controls.Add(icon);
        Controls.Add(message);
        Controls.AddRange(_actionButtons.Values.ToArray());
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public UnknownSubtitleLineAction SelectedAction =>
        _actionButtons.Single(pair => pair.Value.Checked).Key;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _warningIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private static RadioButton AddOption(string text, int top, bool enabled = true) => new()
    {
        Text = text,
        Location = new Point(65, top),
        AutoSize = true,
        Enabled = enabled
    };
}
