using System.Globalization;
using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;
internal sealed class FindLongSubtitleLineDialog : StandardDialog
{
    private readonly TextBox _lengthTextBox;
    public FindLongSubtitleLineDialog(int initialLength) : base(330)
    {
        Text = "Znajdź długi wiersz";
        _lengthTextBox = new TextBox { MaxLength = 10, Text = Math.Max(1, initialLength).ToString(CultureInfo.InvariantCulture) };
        Page.AddSection("Dłuższy niż").AddField("&Znaków:", _lengthTextBox, widthProfile: FieldWidthProfile.Compact, widthSample: "99999");
        ConfigureActions(AcceptValue);
        Adaptive.PreferredClientWidthProvider = () => Math.Max(UiDesignTokens.Pixels(330, DeviceDpi), Page.MeasureSmallFormWidth() + 2 * Shell.Tokens.Inset(Shell.Profile, DeviceDpi));
        Shown += (_, _) => { _lengthTextBox.Focus(); _lengthTextBox.SelectAll(); };
    }
    public int MinimumLength { get; private set; }

    private void AcceptValue()
    {
        if (!int.TryParse(
                _lengthTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value) ||
            value <= 0)
        {
            MessageBox.Show(
                this,
                "Podaj liczbę większą od zera.",
                "Nieprawidłowa wartość",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _lengthTextBox.Focus();
            _lengthTextBox.SelectAll();
            return;
        }

        MinimumLength = value;
        DialogResult = DialogResult.OK;
    }
}
