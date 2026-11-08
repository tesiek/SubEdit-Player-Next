namespace SubEdit.Player.WinForms;

internal enum SubtitleConversionScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleConversionDialog : StandardDialog
{
    private readonly Dictionary<SubtitleTimingFormat, RadioButton> _formatButtons = [];
    private readonly Dictionary<SubtitleConversionScope, RadioButton> _scopeButtons = [];
    public SubtitleConversionDialog() : base(390)
    {
        Text = "Konwersja formatu napisów";
        var formats = Page.AddSection("Format docelowy");
        foreach (var (format, caption, description) in new[] {
            (SubtitleTimingFormat.TmPlayerLong, "00:00:00", "- czasowy"), (SubtitleTimingFormat.TmPlayerShort, "0:00:00", "- czasowy"),
            (SubtitleTimingFormat.MicroDvd, "{000}{000}", "- klatkowy, mDVD"), (SubtitleTimingFormat.Mpl2, "[000][000]", "- czasowy, MPL2") })
            _formatButtons.Add(format, formats.AddRadioChoice(caption, description, format == SubtitleTimingFormat.MicroDvd));
        var scope = Page.AddSection("Obszar zmian napisów");
        _scopeButtons.Add(SubtitleConversionScope.Selection, scope.AddRadioOption("Według zaznaczenia", isChecked: true));
        _scopeButtons.Add(SubtitleConversionScope.FromCurrentLine, scope.AddRadioOption("Od aktualnej linii"));
        _scopeButtons.Add(SubtitleConversionScope.ToCurrentLine, scope.AddRadioOption("Do aktualnej linii"));
        ConfigureActions();
    }
    public SubtitleTimingFormat SelectedFormat => _formatButtons.Single(pair => pair.Value.Checked).Key;
    public SubtitleConversionScope SelectedScope => _scopeButtons.Single(pair => pair.Value.Checked).Key;
}
