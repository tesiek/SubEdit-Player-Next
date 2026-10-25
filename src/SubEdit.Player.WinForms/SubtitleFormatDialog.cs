namespace SubEdit.Player.WinForms;

internal sealed class SubtitleFormatDialog : SubEditDialog
{
    private readonly Dictionary<SubtitleTimingFormat, RadioButton> _buttons;

    public SubtitleFormatDialog(SubtitleTimingFormat selectedFormat)
    {
        Text = "Aktywny format napisów";
        ClientSize = new Size(340, 218);

        var group = new GroupBox
        {
            Text = "Format docelowy",
            ForeColor = Color.Blue,
            Location = new Point(15, 10),
            Size = new Size(310, 156)
        };
        _buttons = new()
        {
            [SubtitleTimingFormat.TmPlayerLong] = AddOption(group, "00:00:00", "- czasowy", 20),
            [SubtitleTimingFormat.TmPlayerShort] = AddOption(group, "0:00:00", "- czasowy", 45),
            [SubtitleTimingFormat.MicroDvd] = AddOption(group, "{000}{000}", "- klatkowy, mDVD", 70),
            [SubtitleTimingFormat.Mpl2] = AddOption(group, "[000][000]", "- czasowy, MPL2", 95)
        };
        _buttons[selectedFormat].Checked = true;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(92, 177), Size = new Size(75, 25) };
        var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(173, 177), Size = new Size(75, 25) };
        Controls.AddRange([group, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public SubtitleTimingFormat SelectedFormat => _buttons.Single(pair => pair.Value.Checked).Key;

    private static RadioButton AddOption(Control parent, string label, string description, int top)
    {
        var radio = new RadioButton
        {
            Text = label,
            ForeColor = SystemColors.ControlText,
            Location = new Point(15, top),
            AutoSize = true
        };
        parent.Controls.Add(radio);
        parent.Controls.Add(new Label
        {
            Text = description,
            ForeColor = SystemColors.ControlText,
            Location = new Point(145, top + 2),
            AutoSize = true
        });
        return radio;
    }
}
