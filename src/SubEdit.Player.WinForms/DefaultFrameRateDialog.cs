using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class DefaultFrameRateDialog : SubEditDialog
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

    public DefaultFrameRateDialog(double defaultFrameRate, double? movieFrameRate)
    {
        Text = "Domyślna wartość współczynnika FPS";
        ClientSize = new Size(350, 205);

        var defaultGroup = new GroupBox
        {
            Text = "Wartość FPS używana przez program",
            Location = new Point(12, 86),
            Size = new Size(326, 70)
        };
        _frameRate = new ComboBox
        {
            Location = new Point(88, 28),
            Size = new Size(150, 23),
            MaxLength = 10,
            Sorted = true,
            Text = FormatFrameRate(defaultFrameRate)
        };
        _frameRate.Items.AddRange(CommonFrameRates);
        defaultGroup.Controls.Add(_frameRate);

        var movieGroup = new GroupBox
        {
            Text = "Wartość FPS filmu",
            Location = new Point(12, 12),
            Size = new Size(326, 63)
        };
        var movieFrameRateLabel = new LinkLabel
        {
            Text = movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "—",
            Location = new Point(12, 25),
            Size = new Size(302, 23),
            TextAlign = ContentAlignment.MiddleCenter,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Enabled = movieFrameRate is > 0d
        };
        movieFrameRateLabel.LinkClicked += (_, _) =>
        {
            if (movieFrameRate is > 0d)
            {
                _frameRate.Text = FormatFrameRate(movieFrameRate.Value);
            }
        };
        movieGroup.Controls.Add(movieFrameRateLabel);
        Controls.Add(movieGroup);
        Controls.Add(defaultGroup);

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(96, 170),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(179, 170),
            Size = new Size(75, 25)
        };
        Controls.AddRange([ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _frameRate.Focus();
            _frameRate.SelectAll();
        };
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
