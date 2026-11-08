using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum RapidSubtitleMergeScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class RapidSubtitleMergeDialog : StandardDialog
{
    private readonly TextBox _fiftyCharactersSeconds;
    private readonly TextBox _firstToLastEndSeconds;
    private readonly TextBox _firstToLastStartSeconds;
    private readonly RadioButton _addAsConsecutiveLines;
    private readonly RadioButton _limitLineCount;
    private readonly TextBox _maximumLines;
    private readonly CheckBox _limitCharacters;
    private readonly TextBox _maximumCharacters;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;

    public RapidSubtitleMergeDialog(RapidSubtitleMergeSettings defaults) : base(530)
    {
        Text = "Połącz szybko znikające napisy";
        var timing = Page.AddSection("Czas wyświetlania");
        _fiftyCharactersSeconds = Input(FormatNumber(defaults.MaximumFiftyCharacterDisplaySeconds), 3);
        _firstToLastEndSeconds = Input(FormatNumber(defaults.MaximumFirstToLastEndSeconds), 3);
        _firstToLastStartSeconds = Input(FormatNumber(defaults.MaximumFirstToLastStartSeconds), 3);
        timing.AddFieldWithUnit("Złącz napis jeżeli czas wyświetlenia 50 znaków, tego napisu jest mniejszy niż", _fiftyCharactersSeconds, "sekund");
        timing.AddFieldWithUnit("a czas między wyświetleniem pierwszego napisu a schowaniem ostatniego jest mniejszy niż", _firstToLastEndSeconds, "sekund");
        timing.AddFieldWithUnit("lub czas między wyświetleniami pierwszego i ostatniego napisu jest mniejszy niż", _firstToLastStartSeconds, "sekund");
        var lines = Page.AddSection("Sposób tworzenia wierszy nowego napisu");
        _addAsConsecutiveLines = lines.AddRadioOption("Dodaj jako kolejne wiersze", isChecked: defaults.AddAsConsecutiveLines);
        _limitLineCount = lines.AddRadioOption("Złącz tak, żeby nie było więcej niż", isChecked: !defaults.AddAsConsecutiveLines);
        var limit = lines.AddDependentSettings(_limitLineCount, "Limit wierszy");
        _maximumLines = Input(defaults.MaximumLines.ToString(CultureInfo.InvariantCulture), 1);
        limit.AddFieldWithUnit("Maksymalnie:", _maximumLines, "wierszy");
        _maximumCharacters = Input(defaults.MaximumCharacters.ToString(CultureInfo.InvariantCulture), 3);
        var characters = Page.AddSection("Długość napisu").AddAdaptiveRow();
        _limitCharacters = characters.AddCheckBox("Nie łącz jeżeli długość napisu przekroczy", defaults.LimitCharacters);
        characters.AddControl(new SubEdit.Player.UI.DesignSystem.FieldValueLayout(_maximumCharacters, "znaków.", keepTogether: true));
        _addAsConsecutiveLines.CheckedChanged += (_, _) => UpdateAvailability();
        _limitLineCount.CheckedChanged += (_, _) => UpdateAvailability();
        _limitCharacters.CheckedChanged += (_, _) => UpdateAvailability();
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        _saveAsDefault = AddSaveDefault(); ConfigureActions(AcceptOptions); UpdateAvailability();
        Shown += (_, _) => { _fiftyCharactersSeconds.Focus(); _fiftyCharactersSeconds.SelectAll(); };
    }

    public bool SaveAsDefault => _saveAsDefault.Checked;

    public RapidSubtitleMergeScope SelectedScope => _fromCurrentLineScope.Checked
        ? RapidSubtitleMergeScope.FromCurrentLine
        : _toCurrentLineScope.Checked ? RapidSubtitleMergeScope.ToCurrentLine : RapidSubtitleMergeScope.Selection;

    public RapidSubtitleMergeSettings CreateSettings() => new()
    {
        MaximumFiftyCharacterDisplaySeconds = ParseNumber(_fiftyCharactersSeconds.Text),
        MaximumFirstToLastEndSeconds = ParseNumber(_firstToLastEndSeconds.Text),
        MaximumFirstToLastStartSeconds = ParseNumber(_firstToLastStartSeconds.Text),
        AddAsConsecutiveLines = _addAsConsecutiveLines.Checked,
        MaximumLines = int.Parse(_maximumLines.Text, CultureInfo.InvariantCulture),
        LimitCharacters = _limitCharacters.Checked,
        MaximumCharacters = int.Parse(_maximumCharacters.Text, CultureInfo.InvariantCulture)
    };

    private void UpdateAvailability()
    {
        _maximumLines.Enabled = _limitLineCount.Checked;
        _maximumCharacters.Enabled = _limitCharacters.Checked;
    }

    private void AcceptOptions()
    {
        if (!IsPositiveNumber(_fiftyCharactersSeconds) ||
            !IsPositiveNumber(_firstToLastEndSeconds) ||
            !IsPositiveNumber(_firstToLastStartSeconds) ||
            !IsPositiveInteger(_maximumLines, 1, 9) ||
            (_limitCharacters.Checked && !IsPositiveInteger(_maximumCharacters, 1, 999)))
        {
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private bool IsPositiveNumber(TextBox textBox)
    {
        if (TryParseNumber(textBox.Text, out var value) && value is >= 0.1d and <= 999d)
        {
            return true;
        }
        ShowValidation(textBox, "Wpisz wartość z zakresu 0,1–999.");
        return false;
    }

    private bool IsPositiveInteger(TextBox textBox, int minimum, int maximum)
    {
        if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum &&
            value <= maximum)
        {
            return true;
        }
        ShowValidation(textBox, $"Wpisz wartość z zakresu {minimum}–{maximum}.");
        return false;
    }

    private void ShowValidation(TextBox textBox, string message)
    {
        MessageBox.Show(this, message, "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        textBox.Focus();
        textBox.SelectAll();
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double ParseNumber(string text) => TryParseNumber(text, out var value) ? value : 0d;
    private static string FormatNumber(double value) => value.ToString("0.########", CultureInfo.CurrentCulture);
}
