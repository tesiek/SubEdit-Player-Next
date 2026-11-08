using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class DefaultFrameRateDialog : StandardDialog
{
    private static readonly string[] CommonFrameRates =
    [
        "12.000",
        "14.998",
        "23.976",
        "24.000",
        "25.000",
        "29.970",
        "30.000"
    ];

    private readonly ComboBox _frameRate;

    public DefaultFrameRateDialog(double defaultFrameRate, double? movieFrameRate) : base(350)
    {
        Text = "Aktywny współczynnik FPS";
        var movie = Page.AddSection("Wartość FPS filmu");
        var link = new LinkLabel { Text = movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "—", AutoSize = true, Enabled = movieFrameRate is > 0d };
        movie.AddAdaptiveRow().AddControl(link);
        _frameRate = new ComboBox { MaxLength = 10, Sorted = true, Text = FormatFrameRate(defaultFrameRate) };
        _frameRate.Items.AddRange(CommonFrameRates);
        Page.AddSection("Wartość FPS używana przez program").AddField("FPS:", _frameRate, 150);
        link.LinkClicked += (_, _) => { if (movieFrameRate is > 0d) _frameRate.Text = FormatFrameRate(movieFrameRate.Value); };
        ConfigureActions(AcceptOptions);
        Shown += (_, _) => { _frameRate.Focus(); _frameRate.SelectAll(); };
    }

    public double FrameRate => TryParseFrameRate(_frameRate.Text, out var value) ? value : 0d;

    private void AcceptOptions()
    {
        if (!TryParseFrameRate(_frameRate.Text, out var frameRate) || frameRate <= 0d)
        {
            MessageBox.Show(
                this,
                "Wpisz prawidłową wartość współczynnika FPS.",
                "Ostrzeżenie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _frameRate.Focus();
            _frameRate.SelectAll();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private static bool TryParseFrameRate(string text, out double value) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
        double.IsFinite(value);

    private static string FormatFrameRate(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
