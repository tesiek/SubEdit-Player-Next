using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class GeometricSubtitleShiftDialog : StandardDialog
{
    private readonly TextBox _initialDelay;
    private readonly TextBox _finalDelay;
    private readonly CheckBox _fromInitialLine;
    private readonly CheckBox _toFinalLine;

    public GeometricSubtitleShiftDialog(
        SubtitleGeometryMeasurement initial,
        SubtitleGeometryMeasurement final,
        double framesPerSecond,
        bool changeFromInitialLine,
        bool changeToFinalLine) : base(560)
    {
        Text = "Geometryczne przesunięcie napisów...";
        var fps = Page.AddSection("FPS");
        var measuredSubtitleFps = (final.SubtitleTimeMilliseconds - initial.SubtitleTimeMilliseconds) * Math.Max(1d, framesPerSecond) / (final.VideoTimeMilliseconds - initial.VideoTimeMilliseconds);
        fps.AddText("FPS aktualnych napisów: " + measuredSubtitleFps.ToString("0.###", CultureInfo.InvariantCulture));
        fps.AddText("FPS napisów po zmianie: " + framesPerSecond.ToString("0.###", CultureInfo.InvariantCulture));
        (_initialDelay, _fromInitialLine) = Measurement("Pomiar początkowy", initial, "Zmieniaj &od tej linii", changeFromInitialLine);
        (_finalDelay, _toFinalLine) = Measurement("Pomiar końcowy", final, "Zmieniaj &do tej linii", changeToFinalLine);
        ConfigureActions(AcceptOptions);
        Shown += (_, _) => { _initialDelay.Focus(); _initialDelay.SelectAll(); };
    }

    public double InitialDelaySeconds { get; private set; }

    public double FinalDelaySeconds { get; private set; }

    public bool ChangeFromInitialLine => _fromInitialLine.Checked;

    public bool ChangeToFinalLine => _toFinalLine.Checked;

    private (TextBox Delay, CheckBox Range) Measurement(string title, SubtitleGeometryMeasurement measurement, string rangeCaption, bool rangeChecked)
    {
        var section = Page.AddSection(title);
        section.AddText("Nr linii: " + (measurement.LineIndex + 1).ToString(CultureInfo.CurrentCulture));
        section.AddText("Linia: " + measurement.LineText);
        var delay = Input(((measurement.VideoTimeMilliseconds - measurement.SubtitleTimeMilliseconds) / 1000d).ToString("0.########", CultureInfo.CurrentCulture));
        section.AddFieldWithUnit("Opóźnienie:", delay, "sekund");
        return (delay, section.AddCheckBox(rangeCaption, rangeChecked));
    }

    private void AcceptOptions()
    {
        if (!TryParseNumber(_initialDelay.Text, out var initialDelay))
        {
            ShowInvalidValue(_initialDelay);
            return;
        }

        if (!TryParseNumber(_finalDelay.Text, out var finalDelay))
        {
            ShowInvalidValue(_finalDelay);
            return;
        }

        InitialDelaySeconds = initialDelay;
        FinalDelaySeconds = finalDelay;
        DialogResult = DialogResult.OK;
    }

    private void ShowInvalidValue(TextBox textBox)
    {
        MessageBox.Show(
            this,
            "Wpisz poprawną wartość opóźnienia.",
            "Ostrzeżenie",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        textBox.Focus();
        textBox.SelectAll();
    }

    private static bool TryParseNumber(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        return double.TryParse(
            text.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }
}
