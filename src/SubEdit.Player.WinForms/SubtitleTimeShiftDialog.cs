using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleTimeShiftScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleTimeShiftDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly CheckBox _shiftStart;
    private readonly CheckBox _shiftEnd;
    private readonly TextBox _seconds;
    private readonly TextBox _frames;
    private readonly CheckBox _backwards;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private bool _updatingValues;

    public SubtitleTimeShiftDialog(double framesPerSecond, double? measuredDelaySeconds = null)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Przesuń czas napisów";
        var measuredDelay = measuredDelaySeconds.GetValueOrDefault();
        var showMeasuredDelay = measuredDelaySeconds.HasValue && measuredDelay != 0d;
        var contentOffset = showMeasuredDelay ? 56 : 0;
        Label? measuredDelayLabel = null;
        ClientSize = new Size(340, 330 + contentOffset);

        if (showMeasuredDelay)
        {
            var measuredDelayGroup = new GroupBox
            {
                Text = "Opóźnienie napisu względem dźwięku wynosi",
                Location = new Point(15, 10),
                Size = new Size(310, 46)
            };
            measuredDelayLabel = new Label
            {
                Text = measuredDelay.ToString("0.########", CultureInfo.CurrentCulture),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(15, 17),
                Size = new Size(280, 24)
            };
            measuredDelayGroup.Controls.Add(measuredDelayLabel);
            Controls.Add(measuredDelayGroup);
        }

        var rangeGroup = new GroupBox
        {
            Text = "Zakres",
            Location = new Point(15, 10 + contentOffset),
            Size = new Size(310, 81)
        };
        _shiftStart = AddCheckBox(rangeGroup, "Zmieniaj czas/klatki st&artowe", 15, 23, isChecked: true);
        _shiftEnd = AddCheckBox(rangeGroup, "Zmieniaj czas/klatki k&ońcowe", 15, 47, isChecked: true);

        var shiftGroup = new GroupBox
        {
            Text = "Przesunięcie",
            Location = new Point(15, 100 + contentOffset),
            Size = new Size(310, 107)
        };
        shiftGroup.Controls.Add(new Label
        {
            Text = "&Sekund:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(8, 25),
            Size = new Size(58, 23)
        });
        shiftGroup.Controls.Add(new Label
        {
            Text = "&Klatek:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(8, 55),
            Size = new Size(58, 23)
        });
        _seconds = new TextBox
        {
            Text = "0.0",
            Location = new Point(70, 25),
            Size = new Size(125, 23),
            MaxLength = 10
        };
        if (measuredDelayLabel is not null)
        {
            measuredDelayLabel.Click += (_, _) => _seconds.Text = measuredDelayLabel.Text;
        }
        _frames = new TextBox
        {
            Text = "0",
            Location = new Point(70, 55),
            Size = new Size(125, 23),
            MaxLength = 13
        };
        _backwards = new CheckBox
        {
            Text = "&Wstecz",
            Location = new Point(205, 81),
            AutoSize = true
        };
        _seconds.TextChanged += (_, _) => UpdateFramesFromSeconds();
        _frames.TextChanged += (_, _) => UpdateSecondsFromFrames();
        _backwards.CheckedChanged += (_, _) => ApplyDirection();
        shiftGroup.Controls.AddRange([_seconds, _frames, _backwards]);

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(15, 215 + contentOffset),
            Size = new Size(310, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(92, 302 + contentOffset),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(173, 302 + contentOffset),
            Size = new Size(75, 25)
        };

        Controls.AddRange([rangeGroup, shiftGroup, scopeGroup, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _seconds.Focus();
            _seconds.SelectAll();
        };
    }

    public double ShiftSeconds => TryParseNumber(_seconds.Text, out var seconds) ? seconds : 0d;

    public bool ShiftStart => _shiftStart.Checked;

    public bool ShiftEnd => _shiftEnd.Checked;

    public SubtitleTimeShiftScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleTimeShiftScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleTimeShiftScope.ToCurrentLine
                : SubtitleTimeShiftScope.Selection;

    private static CheckBox AddCheckBox(Control parent, string text, int left, int top, bool isChecked)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Location = new Point(left, top),
            AutoSize = true,
            Checked = isChecked
        };
        parent.Controls.Add(checkBox);
        return checkBox;
    }

    private static RadioButton AddScopeOption(Control parent, string text, int top)
    {
        var radioButton = new RadioButton
        {
            Text = text,
            ForeColor = SystemColors.ControlText,
            Location = new Point(8, top),
            AutoSize = true
        };
        parent.Controls.Add(radioButton);
        return radioButton;
    }

    private void UpdateFramesFromSeconds()
    {
        if (_updatingValues || !TryParseNumber(_seconds.Text, out var seconds))
        {
            return;
        }

        _updatingValues = true;
        try
        {
            _frames.Text = RoundLikeOriginal(seconds * _framesPerSecond)
                .ToString(CultureInfo.InvariantCulture);
            _backwards.Checked = seconds < 0;
        }
        finally
        {
            _updatingValues = false;
        }
    }

    private void UpdateSecondsFromFrames()
    {
        if (_updatingValues ||
            !long.TryParse(_frames.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames))
        {
            return;
        }

        _updatingValues = true;
        try
        {
            _seconds.Text = (frames / _framesPerSecond).ToString("0.########", CultureInfo.CurrentCulture);
            _backwards.Checked = frames < 0;
        }
        finally
        {
            _updatingValues = false;
        }
    }

    private void ApplyDirection()
    {
        if (_updatingValues ||
            !TryParseNumber(_seconds.Text, out var seconds) ||
            !long.TryParse(_frames.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames))
        {
            return;
        }

        var shouldBeNegative = _backwards.Checked;
        _updatingValues = true;
        try
        {
            _seconds.Text = (shouldBeNegative ? -Math.Abs(seconds) : Math.Abs(seconds))
                .ToString("0.########", CultureInfo.CurrentCulture);
            _frames.Text = (shouldBeNegative ? -Math.Abs(frames) : Math.Abs(frames))
                .ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingValues = false;
        }
    }

    private void AcceptOptions()
    {
        if (!TryParseNumber(_seconds.Text, out _) ||
            !long.TryParse(_frames.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames))
        {
            MessageBox.Show(this, "Wpisz poprawną wartość przesunięcia.", "Ostrzeżenie",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _seconds.Focus();
            return;
        }

        if (frames == 0)
        {
            MessageBox.Show(this, "Przesunięcie nie może wynosić zero.", "Ostrzeżenie",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _frames.Focus();
            _frames.SelectAll();
            return;
        }

        if (!_shiftStart.Checked && !_shiftEnd.Checked)
        {
            MessageBox.Show(this, "Nie ma czego zmieniać.", "Ostrzeżenie",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _shiftStart.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private static bool TryParseNumber(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        var normalized = text.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static long RoundLikeOriginal(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);
}
