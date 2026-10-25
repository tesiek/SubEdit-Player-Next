using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal enum SubtitleCorrectionScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleCorrectionDialog : SubEditDialog
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

    public SubtitleCorrectionDialog(SubtitleCorrectionSettings defaults)
    {
        Text = "Popraw napisy";
        ClientSize = new Size(340, 412);

        _limitDisplayTime = AddCheckBox("&Blokuj czas pokazywania napisu", 16, defaults.LimitDisplayTime);
        _fixPunctuation = AddCheckBox("Popraw pozycje znaków interpunkcyjnych", 36, defaults.FixPunctuation);
        _fixDialogueDashes = AddCheckBox("Popraw pozycję znaku myślnika (dialogi)", 56, defaults.FixDialogueDashes);
        _fixDialogueDashes.CheckedChanged += (_, _) => UpdateDialogueStyleAvailability();

        var dialogueStylePanel = new Panel
        {
            Location = new Point(145, 76),
            Size = new Size(120, 20)
        };
        _dialogueDashWithoutSpace = new RadioButton
        {
            Text = "|-aaa",
            Location = new Point(2, 0),
            Padding = new Padding(4, 0, 0, 0),
            AutoSize = true,
            Checked = !defaults.AddSpaceAfterDialogueDash
        };
        _dialogueDashWithSpace = new RadioButton
        {
            Text = "|- aaa",
            Location = new Point(55, 0),
            Padding = new Padding(4, 0, 0, 0),
            AutoSize = true,
            Checked = defaults.AddSpaceAfterDialogueDash
        };
        dialogueStylePanel.Controls.AddRange([_dialogueDashWithoutSpace, _dialogueDashWithSpace]);
        Controls.Add(dialogueStylePanel);

        _removeEmptyLines = AddCheckBox("&Usuwaj puste wiersze", 99, defaults.RemoveEmptyLines);
        _removeRepeatedLines = AddCheckBox("Usuwaj powtarzające się wiersze", 119, defaults.RemoveRepeatedLines);
        _removeDoubleSpaces = AddCheckBox("Usuwaj &podwójne spacje", 139, defaults.RemoveDoubleSpaces);
        _trimLineSpaces = AddCheckBox("Usuwaj spacje na &końcach wierszy", 159, defaults.TrimLineSpaces);
        _removeSingleDialogueDash = AddCheckBox(
            "Usuwaj znak dialogu jeżeli jest jedynym w linii",
            179,
            defaults.RemoveSingleDialogueDash);
        _removeEllipses = AddCheckBox(
            "Usuwaj wielokropki w pojedynczych wierszach",
            199,
            defaults.RemoveEllipsesFromSingleLines);
        _removeSpacesBetweenDigits = AddCheckBox(
            "Usuwaj spacje między &cyframi",
            219,
            defaults.RemoveSpacesBetweenDigits);
        _removeColorInformation = AddCheckBox(
            "Usuwaj informacje o kolorze (mDVDp)",
            239,
            defaults.RemoveMicroDvdColorInformation);

        _operationCheckBoxes =
        [
            _limitDisplayTime,
            _fixPunctuation,
            _fixDialogueDashes,
            _removeEmptyLines,
            _removeRepeatedLines,
            _removeDoubleSpaces,
            _trimLineSpaces,
            _removeSingleDialogueDash,
            _removeEllipses,
            _removeSpacesBetweenDigits,
            _removeColorInformation
        ];

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(15, 267),
            Size = new Size(310, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;
        Controls.Add(scopeGroup);

        _saveAsDefault = new CheckBox
        {
            Text = "Zachowaj ustawienia jako domyślne",
            Location = new Point(18, 355),
            AutoSize = true
        };

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(92, 379),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(173, 379),
            Size = new Size(75, 25)
        };
        Controls.AddRange([_saveAsDefault, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;

        AddToolTips();
        UpdateDialogueStyleAvailability();
        Shown += (_, _) => _limitDisplayTime.Focus();
    }

    public SubtitleCorrectionScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleCorrectionScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleCorrectionScope.ToCurrentLine
                : SubtitleCorrectionScope.Selection;

    public bool SaveAsDefault => _saveAsDefault.Checked;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Auto-sized radio buttons can outgrow the original 20-pixel row when text is enlarged.
        var dialogueStylePanel = _dialogueDashWithoutSpace.Parent!;
        var gap = _dialogueDashWithoutSpace.Padding.Left;
        _dialogueDashWithSpace.Left = Math.Max(_dialogueDashWithSpace.Left, _dialogueDashWithoutSpace.Right + gap);
        dialogueStylePanel.Width = Math.Max(dialogueStylePanel.Width, _dialogueDashWithSpace.Right + gap);
        dialogueStylePanel.Height = Math.Max(dialogueStylePanel.Height,
            Math.Max(_dialogueDashWithoutSpace.Bottom, _dialogueDashWithSpace.Bottom));
        var extraHeight = Math.Max(0, dialogueStylePanel.Bottom - _removeEmptyLines.Top);
        if (extraHeight == 0)
        {
            return;
        }

        foreach (var control in Controls.Cast<Control>().Where(control => control.Top >= _removeEmptyLines.Top).ToArray())
        {
            control.Top += extraHeight;
        }
        ClientSize = new Size(ClientSize.Width, ClientSize.Height + extraHeight);
    }

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

    private CheckBox AddCheckBox(string text, int top, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Location = new Point(18, top),
            AutoSize = true,
            Checked = isChecked
        };
        Controls.Add(checkBox);
        return checkBox;
    }

    private static RadioButton AddScopeOption(Control parent, string text, int top)
    {
        var radioButton = new RadioButton
        {
            Text = text,
            ForeColor = SystemColors.ControlText,
            Location = new Point(8, top),
            AutoSize = true
        };
        parent.Controls.Add(radioButton);
        return radioButton;
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
