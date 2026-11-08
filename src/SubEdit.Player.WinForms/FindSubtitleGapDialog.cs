using System.Globalization;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class FindSubtitleGapDialog : StandardDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public FindSubtitleGapDialog(SubtitleGapSearchSettings settings, double framesPerSecond) : base(330)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond); Text = "Znajdź przerwę w wyświetlaniu napisów";
        var group = Page.AddSection("Co najmniej");
        _secondsTextBox = Input("0", 10); _framesTextBox = Input("0", 13);
        group.AddField("&Sekund:", _secondsTextBox); group.AddField("&Klatek:", _framesTextBox);
        ConfigureActions(AcceptValue);
        const string fieldToolTip = "Min. wielkość przerwy między napisami";
        _toolTip.SetToolTip(_secondsTextBox, fieldToolTip);
        _toolTip.SetToolTip(_framesTextBox, fieldToolTip);

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        UpdateFields(Math.Max(0.0000001d, settings.MinimumGapSeconds));
        BindOptionToolTips(_toolTip);
        Shown += (_, _) =>
        {
            _secondsTextBox.Focus();
            _secondsTextBox.SelectAll();
        };
    }

    public double MinimumGapSeconds { get; private set; }

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

        MinimumGapSeconds = seconds;
        DialogResult = DialogResult.OK;
    }

}
