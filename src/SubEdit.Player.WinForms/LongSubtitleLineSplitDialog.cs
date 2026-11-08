using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum LongSubtitleLineSplitScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class LongSubtitleLineSplitDialog : StandardDialog
{
    private readonly RadioButton _splitLongLines;
    private readonly RadioButton _removeSplitters;
    private readonly CheckBox _removeExistingSplitters;
    private readonly CheckBox _preserveBeforeDialogues;
    private readonly TextBox _maximumLineLength;
    private readonly CheckBox _limitLineCount;
    private readonly TextBox _maximumLines;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;

    public LongSubtitleLineSplitDialog(LongSubtitleLineSplitSettings defaults) : base(510)
    {
        Text = "Podziel długie wiersze";
        var operation = Page.AddSection("Sposób działania");
        _splitLongLines = operation.AddRadioOption("&Podziel długie wiersze dla:", isChecked: true);
        var split = operation.AddDependentSettings(_splitLongLines, "Ustawienia podziału");
        _removeExistingSplitters = split.AddCheckBox("Przed podziałem usuń stare znaki podziału", defaults.RemoveExistingSplitters);
        _preserveBeforeDialogues = split.AddCheckBox("Nie usuwaj przed dialogami", defaults.PreserveSplittersBeforeDialogues, dependsOn: _removeExistingSplitters);
        _maximumLineLength = new TextBox { Text = defaults.MaximumLineLength.ToString(CultureInfo.InvariantCulture), MaxLength = 3 };
        split.AddField("&Wiersza dłuższego niż:", _maximumLineLength, widthProfile: SubEdit.Player.UI.DesignSystem.FieldWidthProfile.Compact, widthSample: "999");
        _maximumLines = new TextBox { Text = defaults.MaximumLines.ToString(CultureInfo.InvariantCulture), MaxLength = 2 };
        _limitLineCount = split.AddCheckField("&Nie przekrocz liczby wierszy:", _maximumLines, defaults.LimitLineCount);
        _removeSplitters = operation.AddRadioOption("&Usuń znaki podziału");
        var scope = Page.AddSection("Obszar zmian napisów");
        _selectionScope = scope.AddRadioOption("Według zaznaczenia", isChecked: true);
        _fromCurrentLineScope = scope.AddRadioOption("Od aktualnej linii");
        _toCurrentLineScope = scope.AddRadioOption("Do aktualnej linii");
        _saveAsDefault = Page.AddSection("Zapamiętanie ustawień").AddCheckBox("Zachowaj ustawienia jako domyślne");
        ConfigureActions(AcceptOptions);
        Shown += (_, _) => _maximumLineLength.Focus();
    }

    public bool SplitLongLines => _splitLongLines.Checked;

    public LongSubtitleLineSplitScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? LongSubtitleLineSplitScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? LongSubtitleLineSplitScope.ToCurrentLine
                : LongSubtitleLineSplitScope.Selection;

    public bool SaveAsDefault => _saveAsDefault.Checked;

    public LongSubtitleLineSplitSettings CreateSettings() => new()
    {
        MaximumLineLength = ParsePositiveInteger(_maximumLineLength, 40),
        LimitLineCount = _limitLineCount.Checked,
        MaximumLines = ParsePositiveInteger(_maximumLines, 3),
        RemoveExistingSplitters = _removeExistingSplitters.Checked,
        PreserveSplittersBeforeDialogues = _preserveBeforeDialogues.Checked
    };

    private void AcceptOptions()
    {
        if (_splitLongLines.Checked &&
            (!ValidatePositiveInteger(_maximumLineLength, "Wpisz prawidłową maksymalną długość wiersza.") ||
             (_limitLineCount.Checked && !ValidatePositiveInteger(_maximumLines, "Wpisz prawidłową maksymalną liczbę wierszy."))))
        {
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private bool ValidatePositiveInteger(TextBox textBox, string message)
    {
        if (int.TryParse(textBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
        {
            return true;
        }

        MessageBox.Show(this, message, "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        textBox.Focus();
        textBox.SelectAll();
        return false;
    }

    private static int ParsePositiveInteger(TextBox textBox, int fallback) =>
        int.TryParse(textBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
}
