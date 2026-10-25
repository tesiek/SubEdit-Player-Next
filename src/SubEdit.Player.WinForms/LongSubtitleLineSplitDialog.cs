using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum LongSubtitleLineSplitScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class LongSubtitleLineSplitDialog : SubEditDialog
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

    public LongSubtitleLineSplitDialog(LongSubtitleLineSplitSettings defaults)
    {
        Text = "Podziel długie wiersze";
        ClientSize = new Size(390, 400);

        var optionsGroup = new GroupBox
        {
            Location = new Point(12, 12),
            Size = new Size(366, 222)
        };
        _splitLongLines = new RadioButton
        {
            Text = "&Podziel długie wiersze dla:",
            Location = new Point(12, 20),
            AutoSize = true,
            Checked = true
        };
        _removeSplitters = new RadioButton
        {
            Text = "&Usuń znaki podziału",
            Location = new Point(12, 194),
            AutoSize = true
        };

        var splitPanel = new GroupBox
        {
            Location = new Point(28, 45),
            Size = new Size(326, 139),
            Text = string.Empty
        };
        _removeExistingSplitters = new CheckBox
        {
            Text = "Przed podziałem usuń stare znaki podziału",
            Location = new Point(12, 10),
            AutoSize = true,
            Checked = defaults.RemoveExistingSplitters
        };
        _preserveBeforeDialogues = new CheckBox
        {
            Text = "Nie usuwaj przed dialogami",
            Location = new Point(30, 34),
            AutoSize = true,
            Checked = defaults.PreserveSplittersBeforeDialogues
        };
        var lengthLabel = new Label
        {
            Text = "&Wiersza dłuższego niż:",
            Location = new Point(62, 68),
            Size = new Size(165, 23),
            TextAlign = ContentAlignment.MiddleRight
        };
        _maximumLineLength = new TextBox
        {
            Location = new Point(235, 68),
            Size = new Size(48, 23),
            MaxLength = 3,
            Text = defaults.MaximumLineLength.ToString(CultureInfo.InvariantCulture)
        };
        lengthLabel.Click += (_, _) => _maximumLineLength.Focus();
        _limitLineCount = new CheckBox
        {
            Text = "&Nie przekrocz liczby wierszy:",
            Location = new Point(48, 104),
            Size = new Size(180, 23),
            Checked = defaults.LimitLineCount
        };
        _maximumLines = new TextBox
        {
            Location = new Point(235, 101),
            Size = new Size(48, 23),
            MaxLength = 2,
            Text = defaults.MaximumLines.ToString(CultureInfo.InvariantCulture)
        };
        splitPanel.Controls.AddRange([
            _removeExistingSplitters,
            _preserveBeforeDialogues,
            lengthLabel,
            _maximumLineLength,
            _limitLineCount,
            _maximumLines
        ]);
        optionsGroup.Controls.AddRange([_splitLongLines, splitPanel, _removeSplitters]);
        Controls.Add(optionsGroup);

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(12, 246),
            Size = new Size(366, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;
        Controls.Add(scopeGroup);

        _saveAsDefault = new CheckBox
        {
            Text = "Zachowaj ustawienia jako domyślne",
            Location = new Point(12, 334),
            AutoSize = true
        };
        var ok = new Button
        {
            Text = "OK",
            Location = new Point(114, 365),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(195, 365),
            Size = new Size(75, 25)
        };
        Controls.AddRange([_saveAsDefault, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;

        _splitLongLines.CheckedChanged += (_, _) => UpdateAvailability(splitPanel);
        _removeExistingSplitters.CheckedChanged += (_, _) => UpdateAvailability(splitPanel);
        _limitLineCount.CheckedChanged += (_, _) => UpdateAvailability(splitPanel);
        UpdateAvailability(splitPanel);
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

    private static RadioButton AddScopeOption(Control parent, string text, int top)
    {
        var option = new RadioButton
        {
            Text = text,
            ForeColor = SystemColors.ControlText,
            Location = new Point(8, top),
            AutoSize = true
        };
        parent.Controls.Add(option);
        return option;
    }

    private void UpdateAvailability(Control splitPanel)
    {
        splitPanel.Enabled = _splitLongLines.Checked;
        _preserveBeforeDialogues.Enabled = _splitLongLines.Checked && _removeExistingSplitters.Checked;
        _maximumLines.Enabled = _splitLongLines.Checked && _limitLineCount.Checked;
    }

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
