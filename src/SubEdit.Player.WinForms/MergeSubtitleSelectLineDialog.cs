namespace SubEdit.Player.WinForms;

internal sealed class MergeSubtitleSelectLineDialog : StandardDialog
{
    private readonly ListBox _cueListBox;
    private readonly CheckBox? _ignoreCheckBox;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public MergeSubtitleSelectLineDialog(
        IReadOnlyList<string> displayItems,
        bool hasRememberedScale) : base(600)
    {
        Text = "Łączenie napisów - wybór aktualnego dialogu filmu";
        ConfigureActions();
        _okButton = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Ok);
        _cancelButton = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Cancel);
        _okButton.Enabled = false;
        var section = Page.AddSection("Wybór linii");
        section.AddText("Wybierz linię napisu odpowiadającą aktualnemu dialogowi w filmie:");
        _cueListBox = new ListBox { IntegralHeight = false, HorizontalScrollbar = true, Height = 240 };
        _cueListBox.Items.AddRange(displayItems.Cast<object>().ToArray());
        section.AddField("Linia:", _cueListBox, 550, SubEdit.Player.UI.DesignSystem.FieldWidthProfile.Fill);
        _cueListBox.DoubleClick += (_, _) => { if (_cueListBox.SelectedIndex >= 0) { DialogResult = DialogResult.OK; Close(); } };
        _cueListBox.SelectedIndexChanged += (_, _) => _okButton.Enabled = _cueListBox.SelectedIndex >= 0;
        if (_cueListBox.Items.Count > 0) _cueListBox.SelectedIndex = 0;
        if (hasRememberedScale)
        {
            var fps = Page.AddSection("Korekta FPS");
            fps.AddText("Dokonałeś zmiany współczynnika FPS w aktualnie edytowanych napisach.\r\nZmiana ta zostanie również wprowadzona automatycznie do napisów,\r\nktóre teraz dodajesz!");
            _ignoreCheckBox = fps.AddCheckBox("&Ignoruj");
        }
    }

    public int SelectedCueIndex => _cueListBox.SelectedIndex;

    public bool IgnoreRememberedScale => _ignoreCheckBox?.Checked ?? false;
}
