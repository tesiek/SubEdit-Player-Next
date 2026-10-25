using System.Globalization;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class FindShortSubtitleDialog : SubEditDialog
{
    private readonly double _framesPerSecond;
    private readonly TextBox _secondsTextBox;
    private readonly TextBox _framesTextBox;
    private readonly CheckBox _adjustForLineLengthCheckBox;
    private readonly ToolTip _toolTip = new();
    private bool _updatingFields;

    public FindShortSubtitleDialog(ShortSubtitleSearchSettings settings, double framesPerSecond)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        Text = "Znajdź krótko wyświetlany napis";
        ClientSize = new Size(330, 214);

        var groupBox = new GroupBox
        {
            Text = "Nie dłużej niż",
            Location = new Point(12, 10),
            Size = new Size(306, 145)
        };
        groupBox.Controls.Add(CreateLabel("&Sekund:", 31, 0));
        groupBox.Controls.Add(CreateLabel("&Klatek:", 62, 2));

        _secondsTextBox = CreateTextBox(new Point(99, 29), new Size(175, 23), 10, 1);
        _framesTextBox = CreateTextBox(new Point(99, 60), new Size(175, 23), 13, 3);
        _adjustForLineLengthCheckBox = new CheckBox
        {
            Text = "&Zależnie od długości linii",
            AutoSize = true,
            Location = new Point(18, 104),
            Checked = settings.AdjustForLineLength,
            TabIndex = 4
        };
        groupBox.Controls.AddRange([_secondsTextBox, _framesTextBox, _adjustForLineLengthCheckBox]);

        var okButton = new Button
        {
            Text = "OK",
            Location = new Point(87, 174),
            Size = new Size(75, 25),
            TabIndex = 5
        };
        okButton.Click += (_, _) => AcceptValue();
        var cancelButton = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(168, 174),
            Size = new Size(75, 25),
            TabIndex = 6
        };

        Controls.AddRange([groupBox, okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        _toolTip.SetToolTip(_secondsTextBox, "Min. wielkość przerwy między napisami");
        _toolTip.SetToolTip(_framesTextBox, "Min. wielkość przerwy między napisami");
        _toolTip.SetToolTip(
            _adjustForLineLengthCheckBox,
            "Czas szukanego napisu uzależniony od długości napisu (wpisany czas rozdziela się na 30 znaków)");

        _secondsTextBox.TextChanged += (_, _) => UpdateFromSeconds();
        _framesTextBox.TextChanged += (_, _) => UpdateFromFrames();
        UpdateFields(Math.Max(0.0000001d, settings.MaximumDurationSeconds));
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

        MaximumDurationSeconds = seconds;
        DialogResult = DialogResult.OK;
    }

}
