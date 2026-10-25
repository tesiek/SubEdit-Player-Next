namespace SubEdit.Player.WinForms;

internal sealed class MergeSubtitleSelectLineDialog : SubEditDialog
{
    private readonly ListBox _cueListBox;
    private readonly CheckBox? _ignoreCheckBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public MergeSubtitleSelectLineDialog(
        IReadOnlyList<string> displayItems,
        bool hasRememberedScale)
    {
        Text = "Łączenie napisów - wybór aktualnego dialogu filmu";
        ClientSize = new Size(600, hasRememberedScale ? 460 : 360);

        _okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 26),
            Enabled = false
        };

        _cancelButton = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Size = new Size(80, 26)
        };

        var promptLabel = new Label
        {
            Text = "Wybierz linię napisu odpowiadającą aktualnemu dialogowi w filmie:",
            Location = new Point(15, 12),
            Size = new Size(570, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(promptLabel);

        _cueListBox = new ListBox
        {
            Location = new Point(15, 36),
            Size = new Size(570, 240),
            IntegralHeight = false,
            HorizontalScrollbar = true
        };

        foreach (var item in displayItems)
        {
            _cueListBox.Items.Add(item);
        }

        _cueListBox.DoubleClick += (_, _) =>
        {
            if (_cueListBox.SelectedIndex >= 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        _cueListBox.SelectedIndexChanged += (_, _) =>
        {
            _okButton.Enabled = _cueListBox.SelectedIndex >= 0;
        };

        if (_cueListBox.Items.Count > 0)
        {
            _cueListBox.SelectedIndex = 0;
        }

        Controls.Add(_cueListBox);

        var nextControlTop = _cueListBox.Bottom + 12;

        if (hasRememberedScale)
        {
            var fpsGroup = new GroupBox
            {
                Text = "Korekta FPS",
                Location = new Point(15, nextControlTop),
                Size = new Size(570, 85)
            };

            var infoLabel = new Label
            {
                Text = "Dokonałeś zmiany współczynnika FPS w aktualnie edytowanych napisach.\r\n" +
                       "Zmiana ta zostanie również wprowadzona automatycznie do napisów,\r\n" +
                       "które teraz dodajesz!",
                Location = new Point(12, 20),
                Size = new Size(460, 52),
                TextAlign = ContentAlignment.MiddleLeft
            };
            fpsGroup.Controls.Add(infoLabel);

            _ignoreCheckBox = new CheckBox
            {
                Text = "&Ignoruj",
                Checked = false,
                Location = new Point(480, 32),
                Size = new Size(80, 24),
                UseVisualStyleBackColor = true
            };
            fpsGroup.Controls.Add(_ignoreCheckBox);

            Controls.Add(fpsGroup);
            nextControlTop = fpsGroup.Bottom + 15;
        }

        _okButton.Location = new Point(215, nextControlTop);
        _cancelButton.Location = new Point(305, nextControlTop);

        Controls.Add(_okButton);
        Controls.Add(_cancelButton);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    public int SelectedCueIndex => _cueListBox.SelectedIndex;

    public bool IgnoreRememberedScale => _ignoreCheckBox?.Checked ?? false;
}
