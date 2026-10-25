using System.Globalization;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class FindSubtitleGapDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public FindSubtitleGapDialog(SubtitleGapSearchSettings settings, double framesPerSecond)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Znajdź przerwę w wyświetlaniu napisów";
        ClientSize = new Size(330, 177);

        var groupBox = new GroupBox
        {
            Text = "Co najmniej",
            Location = new Point(12, 10),
            Size = new Size(306, 108)
        };
        groupBox.Controls.Add(CreateLabel("&Sekund:", 31, 0));
        groupBox.Controls.Add(CreateLabel("&Klatek:", 62, 2));

        _secondsTextBox = CreateTextBox(new Point(99, 29), new Size(175, 23), 10, 1);
        _framesTextBox = CreateTextBox(new Point(99, 60), new Size(175, 23), 13, 3);
        groupBox.Controls.AddRange([_secondsTextBox, _framesTextBox]);

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(87, 137),
            Size = new Size(75, 25),
            TabIndex = 4
        };
        okButton.Click += (_, _) => AcceptValue();
        var cancelButton = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(168, 137),
            Size = new Size(75, 25),
            TabIndex = 5
        };

        Controls.AddRange([groupBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        const string fieldToolTip = "Min. wielkość przerwy między napisami";
        _toolTip.SetToolTip(_secondsTextBox, fieldToolTip);
        _toolTip.SetToolTip(_framesTextBox, fieldToolTip);

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        UpdateFields(Math.Max(0.0000001d, settings.MinimumGapSeconds));
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

    private static Label CreateLabel(string text, int top, int tabIndex) => new()
    {
        Text = text,
        Location = new Point(18, top),
        Size = new Size(75, 23),
        TextAlign = ContentAlignment.MiddleRight,
        TabIndex = tabIndex
    };

    private static TextBox CreateTextBox(Point location, Size size, int maxLength, int tabIndex) => new()
    {
        Location = location,
        Size = size,
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
