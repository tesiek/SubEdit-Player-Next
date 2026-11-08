using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleTimeShiftScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleTimeShiftDialog : StandardDialog
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

    public SubtitleTimeShiftDialog(double framesPerSecond, double? measuredDelaySeconds = null) : base(370)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond); Text = "Przesuń czas napisów";
        _seconds = Input("0.0"); _frames = Input("0", 13);
        if (measuredDelaySeconds.HasValue && measuredDelaySeconds.Value != 0d)
        {
            var measured = new LinkLabel { Text = measuredDelaySeconds.Value.ToString("0.########", CultureInfo.CurrentCulture), AutoSize = true };
            Page.AddSection("Opóźnienie napisu względem dźwięku wynosi").AddAdaptiveRow().AddControl(measured);
            measured.LinkClicked += (_, _) => _seconds.Text = measured.Text;
        }
        var range = Page.AddSection("Zakres");
        _shiftStart = range.AddCheckBox("Zmieniaj czas/klatki st&artowe", true);
        _shiftEnd = range.AddCheckBox("Zmieniaj czas/klatki k&ońcowe", true);
        var shift = Page.AddSection("Przesunięcie");
        shift.AddField("&Sekund:", _seconds); shift.AddField("&Klatek:", _frames);
        _backwards = shift.AddCheckBox("&Wstecz");
        _seconds.TextChanged += (_, _) => UpdateFramesFromSeconds();
        _frames.TextChanged += (_, _) => UpdateSecondsFromFrames();
        _backwards.CheckedChanged += (_, _) => ApplyDirection();
        (_selectionScope, _fromCurrentLineScope, _toCurrentLineScope) = AddScope();
        ConfigureActions(AcceptOptions);
        Shown += (_, _) => { _seconds.Focus(); _seconds.SelectAll(); };
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
