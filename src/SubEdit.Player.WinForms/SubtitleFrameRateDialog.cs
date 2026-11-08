using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleFrameRateScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleFrameRateDialog : StandardDialog
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

    private readonly ComboBox _currentFrameRate;
    private readonly ComboBox _targetFrameRate;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly ToolTip _toolTip = new();

    public SubtitleFrameRateDialog(double? movieFrameRate) : base(400)
    {
        Text = "Zmień współczynnik FPS napisów";
        var fps = Page.AddSection("Ilość klatek na sekundę");
        _currentFrameRate = CreateFrameRateCombo("23.976");
        _targetFrameRate = CreateFrameRateCombo(movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "25.000");
        fps.AddField("&Aktualna:", _currentFrameRate, 150); fps.AddField("&Docelowa:", _targetFrameRate, 150);
        var link = new LinkLabel { Text = movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "—", AutoSize = true, Enabled = movieFrameRate is > 0d };
        fps.AddAdaptiveRow().AddField("FPS filmu:", link);
        link.LinkClicked += (_, _) => { if (movieFrameRate is > 0d) _targetFrameRate.Text = FormatFrameRate(movieFrameRate.Value); };
        _toolTip.SetToolTip(link, movieFrameRate is > 0d ? "Kliknij aby wpisać tą wartość" : "Współczynnik FPS nieosiągalny (brak otwartego filmu)");
        fps.AddText("funkcja zmiany FPS działa tylko dla formatu mDVD");
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        ConfigureActions(AcceptOptions);
        BindOptionToolTips(_toolTip);
        Shown += (_, _) => { _currentFrameRate.Focus(); _currentFrameRate.SelectAll(); };
    }

    public double CurrentFrameRate => ParseFrameRate(_currentFrameRate.Text);

    public double TargetFrameRate => ParseFrameRate(_targetFrameRate.Text);

    public SubtitleFrameRateScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleFrameRateScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleFrameRateScope.ToCurrentLine
                : SubtitleFrameRateScope.Selection;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ComboBox CreateFrameRateCombo(string value)
    {
        var combo = new ComboBox
        {
            MaxLength = 10,
            Sorted = true,
            Text = value
        };
        combo.Items.AddRange(CommonFrameRates);
        return combo;
    }

    private void AcceptOptions()
    {
        if (!TryParseFrameRate(_currentFrameRate.Text, out var currentFrameRate) || currentFrameRate <= 0d)
        {
            ShowValidation(_currentFrameRate, "Wpisz prawidłowy aktualny współczynnik FPS.");
            return;
        }

        if (!TryParseFrameRate(_targetFrameRate.Text, out var targetFrameRate) || targetFrameRate <= 0d)
        {
            ShowValidation(_targetFrameRate, "Wpisz prawidłowy docelowy współczynnik FPS.");
            return;
        }

        if (currentFrameRate == targetFrameRate)
        {
            ShowValidation(_currentFrameRate, "Aktualny i docelowy współczynnik FPS muszą być różne.");
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private void ShowValidation(Control control, string message)
    {
        MessageBox.Show(this, message, "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
        if (control is ComboBox combo)
        {
            combo.SelectAll();
        }
    }

    private static bool TryParseFrameRate(string text, out double value) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
        double.IsFinite(value);

    private static double ParseFrameRate(string text) =>
        TryParseFrameRate(text, out var value) ? value : 0d;

    private static string FormatFrameRate(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
