using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class GoToSubtitleDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly TextBox _hoursTextBox;
    private readonly TextBox _minutesTextBox;
    private readonly TextBox _timeSecondsTextBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public GoToSubtitleDialog(double framesPerSecond)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Przejdź do napisu";
        ClientSize = new Size(340, 202);

        var panel = new GroupBox
        {
            Text = string.Empty,
            Location = new Point(15, 10),
            Size = new Size(310, 136)
        };

        panel.Controls.Add(CreateLabel("&Sekundy:", 26, 0));
        panel.Controls.Add(CreateLabel("&Klatki:", 56, 2));
        panel.Controls.Add(CreateLabel("&Czasu:", 86, 4));

        _secondsTextBox = CreateTextBox(new Point(115, 22), new Size(175, 23), "0.0", 10, 1);
        _framesTextBox = CreateTextBox(new Point(115, 52), new Size(175, 23), "0", 13, 3);
        _hoursTextBox = CreateTextBox(new Point(115, 82), new Size(32, 23), "00", 2, 5);
        _minutesTextBox = CreateTextBox(new Point(175, 82), new Size(32, 23), "00", 2, 6);
        _timeSecondsTextBox = CreateTextBox(new Point(235, 82), new Size(32, 23), "00", 2, 7);

        panel.Controls.AddRange([
            _secondsTextBox,
            _framesTextBox,
            _hoursTextBox,
            _minutesTextBox,
            _timeSecondsTextBox,
            CreateUnitLabel("g", 150),
            CreateUnitLabel("m", 210),
            CreateUnitLabel("s", 270)
        ]);

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        _hoursTextBox.TextChanged += (_, _) => UpdateFromClock();
        _minutesTextBox.TextChanged += (_, _) => UpdateFromClock();
        _timeSecondsTextBox.TextChanged += (_, _) => UpdateFromClock();

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(92, 163),
            Size = new Size(75, 25),
            TabIndex = 1
        };
        ok.Click += (_, _) => AcceptValue();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(173, 163),
            Size = new Size(75, 25),
            TabIndex = 2
        };

        Controls.AddRange([panel, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;

        _toolTip.SetToolTip(_secondsTextBox, "Sekunda, według której zostanie wyszukany napis.");
        _toolTip.SetToolTip(_framesTextBox, "Klatka, według której zostanie wyszukany napis.");
        const string clockHint = "Czas, według którego zostanie wyszukany napis.";
        _toolTip.SetToolTip(_hoursTextBox, clockHint);
        _toolTip.SetToolTip(_minutesTextBox, clockHint);
        _toolTip.SetToolTip(_timeSecondsTextBox, clockHint);

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

    private static Label CreateLabel(string text, int top, int tabIndex) => new()
    {
        Text = text,
        Location = new Point(25, top),
        Size = new Size(78, 20),
        TextAlign = ContentAlignment.MiddleRight,
        TabIndex = tabIndex
    };

    private static Label CreateUnitLabel(string text, int left) => new()
    {
        Text = text,
        Location = new Point(left, 85),
        AutoSize = true
    };

    private static TextBox CreateTextBox(Point location, Size size, string text, int maxLength, int tabIndex) => new()
    {
        Location = location,
        Size = size,
        Text = text,
        MaxLength = maxLength,
        TabIndex = tabIndex
    };

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
        if (_updatingFields ||
            !TryParseClockPart(_hoursTextBox.Text, out var hours) ||
            !TryParseClockPart(_minutesTextBox.Text, out var minutes) ||
            !TryParseClockPart(_timeSecondsTextBox.Text, out var seconds))
        {
            return;
        }

        UpdateFields(hours * 3600d + minutes * 60d + seconds, updateClock: false);
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
                var wholeSeconds = (long)Math.Min(long.MaxValue, Math.Floor(seconds));
                var hours = wholeSeconds / 3600;
                var minutes = wholeSeconds / 60 % 60;
                var clockSeconds = wholeSeconds % 60;
                _hoursTextBox.Text = hours.ToString("00", CultureInfo.InvariantCulture);
                _minutesTextBox.Text = minutes.ToString("00", CultureInfo.InvariantCulture);
                _timeSecondsTextBox.Text = clockSeconds.ToString("00", CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _updatingFields = false;
        }
    }

    private void AcceptValue()
    {
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

    private static bool TryParseClockPart(string text, out long value) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

}
