using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleDisplayTimeScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleDisplayTimeDialog : StandardDialog
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

    public SubtitleDisplayTimeDialog(double framesPerSecond, SubtitleDisplayTimeSettings defaults) : base(440)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond); Text = "Zmień czas wyświetlania napisów";
        _clearEndTimes = Page.AddSection("Sposób zmiany").AddCheckBox("Wpisz &puste pozycje");
        var options = Page.AddSection("Opcje");
        _onlyUndefined = options.AddCheckBox("&Zmieniaj tylko nieokreślone czasy", defaults.ChangeOnlyUndefinedEndTimes);
        _dependsOnTextLength = options.AddCheckBox("&Czas zależny od długości napisu", defaults.DurationDependsOnTextLength);
        _blockEndTime = options.AddCheckBox("&Blokuj czas końcowy", defaults.BlockEndTime);
        var duration = Page.AddSection("Czas wyświetlania");
        _baseSeconds = Input(FormatNumber(defaults.BaseDurationSeconds));
        _baseFrames = Input(RoundLikeOriginal(defaults.BaseDurationSeconds * _framesPerSecond).ToString(CultureInfo.InvariantCulture));
        _additionalSeconds = Input(FormatNumber(defaults.AdditionalDurationSeconds));
        _additionalFrames = Input(RoundLikeOriginal(defaults.AdditionalDurationSeconds * _framesPerSecond).ToString(CultureInfo.InvariantCulture));
        var seconds = duration.AddAdaptiveRow();
        var baseSecondsField = seconds.AddField("&Sekund:", _baseSeconds);
        _baseSecondsLabel = baseSecondsField.Description; _baseSecondsLabel.UseMnemonic = true;
        seconds.AddField("+", _additionalSeconds);
        var frames = duration.AddAdaptiveRow();
        var baseFramesField = frames.AddField("&Klatek:", _baseFrames);
        _baseFramesLabel = baseFramesField.Description; _baseFramesLabel.UseMnemonic = true;
        frames.AddField("+", _additionalFrames);
        _baseSeconds.TextChanged += (_, _) => UpdateFramesFromSeconds(_baseSeconds, _baseFrames);
        _baseFrames.TextChanged += (_, _) => UpdateSecondsFromFrames(_baseFrames, _baseSeconds);
        _additionalSeconds.TextChanged += (_, _) => UpdateFramesFromSeconds(_additionalSeconds, _additionalFrames);
        _additionalFrames.TextChanged += (_, _) => UpdateSecondsFromFrames(_additionalFrames, _additionalSeconds);
        _clearEndTimes.CheckedChanged += (_, _) => UpdateOptionsAvailability();
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        _saveAsDefault = AddSaveDefault(); ConfigureActions(AcceptOptions); AddToolTips(); UpdateOptionsAvailability();
        BindOptionToolTips(_toolTip);
        Shown += (_, _) => { _baseSeconds.Focus(); _baseSeconds.SelectAll(); };
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
