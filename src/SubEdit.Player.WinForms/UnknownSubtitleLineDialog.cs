namespace SubEdit.Player.WinForms;

internal enum UnknownSubtitleLineAction
{
    Break,
    TreatAsSubtitleText,
    IgnoreAll,
    DeleteAll
}

internal sealed class UnknownSubtitleLineDialog : StandardDialog
{
    private readonly Dictionary<UnknownSubtitleLineAction, RadioButton> _actionButtons;
    private readonly Bitmap _warningIcon = SystemIcons.Warning.ToBitmap();

    public UnknownSubtitleLineDialog(bool allowTreatAsSubtitleText = false) : base(540)
    {
        Text = "Błąd formatu w aktualnej linii";
        Page.AddMessage("Program znalazł napis o nieznanym formacie!", _warningIcon);
        var options = Page.AddSection("Dalsze działanie");
        _actionButtons = new()
        {
            [UnknownSubtitleLineAction.Break] = options.AddRadioOption("Przerwij, ustawiając kursor na nierozpoznanej linii napisu (zalecane)", isChecked: true),
            [UnknownSubtitleLineAction.TreatAsSubtitleText] = options.AddRadioOption("Kontynuuj, traktując nierozpoznane linie jako tekst napisu"),
            [UnknownSubtitleLineAction.IgnoreAll] = options.AddRadioOption("Kontynuuj, IGNORUJĄC wszystkie nierozpoznane linie"),
            [UnknownSubtitleLineAction.DeleteAll] = options.AddRadioOption("Kontynuuj, USUWAJĄC wszystkie nierozpoznane linie")
        };
        _actionButtons[UnknownSubtitleLineAction.TreatAsSubtitleText].Enabled = allowTreatAsSubtitleText;
        ConfigureActions();
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

}
