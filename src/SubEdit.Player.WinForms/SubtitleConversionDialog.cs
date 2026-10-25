namespace SubEdit.Player.WinForms;

internal enum SubtitleConversionScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleConversionDialog : SubEditDialog
{
    private readonly Dictionary<SubtitleTimingFormat, RadioButton> _formatButtons;
    private readonly Dictionary<SubtitleConversionScope, RadioButton> _scopeButtons;

    public SubtitleConversionDialog()
    {
        Text = "Konwersja formatu napisów";
        ClientSize = new Size(340, 320);

        var formatGroup = new GroupBox
        {
            Text = "Format docelowy",
            ForeColor = Color.Blue,
            Location = new Point(15, 10),
            Size = new Size(310, 166)
        };
        _formatButtons = new()
        {
            [SubtitleTimingFormat.TmPlayerLong] = AddOption(formatGroup, "00:00:00", "- czasowy", 20),
            [SubtitleTimingFormat.TmPlayerShort] = AddOption(formatGroup, "0:00:00", "- czasowy", 45),
            [SubtitleTimingFormat.MicroDvd] = AddOption(formatGroup, "{000}{000}", "- klatkowy, mDVD", 70),
            [SubtitleTimingFormat.Mpl2] = AddOption(formatGroup, "[000][000]", "- czasowy, MPL2", 95)
        };
        _formatButtons[SubtitleTimingFormat.MicroDvd].Checked = true;

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(15, 184),
            Size = new Size(310, 79)
        };
        _scopeButtons = new()
        {
            [SubtitleConversionScope.Selection] = AddScopeOption(scopeGroup, "Według zaznaczenia", 16),
            [SubtitleConversionScope.FromCurrentLine] = AddScopeOption(scopeGroup, "Od aktualnej linii", 36),
            [SubtitleConversionScope.ToCurrentLine] = AddScopeOption(scopeGroup, "Do aktualnej linii", 56)
        };
        _scopeButtons[SubtitleConversionScope.Selection].Checked = true;

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(92, 279), Size = new Size(75, 25) };
        var cancel = new Button { Text = "Anuluj", DialogResult = DialogResult.Cancel, Location = new Point(173, 279), Size = new Size(75, 25) };
        Controls.AddRange([formatGroup, scopeGroup, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public SubtitleTimingFormat SelectedFormat => _formatButtons.Single(pair => pair.Value.Checked).Key;

    public SubtitleConversionScope SelectedScope => _scopeButtons.Single(pair => pair.Value.Checked).Key;

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

    private static RadioButton AddScopeOption(Control parent, string label, int top)
    {
        var radio = new RadioButton
        {
            Text = label,
            ForeColor = SystemColors.ControlText,
            Location = new Point(10, top),
            AutoSize = true
        };
        parent.Controls.Add(radio);
        return radio;
    }
}
