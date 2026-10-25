using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleLineLimitScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleLineLimitDialog : SubEditDialog
{
    private readonly TextBox _maximumLines;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;

    public SubtitleLineLimitDialog(SubtitleLineLimitSettings defaults)
    {
        Text = "Ogranicz ilość wierszy w linii";
        ClientSize = new Size(340, 275);

        var limitGroup = new GroupBox
        {
            Text = "&Maksymalna ilość wierszy w linii:",
            Location = new Point(12, 12),
            Size = new Size(316, 76)
        };
        _maximumLines = new TextBox
        {
            Location = new Point(133, 31),
            Size = new Size(48, 23),
            MaxLength = 2,
            Text = defaults.MaximumLines.ToString(CultureInfo.InvariantCulture)
        };
        limitGroup.Controls.Add(_maximumLines);
        Controls.Add(limitGroup);

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(12, 100),
            Size = new Size(316, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;
        Controls.Add(scopeGroup);

        _saveAsDefault = new CheckBox
        {
            Text = "Zachowaj ustawienia jako domyślne",
            Location = new Point(15, 188),
            AutoSize = true
        };
        var ok = new Button
        {
            Text = "OK",
            Location = new Point(91, 238),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(172, 238),
            Size = new Size(75, 25)
        };
        Controls.AddRange([_saveAsDefault, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _maximumLines.Focus();
            _maximumLines.SelectAll();
        };
    }

    public int MaximumLines =>
        int.TryParse(_maximumLines.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 3;

    public SubtitleLineLimitScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleLineLimitScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleLineLimitScope.ToCurrentLine
                : SubtitleLineLimitScope.Selection;

    public bool SaveAsDefault => _saveAsDefault.Checked;

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

    private void AcceptOptions()
    {
        if (!int.TryParse(_maximumLines.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            MessageBox.Show(
                this,
                "Wpisz prawidłową maksymalną liczbę wierszy.",
                "Ostrzeżenie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _maximumLines.Focus();
            _maximumLines.SelectAll();
            return;
        }

        DialogResult = DialogResult.OK;
    }
}
