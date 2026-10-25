using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum SubtitleFrameRateScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class SubtitleFrameRateDialog : SubEditDialog
{
    private static readonly string[] CommonFrameRates =
    [
        "12.000",
        "14.998",
        "23.976",
        "24.000",
        "25.000",
        "29.970",
        "30.000"
    ];

    private readonly ComboBox _currentFrameRate;
    private readonly ComboBox _targetFrameRate;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly ToolTip _toolTip = new();

    public SubtitleFrameRateDialog(double? movieFrameRate)
    {
        Text = "Zmień współczynnik FPS napisów";
        ClientSize = new Size(380, 335);

        var frameRateGroup = new GroupBox
        {
            Text = "Ilość klatek na sekundę",
            Location = new Point(12, 12),
            Size = new Size(356, 104)
        };
        frameRateGroup.Controls.Add(new Label
        {
            Text = "&Aktualna:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(32, 27),
            Size = new Size(88, 23)
        });
        frameRateGroup.Controls.Add(new Label
        {
            Text = "&Docelowa:",
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(32, 64),
            Size = new Size(88, 23)
        });
        _currentFrameRate = CreateFrameRateCombo("23.976", new Point(126, 27));
        _targetFrameRate = CreateFrameRateCombo(
            movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "25.000",
            new Point(126, 64));
        frameRateGroup.Controls.AddRange([_currentFrameRate, _targetFrameRate]);
        Controls.Add(frameRateGroup);

        Controls.Add(new Label
        {
            Text = "FPS filmu:",
            Location = new Point(20, 130),
            Size = new Size(75, 23),
            TextAlign = ContentAlignment.MiddleLeft
        });
        var movieFrameRateLabel = new LinkLabel
        {
            Text = movieFrameRate is > 0d ? FormatFrameRate(movieFrameRate.Value) : "—",
            Location = new Point(95, 130),
            Size = new Size(85, 23),
            TextAlign = ContentAlignment.MiddleLeft,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Enabled = movieFrameRate is > 0d
        };
        if (movieFrameRate is > 0d)
        {
            movieFrameRateLabel.LinkClicked += (_, _) => _targetFrameRate.Text = FormatFrameRate(movieFrameRate.Value);
            _toolTip.SetToolTip(movieFrameRateLabel, "Kliknij aby wpisać tą wartość");
        }
        else
        {
            _toolTip.SetToolTip(movieFrameRateLabel, "Współczynnik FPS nieosiągalny (brak otwartego filmu)");
        }
        Controls.Add(movieFrameRateLabel);

        Controls.Add(new Label
        {
            Text = "funkcja zmiany FPS działa tylko dla formatu mDVD",
            ForeColor = SystemColors.ControlText,
            Location = new Point(20, 160),
            Size = new Size(340, 23),
            TextAlign = ContentAlignment.MiddleLeft
        });

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(12, 192),
            Size = new Size(356, 79)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 16);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 36);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 56);
        _selectionScope.Checked = true;
        Controls.Add(scopeGroup);

        var ok = new Button
        {
            Text = "OK",
            Location = new Point(111, 298),
            Size = new Size(75, 25)
        };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            Location = new Point(194, 298),
            Size = new Size(75, 25)
        };
        Controls.AddRange([ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            _currentFrameRate.Focus();
            _currentFrameRate.SelectAll();
        };
    }

    public double CurrentFrameRate => ParseFrameRate(_currentFrameRate.Text);

    public double TargetFrameRate => ParseFrameRate(_targetFrameRate.Text);

    public SubtitleFrameRateScope SelectedScope =>
        _fromCurrentLineScope.Checked
            ? SubtitleFrameRateScope.FromCurrentLine
            : _toCurrentLineScope.Checked
                ? SubtitleFrameRateScope.ToCurrentLine
                : SubtitleFrameRateScope.Selection;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ComboBox CreateFrameRateCombo(string value, Point location)
    {
        var combo = new ComboBox
        {
            Location = location,
            Size = new Size(118, 23),
            MaxLength = 10,
            Sorted = true,
            Text = value
        };
        combo.Items.AddRange(CommonFrameRates);
        return combo;
    }

    private static RadioButton AddScopeOption(Control parent, string text, int top)
    {
        var option = new RadioButton
        {
            Text = text,
            ForeColor = SystemColors.ControlText,
            Location = new Point(8, top),
            AutoSize = true
        };
        parent.Controls.Add(option);
        return option;
    }

    private void AcceptOptions()
    {
        if (!TryParseFrameRate(_currentFrameRate.Text, out var currentFrameRate) || currentFrameRate <= 0d)
        {
            ShowValidation(_currentFrameRate, "Wpisz prawidłowy aktualny współczynnik FPS.");
            return;
        }

        if (!TryParseFrameRate(_targetFrameRate.Text, out var targetFrameRate) || targetFrameRate <= 0d)
        {
            ShowValidation(_targetFrameRate, "Wpisz prawidłowy docelowy współczynnik FPS.");
            return;
        }

        if (currentFrameRate == targetFrameRate)
        {
            ShowValidation(_currentFrameRate, "Aktualny i docelowy współczynnik FPS muszą być różne.");
            return;
        }

        DialogResult = DialogResult.OK;
    }

    private void ShowValidation(Control control, string message)
    {
        MessageBox.Show(this, message, "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        control.Focus();
        if (control is ComboBox combo)
        {
            combo.SelectAll();
        }
    }

    private static bool TryParseFrameRate(string text, out double value) =>
        (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
         double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) &&
        double.IsFinite(value);

    private static double ParseFrameRate(string text) =>
        TryParseFrameRate(text, out var value) ? value : 0d;

    private static string FormatFrameRate(double value) =>
        value.ToString("0.000", CultureInfo.InvariantCulture);
}
