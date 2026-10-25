using System.Globalization;

namespace SubEdit.Player.WinForms;

internal sealed class GeometricSubtitleShiftDialog : SubEditDialog
{
    private readonly TextBox _initialDelay;
    private readonly TextBox _finalDelay;
    private readonly CheckBox _fromInitialLine;
    private readonly CheckBox _toFinalLine;

    public GeometricSubtitleShiftDialog(
        SubtitleGeometryMeasurement initial,
        SubtitleGeometryMeasurement final,
        double framesPerSecond,
        bool changeFromInitialLine,
        bool changeToFinalLine)
    {
        Text = "Geometryczne przesunięcie napisów...";
        ClientSize = new Size(560, 382);

        var fpsPanel = new GroupBox
        {
            Text = string.Empty,
            Location = new Point(300, 12),
            Size = new Size(245, 52)
        };
        var measuredSubtitleFps = (final.SubtitleTimeMilliseconds - initial.SubtitleTimeMilliseconds) *
            Math.Max(1d, framesPerSecond) /
            (final.VideoTimeMilliseconds - initial.VideoTimeMilliseconds);
        AddFpsRow(fpsPanel, "FPS aktualnych napisów:", measuredSubtitleFps, 8);
        AddFpsRow(fpsPanel, "FPS napisów po zmianie:", framesPerSecond, 28);

        var initialGroup = CreateMeasurementGroup(
            "Pomiar początkowy",
            initial,
            72,
            out _initialDelay,
            out _fromInitialLine,
            "Zmieniaj &od tej linii",
            changeFromInitialLine);
        var finalGroup = CreateMeasurementGroup(
            "Pomiar końcowy",
            final,
            202,
            out _finalDelay,
            out _toFinalLine,
            "Zmieniaj &do tej linii",
            changeToFinalLine);

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(198, 345),
            Size = new Size(75, 27)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(281, 345),
            Size = new Size(75, 27)
        };

        Controls.AddRange([fpsPanel, initialGroup, finalGroup, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _initialDelay.Focus();
            _initialDelay.SelectAll();
        };
    }

    public double InitialDelaySeconds { get; private set; }

    public double FinalDelaySeconds { get; private set; }

    public bool ChangeFromInitialLine => _fromInitialLine.Checked;

    public bool ChangeToFinalLine => _toFinalLine.Checked;

    private static GroupBox CreateMeasurementGroup(
        string title,
        SubtitleGeometryMeasurement measurement,
        int top,
        out TextBox delay,
        out CheckBox rangeLimit,
        string rangeLimitText,
        bool rangeLimitChecked)
    {
        var group = new GroupBox
        {
            Text = title,
            Location = new Point(14, top),
            Size = new Size(531, 119)
        };
        group.Controls.Add(new Label
        {
            Text = "Nr linii:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(8, 21),
            Size = new Size(68, 23)
        });
        group.Controls.Add(new Label
        {
            Text = (measurement.LineIndex + 1).ToString(CultureInfo.CurrentCulture),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(83, 21),
            Size = new Size(80, 23)
        });
        group.Controls.Add(new Label
        {
            Text = "Linia:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(8, 44),
            Size = new Size(68, 23)
        });
        var line = new Label
        {
            Text = measurement.LineText,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(83, 44),
            Size = new Size(432, 23)
        };
        group.Controls.Add(line);
        group.Controls.Add(new Label
        {
            Text = "Opóźnienie:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(8, 69),
            Size = new Size(68, 23)
        });
        delay = new TextBox
        {
            Text = ((measurement.VideoTimeMilliseconds - measurement.SubtitleTimeMilliseconds) / 1000d)
                .ToString("0.########", CultureInfo.CurrentCulture),
            Location = new Point(83, 69),
            Size = new Size(235, 23)
        };
        group.Controls.Add(delay);
        group.Controls.Add(new Label
        {
            Text = "sekund",
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(324, 69),
            Size = new Size(65, 23)
        });
        rangeLimit = new CheckBox
        {
            Text = rangeLimitText,
            AutoSize = true,
            ForeColor = Color.Blue,
            Location = new Point(383, 94),
            Checked = rangeLimitChecked
        };
        group.Controls.Add(rangeLimit);
        return group;
    }

    private static void AddFpsRow(Control parent, string caption, double value, int top)
    {
        parent.Controls.Add(new Label
        {
            Text = caption,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(7, top),
            Size = new Size(155, 21)
        });
        parent.Controls.Add(new Label
        {
            Text = value.ToString("0.###", CultureInfo.InvariantCulture),
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(164, top),
            Size = new Size(70, 21)
        });
    }

    private void AcceptOptions()
    {
        if (!TryParseNumber(_initialDelay.Text, out var initialDelay))
        {
            ShowInvalidValue(_initialDelay);
            return;
        }

        if (!TryParseNumber(_finalDelay.Text, out var finalDelay))
        {
            ShowInvalidValue(_finalDelay);
            return;
        }

        InitialDelaySeconds = initialDelay;
        FinalDelaySeconds = finalDelay;
        DialogResult = DialogResult.OK;
    }

    private void ShowInvalidValue(TextBox textBox)
    {
        MessageBox.Show(
            this,
            "Wpisz poprawną wartość opóźnienia.",
            "Ostrzeżenie",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        textBox.Focus();
        textBox.SelectAll();
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
}
