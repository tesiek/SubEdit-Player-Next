using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleDisplayTimeScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleDisplayTimeDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly CheckBox _clearEndTimes;
    private readonly CheckBox _onlyUndefined;
    private readonly CheckBox _dependsOnTextLength;
    private readonly CheckBox _blockEndTime;
    private readonly TextBox _baseSeconds;
    private readonly TextBox _baseFrames;
    private readonly TextBox _additionalSeconds;
    private readonly TextBox _additionalFrames;
    private readonly Label _baseSecondsLabel;
    private readonly Label _baseFramesLabel;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;
    private readonly ToolTip _toolTip = new();
    private bool _updatingValues;

    public SubtitleDisplayTimeDialog(double framesPerSecond, SubtitleDisplayTimeSettings defaults)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Zmień czas wyświetlania napisów";
        ClientSize = new Size(340, 392);

        _clearEndTimes = AddCheckBox(this, "Wpisz &puste pozycje", 18, 12, false);
        _clearEndTimes.CheckedChanged += (_, _) => UpdateOptionsAvailability();

        var optionsGroup = new GroupBox
        {
            Text = "Opcje",
            Location = new Point(15, 39),
            Size = new Size(310, 88)
        };
        _onlyUndefined = AddCheckBox(
            optionsGroup,
            "&Zmieniaj tylko nieokreślone czasy",
            12,
            18,
            defaults.ChangeOnlyUndefinedEndTimes);
        _dependsOnTextLength = AddCheckBox(
            optionsGroup,
            "&Czas zależny od długości napisu",
            12,
            40,
            defaults.DurationDependsOnTextLength);
        _blockEndTime = AddCheckBox(
            optionsGroup,
            "&Blokuj czas końcowy",
            12,
            62,
            defaults.BlockEndTime);

        var durationGroup = new GroupBox
        {
            Text = "Czas wyświetlania",
            Location = new Point(15, 135),
            Size = new Size(310, 104)
        };
        _baseSecondsLabel = AddLabel(durationGroup, "&Sekund:", 8, 25);
        _baseFramesLabel = AddLabel(durationGroup, "&Klatek:", 8, 57);
        _baseSeconds = AddTextBox(
            durationGroup,
            FormatNumber(defaults.BaseDurationSeconds),
            70,
            25,
            112);
        _baseFrames = AddTextBox(
            durationGroup,
            RoundLikeOriginal(defaults.BaseDurationSeconds * _framesPerSecond).ToString(CultureInfo.InvariantCulture),
            70,
            57,
            112);
        durationGroup.Controls.Add(new Label
        {
            Text = "+",
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(186, 25),
            Size = new Size(18, 23)
        });
        durationGroup.Controls.Add(new Label
        {
            Text = "+",
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(186, 57),
            Size = new Size(18, 23)
        });
        _additionalSeconds = AddTextBox(
            durationGroup,
            FormatNumber(defaults.AdditionalDurationSeconds),
            206,
            25,
            50);
        _additionalFrames = AddTextBox(
            durationGroup,
            RoundLikeOriginal(defaults.AdditionalDurationSeconds * _framesPerSecond).ToString(CultureInfo.InvariantCulture),
            206,
            57,
            50);

        _baseSeconds.TextChanged += (_, _) => UpdateFramesFromSeconds(_baseSeconds, _baseFrames);
        _baseFrames.TextChanged += (_, _) => UpdateSecondsFromFrames(_baseFrames, _baseSeconds);
        _additionalSeconds.TextChanged += (_, _) => UpdateFramesFromSeconds(_additionalSeconds, _additionalFrames);
        _additionalFrames.TextChanged += (_, _) => UpdateSecondsFromFrames(_additionalFrames, _additionalSeconds);

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(15, 247),
            Size = new Size(310, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;

        _saveAsDefault = new CheckBox
        {
            Text = "Zachowaj ustawienia jako domyślne",
            Location = new Point(18, 334),
            AutoSize = true
        };
        var ok = new Button
        {
            Text = "OK",
            Location = new Point(92, 362),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(173, 362),
            Size = new Size(75, 25)
        };

        Controls.AddRange([optionsGroup, durationGroup, scopeGroup, _saveAsDefault, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        AddToolTips();
        UpdateOptionsAvailability();
        Shown += (_, _) =>
        {
            _baseSeconds.Focus();
            _baseSeconds.SelectAll();
        };
    }

    public bool ClearEndTimes => _clearEndTimes.Checked;

    public bool SaveAsDefault => _saveAsDefault.Checked;

    public SubtitleDisplayTimeScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleDisplayTimeScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleDisplayTimeScope.ToCurrentLine
                : SubtitleDisplayTimeScope.Selection;

    public SubtitleDisplayTimeSettings CreateSettings() => new()
    {
        ChangeOnlyUndefinedEndTimes = _onlyUndefined.Checked,
        DurationDependsOnTextLength = _dependsOnTextLength.Checked,
        BlockEndTime = _blockEndTime.Checked,
        BaseDurationSeconds = TryParseNumber(_baseSeconds.Text, out var baseSeconds) ? baseSeconds : 0d,
        AdditionalDurationSeconds = TryParseNumber(_additionalSeconds.Text, out var additionalSeconds)
            ? additionalSeconds
            : 0d
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private static CheckBox AddCheckBox(
        Control parent,
        string text,
        int left,
        int top,
        bool isChecked)
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

    private static Label AddLabel(Control parent, string text, int left, int top)
    {
        var label = new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(left, top),
            Size = new Size(58, 23)
        };
        parent.Controls.Add(label);
        return label;
    }

    private static TextBox AddTextBox(Control parent, string text, int left, int top, int width)
    {
        var textBox = new TextBox
        {
            Text = text,
            Location = new Point(left, top),
            Size = new Size(width, 23),
            MaxLength = 10
        };
        parent.Controls.Add(textBox);
        return textBox;
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

    private void UpdateOptionsAvailability()
    {
        var enabled = !_clearEndTimes.Checked;
        _onlyUndefined.Enabled = enabled;
        _dependsOnTextLength.Enabled = enabled;
        _blockEndTime.Enabled = enabled;
        _baseSeconds.Enabled = enabled;
        _baseFrames.Enabled = enabled;
        _baseSecondsLabel.Enabled = enabled;
        _baseFramesLabel.Enabled = enabled;
    }

    private void AddToolTips()
    {
        _toolTip.SetToolTip(_onlyUndefined, "Zmienia czas końcowy tylko jeżeli jest zerowy");
        _toolTip.SetToolTip(
            _dependsOnTextLength,
            "Czas wyświetlania uzależniony od długości napisu (wpisany czas rozdziela się na 50 znaków)");
        _toolTip.SetToolTip(
            _blockEndTime,
            "Czas końcowy zawsze mniejszy od początkowego czasu następnego napisu");
        _toolTip.SetToolTip(_baseSeconds, "Ilość sekund wyświetlania napisu");
        _toolTip.SetToolTip(_baseFrames, "Ilość sekund wyświetlania napisu");
    }

    private void UpdateFramesFromSeconds(TextBox secondsBox, TextBox framesBox)
    {
        if (_updatingValues || !TryParseNumber(secondsBox.Text, out var seconds))
        {
            return;
        }

        _updatingValues = true;
        try
        {
            framesBox.Text = RoundLikeOriginal(seconds * _framesPerSecond)
                .ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingValues = false;
        }
    }

    private void UpdateSecondsFromFrames(TextBox framesBox, TextBox secondsBox)
    {
        if (_updatingValues ||
            !long.TryParse(framesBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames))
        {
            return;
        }

        _updatingValues = true;
        try
        {
            secondsBox.Text = FormatNumber(frames / _framesPerSecond);
        }
        finally
        {
            _updatingValues = false;
        }
    }

    private void AcceptOptions()
    {
        if (!_clearEndTimes.Checked)
        {
            if (!TryParseNumber(_baseSeconds.Text, out _) ||
                !long.TryParse(_baseFrames.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var frames) ||
                frames <= 0)
            {
                MessageBox.Show(
                    this,
                    "Czas wyświetlania musi być większy od zera.",
                    "Ostrzeżenie",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _baseFrames.Focus();
                _baseFrames.SelectAll();
                return;
            }

            if (!TryParseNumber(_additionalSeconds.Text, out _) ||
                !long.TryParse(_additionalFrames.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show(
                    this,
                    "Wpisz poprawny dodatkowy czas wyświetlania.",
                    "Ostrzeżenie",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _additionalSeconds.Focus();
                _additionalSeconds.SelectAll();
                return;
            }
        }

        DialogResult = DialogResult.OK;
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

    private static string FormatNumber(double value) =>
        value.ToString("0.########", CultureInfo.CurrentCulture);

    private static long RoundLikeOriginal(double value) =>
        SubtitleTimingFormatter.RoundAwayFromZero(value);
}
