using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class GoToSubtitleDialog : StandardDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly TextBox _hoursTextBox;
    private readonly TextBox _minutesTextBox;
    private readonly TextBox _timeSecondsTextBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;
    private bool _clockInputPending;
    private const string ClockOutsideRangeHint = "Czas poza zakresem zegara 00:00:00–99:59:59";
    private const string ClockHint = "Czas, według którego zostanie wyszukany napis.";

    public GoToSubtitleDialog(double framesPerSecond) : base(360)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Przejdź do napisu";
        var group = Page.AddSection("Pozycja");
        _secondsTextBox = Input("0.0"); _framesTextBox = Input("0", 13);
        _hoursTextBox = Input("00", 2); _minutesTextBox = Input("00", 2); _timeSecondsTextBox = Input("00", 2);
        group.AddField("&Sekundy:", _secondsTextBox); group.AddField("&Klatki:", _framesTextBox);
        var clock = new SubEdit.Player.UI.DesignSystem.AdaptiveSettingsRow { KeepTogether = true };
        clock.AddControl(new SubEdit.Player.UI.DesignSystem.FieldValueLayout(_hoursTextBox, "g", keepTogether: true, widthSample: "99"));
        clock.AddControl(new SubEdit.Player.UI.DesignSystem.FieldValueLayout(_minutesTextBox, "m", keepTogether: true, widthSample: "59"));
        clock.AddControl(new SubEdit.Player.UI.DesignSystem.FieldValueLayout(_timeSecondsTextBox, "s", keepTogether: true, widthSample: "59"));
        group.AddField("&Czas:", clock);
        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        _hoursTextBox.TextChanged += (_, _) => UpdateFromClock();
        _minutesTextBox.TextChanged += (_, _) => UpdateFromClock();
        _timeSecondsTextBox.TextChanged += (_, _) => UpdateFromClock();
        ConfigureActions(AcceptValue);
        _toolTip.SetToolTip(_secondsTextBox, "Sekunda, według której zostanie wyszukany napis.");
        _toolTip.SetToolTip(_framesTextBox, "Klatka, według której zostanie wyszukany napis.");
        _toolTip.SetToolTip(_hoursTextBox, ClockHint);
        _toolTip.SetToolTip(_minutesTextBox, ClockHint);
        _toolTip.SetToolTip(_timeSecondsTextBox, ClockHint);

        BindOptionToolTips(_toolTip);
        Shown += (_, _) =>
        {
            _secondsTextBox.Focus();
            _secondsTextBox.SelectAll();
        };
    }

    public double TargetSeconds { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateFromSeconds()
    {
        if (_updatingFields || !SubtitleTimingInput.TryParseNonNegativeNumber(_secondsTextBox.Text, out var seconds))
        {
            return;
        }

        UpdateFields(seconds, updateSeconds: false);
    }

    private void UpdateFromFrames()
    {
        if (_updatingFields ||
            !long.TryParse(_framesTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) ||
            frames < 0)
        {
            return;
        }

        UpdateFields(frames / _framesPerSecond, updateFrames: false);
    }

    private void UpdateFromClock()
    {
        if (_updatingFields) return;
        _clockInputPending = true;
        if (TryGetClockSeconds(out var seconds))
        {
            UpdateFields(seconds, updateClock: false);
            SetClockHint(ClockHint);
        }
    }

    private void UpdateFields(
        double seconds,
        bool updateSeconds = true,
        bool updateFrames = true,
        bool updateClock = true)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
        {
            return;
        }

        _updatingFields = true;
        try
        {
            if (updateSeconds)
            {
                _secondsTextBox.Text = seconds.ToString("0.##########", CultureInfo.CurrentCulture);
            }

            if (updateFrames)
            {
                var frames = Math.Min(
                    long.MaxValue,
                    Math.Floor(seconds * _framesPerSecond + 0.5d));
                _framesTextBox.Text = ((long)frames).ToString(CultureInfo.InvariantCulture);
            }

            if (updateClock)
            {
                _clockInputPending = false;
                var fitsClock = seconds < 100 * 3600d;
                var wholeSeconds = fitsClock ? (long)Math.Floor(seconds) : 0;
                _hoursTextBox.Text = fitsClock ? (wholeSeconds / 3600).ToString("00", CultureInfo.InvariantCulture) : "";
                _minutesTextBox.Text = fitsClock ? (wholeSeconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture) : "";
                _timeSecondsTextBox.Text = fitsClock ? (wholeSeconds % 60).ToString("00", CultureInfo.InvariantCulture) : "";
                SetClockHint(fitsClock ? ClockHint : ClockOutsideRangeHint);
            }
        }
        finally
        {
            _updatingFields = false;
        }
    }

    private void AcceptValue()
    {
        if (_clockInputPending && !TryGetClockSeconds(out _))
        {
            MessageBox.Show(this, "Podaj prawidłowy czas: godziny 00–99, minuty i sekundy 00–59.",
                "Nieprawidłowa wartość", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _hoursTextBox.Focus();
            _hoursTextBox.SelectAll();
            return;
        }

        if (!SubtitleTimingInput.TryParseNonNegativeNumber(_secondsTextBox.Text, out var seconds) ||
            !long.TryParse(_framesTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var frames) ||
            frames <= 0 ||
            Math.Floor(seconds * _framesPerSecond + 0.5d) <= 0)
        {
            MessageBox.Show(
                this,
                "Podaj czas większy od zera.",
                "Nieprawidłowa wartość",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _secondsTextBox.Focus();
            _secondsTextBox.SelectAll();
            return;
        }

        TargetSeconds = seconds;
        DialogResult = DialogResult.OK;
    }

    private void SetClockHint(string hint)
    {
        foreach (var field in new[] { _hoursTextBox, _minutesTextBox, _timeSecondsTextBox })
            _toolTip.SetToolTip(field, hint);
    }

    private bool TryGetClockSeconds(out double seconds)
    {
        seconds = 0;
        if (!TryParseClockPart(_hoursTextBox.Text, 99, out var hours) ||
            !TryParseClockPart(_minutesTextBox.Text, 59, out var minutes) ||
            !TryParseClockPart(_timeSecondsTextBox.Text, 59, out var clockSeconds)) return false;
        seconds = hours * 3600d + minutes * 60d + clockSeconds;
        return true;
    }

    private static bool TryParseClockPart(string text, long maximum, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= maximum;

}
