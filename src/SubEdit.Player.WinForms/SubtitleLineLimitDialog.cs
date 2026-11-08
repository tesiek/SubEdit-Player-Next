using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleLineLimitScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleLineLimitDialog : StandardDialog
{
    private readonly TextBox _maximumLines;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;

    public SubtitleLineLimitDialog(SubtitleLineLimitSettings defaults) : base(370)
    {
        Text = "Ogranicz ilość wierszy w linii";
        _maximumLines = Input(defaults.MaximumLines.ToString(CultureInfo.InvariantCulture), 2);
        Page.AddSection("Limit wierszy").AddField("&Maksymalna ilość wierszy w linii:", _maximumLines,
            widthProfile: SubEdit.Player.UI.DesignSystem.FieldWidthProfile.Compact, widthSample: "99");
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        _saveAsDefault = AddSaveDefault(); ConfigureActions(AcceptOptions);
        Shown += (_, _) => { _maximumLines.Focus(); _maximumLines.SelectAll(); };
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
