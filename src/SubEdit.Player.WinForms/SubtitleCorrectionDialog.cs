using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal enum SubtitleCorrectionScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleCorrectionDialog : StandardDialog
{
    private readonly CheckBox _limitDisplayTime;
    private readonly CheckBox _fixPunctuation;
    private readonly CheckBox _fixDialogueDashes;
    private readonly RadioButton _dialogueDashWithSpace;
    private readonly RadioButton _dialogueDashWithoutSpace;
    private readonly CheckBox _removeEmptyLines;
    private readonly CheckBox _removeRepeatedLines;
    private readonly CheckBox _removeDoubleSpaces;
    private readonly CheckBox _trimLineSpaces;
    private readonly CheckBox _removeSingleDialogueDash;
    private readonly CheckBox _removeEllipses;
    private readonly CheckBox _removeSpacesBetweenDigits;
    private readonly CheckBox _removeColorInformation;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;
    private readonly CheckBox[] _operationCheckBoxes;
    private readonly ToolTip _toolTip = new();

    public SubtitleCorrectionDialog(SubtitleCorrectionSettings defaults) : base(440)
    {
        Text = "Popraw napisy";
        var operations = Page.AddSection("Operacje");
        _limitDisplayTime = operations.AddCheckBox("&Blokuj czas pokazywania napisu", defaults.LimitDisplayTime);
        _fixPunctuation = operations.AddCheckBox("Popraw pozycje znaków interpunkcyjnych", defaults.FixPunctuation);
        _fixDialogueDashes = operations.AddCheckBox("Popraw pozycję znaku myślnika (dialogi)", defaults.FixDialogueDashes);
        var dashes = operations.AddDependentSettings(_fixDialogueDashes, "Znaki dialogów");
        _dialogueDashWithoutSpace = dashes.AddRadioOption("|-aaa", isChecked: !defaults.AddSpaceAfterDialogueDash);
        _dialogueDashWithSpace = dashes.AddRadioOption("|- aaa", isChecked: defaults.AddSpaceAfterDialogueDash);
        _fixDialogueDashes.CheckedChanged += (_, _) => UpdateDialogueStyleAvailability();
        _removeEmptyLines = operations.AddCheckBox("&Usuwaj puste wiersze", defaults.RemoveEmptyLines);
        _removeRepeatedLines = operations.AddCheckBox("Usuwaj powtarzające się wiersze", defaults.RemoveRepeatedLines);
        _removeDoubleSpaces = operations.AddCheckBox("Usuwaj &podwójne spacje", defaults.RemoveDoubleSpaces);
        _trimLineSpaces = operations.AddCheckBox("Usuwaj spacje na &końcach wierszy", defaults.TrimLineSpaces);
        _removeSingleDialogueDash = operations.AddCheckBox("Usuwaj znak dialogu jeżeli jest jedynym w linii", defaults.RemoveSingleDialogueDash);
        _removeEllipses = operations.AddCheckBox("Usuwaj wielokropki w pojedynczych wierszach", defaults.RemoveEllipsesFromSingleLines);
        _removeSpacesBetweenDigits = operations.AddCheckBox("Usuwaj spacje między &cyframi", defaults.RemoveSpacesBetweenDigits);
        _removeColorInformation = operations.AddCheckBox("Usuwaj informacje o kolorze (mDVDp)", defaults.RemoveMicroDvdColorInformation);
        _operationCheckBoxes = [_limitDisplayTime, _fixPunctuation, _fixDialogueDashes, _removeEmptyLines, _removeRepeatedLines, _removeDoubleSpaces, _trimLineSpaces, _removeSingleDialogueDash, _removeEllipses, _removeSpacesBetweenDigits, _removeColorInformation];
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        _saveAsDefault = AddSaveDefault(); ConfigureActions(AcceptOptions);
        AddToolTips(); UpdateDialogueStyleAvailability();
        BindOptionToolTips(_toolTip);
        Shown += (_, _) => _limitDisplayTime.Focus();
    }

    public SubtitleCorrectionScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleCorrectionScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleCorrectionScope.ToCurrentLine
                : SubtitleCorrectionScope.Selection;

    public bool SaveAsDefault => _saveAsDefault.Checked;

    public SubtitleCorrectionSettings CreateSettings() => new()
    {
        LimitDisplayTime = _limitDisplayTime.Checked,
        FixPunctuation = _fixPunctuation.Checked,
        FixDialogueDashes = _fixDialogueDashes.Checked,
        AddSpaceAfterDialogueDash = _dialogueDashWithSpace.Checked,
        RemoveEmptyLines = _removeEmptyLines.Checked,
        RemoveRepeatedLines = _removeRepeatedLines.Checked,
        RemoveDoubleSpaces = _removeDoubleSpaces.Checked,
        TrimLineSpaces = _trimLineSpaces.Checked,
        RemoveSingleDialogueDash = _removeSingleDialogueDash.Checked,
        RemoveEllipsesFromSingleLines = _removeEllipses.Checked,
        RemoveSpacesBetweenDigits = _removeSpacesBetweenDigits.Checked,
        RemoveMicroDvdColorInformation = _removeColorInformation.Checked
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateDialogueStyleAvailability()
    {
        _dialogueDashWithSpace.Enabled = _fixDialogueDashes.Checked;
        _dialogueDashWithoutSpace.Enabled = _fixDialogueDashes.Checked;
    }

    private void AcceptOptions()
    {
        if (!_operationCheckBoxes.Any(checkBox => checkBox.Checked))
        {
            MessageBox.Show(
                this,
                "Nie wybrano żadnej operacji poprawiania napisów.",
                "Ostrzeżenie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _removeEmptyLines.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private void AddToolTips()
    {
        _toolTip.SetToolTip(_limitDisplayTime, "Czas schowania napisu nie przekroczy czasu pokazania następnej linii.");
        _toolTip.SetToolTip(_fixPunctuation, "Usuwa spacje przed znakami interpunkcyjnymi i porządkuje odstęp po nich.");
        _toolTip.SetToolTip(_fixDialogueDashes, "Ustawia myślnik dialogowy na początku wiersza.");
        _toolTip.SetToolTip(_trimLineSpaces, "Usuwa spacje na końcach wszystkich wierszy.");
        _toolTip.SetToolTip(_removeDoubleSpaces, "Usuwa podwójne i wielokrotne spacje.");
        _toolTip.SetToolTip(_removeEmptyLines, "Usuwa puste wiersze i napisy bez tekstu.");
        _toolTip.SetToolTip(_removeSpacesBetweenDigits, "Usuwa spacje znajdujące się między cyframi.");
        _toolTip.SetToolTip(_removeColorInformation, "Usuwa nawiasy klamrowe wraz z zawartością.");
        _toolTip.SetToolTip(_removeSingleDialogueDash, "Usuwa myślnik, jeśli występuje tylko w pierwszym wierszu.");
        _toolTip.SetToolTip(_removeEllipses, "Usuwa początkowy wielokropek z napisu jednowierszowego.");
        _toolTip.SetToolTip(_removeRepeatedLines, "Scala kolejne napisy o identycznym tekście.");
    }
}
