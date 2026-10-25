using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class GoToVideoPositionDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly long _currentPositionMilliseconds;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly TextBox _hoursTextBox;
    private readonly TextBox _minutesTextBox;
    private readonly TextBox _timeSecondsTextBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public GoToVideoPositionDialog(double framesPerSecond, long currentPositionMilliseconds)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        _currentPositionMilliseconds = Math.Max(0, currentPositionMilliseconds);
        Text = "Skocz do";
        ClientSize = new Size(248, 224);

        var panel = new GroupBox
        {
            Text = string.Empty,
            Location = new Point(10, 10),
            Size = new Size(230, 160)
        };

        panel.Controls.Add(CreateLabel("&Sekundy:", 20, 0));
        panel.Controls.Add(CreateLabel("&Klatki:", 52, 1));
        panel.Controls.Add(CreateLabel("&Czasu:", 84, 2));

        _secondsTextBox = CreateTextBox(new Point(77, 18), new Size(114, 23), "0", 10, 3);
        _framesTextBox = CreateTextBox(new Point(77, 50), new Size(114, 23), "0", 13, 4);
        _hoursTextBox = CreateTextBox(new Point(77, 82), new Size(24, 23), "00", 2, 5);
        _minutesTextBox = CreateTextBox(new Point(122, 82), new Size(24, 23), "00", 2, 6);
        _timeSecondsTextBox = CreateTextBox(new Point(167, 82), new Size(24, 23), "00", 2, 7);

        var currentButton = new Button
        {
            Text = "&Aktualny",
            FlatStyle = FlatStyle.Flat,
            Location = new Point(15, 123),
            Size = new Size(70, 25),
            TabIndex = 8
        };
        currentButton.Click += (_, _) => UpdateFields(_currentPositionMilliseconds / 1000d);

        panel.Controls.AddRange([
            _secondsTextBox,
            _framesTextBox,
            _hoursTextBox,
            _minutesTextBox,
            _timeSecondsTextBox,
            CreateUnitLabel("g", 103),
            CreateUnitLabel("m", 148),
            CreateUnitLabel("s", 193),
            currentButton
        ]);

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        _hoursTextBox.TextChanged += (_, _) => UpdateFromClock();
        _minutesTextBox.TextChanged += (_, _) => UpdateFromClock();
        _timeSecondsTextBox.TextChanged += (_, _) => UpdateFromClock();

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(47, 187),
            Size = new Size(75, 25),
            TabIndex = 9
        };
        ok.Click += (_, _) => AcceptValue();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(127, 187),
            Size = new Size(75, 25),
            TabIndex = 10
        };

        Controls.AddRange([panel, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;

        _toolTip.SetToolTip(currentButton, "Wpisuje aktualny czas filmu");
        _toolTip.SetToolTip(_secondsTextBox, "Sekunda do której będzie wykonany skok");
        _toolTip.SetToolTip(_framesTextBox, "Klatka do której będzie wykonany skok");
        const string clockHint = "Czas do którego będzie wykonany skok";
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
        Location = new Point(7, top),
        Size = new Size(68, 20),
        TextAlign = ContentAlignment.MiddleRight,
        TabIndex = tabIndex
    };

    private static Label CreateUnitLabel(string text, int left) => new()
    {
        Text = text,
        Location = new Point(left, 86),
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
                var frames = Math.Min(long.MaxValue, Math.Floor(seconds * _framesPerSecond + 0.5d));
                _framesTextBox.Text = ((long)frames).ToString(CultureInfo.InvariantCulture);
            }

            if (updateClock)
            {
                var wholeSeconds = (long)Math.Min(long.MaxValue, Math.Floor(seconds));
                _hoursTextBox.Text = (wholeSeconds / 3600).ToString("00", CultureInfo.InvariantCulture);
                _minutesTextBox.Text = (wholeSeconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture);
                _timeSecondsTextBox.Text = (wholeSeconds % 60).ToString("00", CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _updatingFields = false;
        }
    }

    private void AcceptValue()
    {
        if (!SubtitleTimingInput.TryParseNonNegativeNumber(_secondsTextBox.Text, out var seconds))
        {
            MessageBox.Show(this, "Podaj prawidłowy czas.", "Nieprawidłowa wartość", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
