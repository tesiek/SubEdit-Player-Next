using System.Globalization;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class FindShortSubtitleDialog : StandardDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly CheckBox _adjustForLineLengthCheckBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public FindShortSubtitleDialog(ShortSubtitleSearchSettings settings, double framesPerSecond) : base(330)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond); Text = "Znajdź krótko wyświetlany napis";
        var group = Page.AddSection("Nie dłużej niż");
        _secondsTextBox = Input("0", 10); _framesTextBox = Input("0", 13);
        group.AddField("&Sekund:", _secondsTextBox); group.AddField("&Klatek:", _framesTextBox);
        _adjustForLineLengthCheckBox = group.AddCheckBox("&Zależnie od długości linii", settings.AdjustForLineLength);
        ConfigureActions(AcceptValue);
        _toolTip.SetToolTip(_secondsTextBox, "Min. wielkość przerwy między napisami");
        _toolTip.SetToolTip(_framesTextBox, "Min. wielkość przerwy między napisami");
        _toolTip.SetToolTip(
            _adjustForLineLengthCheckBox,
            "Czas szukanego napisu uzależniony od długości napisu (wpisany czas rozdziela się na 30 znaków)");

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        UpdateFields(Math.Max(0.0000001d, settings.MaximumDurationSeconds));
        BindOptionToolTips(_toolTip);
        Shown += (_, _) =>
        {
            _secondsTextBox.Focus();
            _secondsTextBox.SelectAll();
        };
    }

    public double MaximumDurationSeconds { get; private set; }

    public bool AdjustForLineLength => _adjustForLineLengthCheckBox.Checked;

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
        if (_updatingFields || !SubtitleTimingInput.TryParseNonNegativeNumber(_framesTextBox.Text, out var frames))
        {
            return;
        }

        UpdateFields(frames / _framesPerSecond, updateFrames: false);
    }

    private void UpdateFields(double seconds, bool updateSeconds = true, bool updateFrames = true)
    {
        if (!double.IsFinite(seconds) || seconds < 0d)
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
                var frames = Math.Floor(seconds * _framesPerSecond + 0.5d);
                _framesTextBox.Text = frames.ToString("0", CultureInfo.InvariantCulture);
            }
        }
        finally
        {
            _updatingFields = false;
        }
    }

    private void AcceptValue()
    {
        if (!SubtitleTimingInput.TryParseNonNegativeNumber(_secondsTextBox.Text, out var seconds) || seconds <= 0d)
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

        MaximumDurationSeconds = seconds;
        DialogResult = DialogResult.OK;
    }

}
