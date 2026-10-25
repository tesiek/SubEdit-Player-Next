using SubEdit.Player.Core.Configuration;
using System.Globalization;

namespace SubEdit.Player.WinForms;

internal enum RapidSubtitleMergeScope
{
    Selection,
    FromCurrentLine,
    ToCurrentLine
}

internal sealed class RapidSubtitleMergeDialog : SubEditDialog
{
    private readonly TextBox _fiftyCharactersSeconds;
    private readonly TextBox _firstToLastEndSeconds;
    private readonly TextBox _firstToLastStartSeconds;
    private readonly RadioButton _addAsConsecutiveLines;
    private readonly RadioButton _limitLineCount;
    private readonly TextBox _maximumLines;
    private readonly CheckBox _limitCharacters;
    private readonly TextBox _maximumCharacters;
    private readonly RadioButton _selectionScope;
    private readonly RadioButton _fromCurrentLineScope;
    private readonly RadioButton _toCurrentLineScope;
    private readonly CheckBox _saveAsDefault;

    public RapidSubtitleMergeDialog(RapidSubtitleMergeSettings defaults)
    {
        Text = "Połącz szybko znikające napisy";
        ClientSize = new Size(530, 422);

        var timingGroup = new GroupBox
        {
            Location = new Point(12, 12),
            Size = new Size(506, 168)
        };
        _fiftyCharactersSeconds = AddTimeRow(
            timingGroup,
            "Złącz napis jeżeli czas wyświetlenia 50 znaków, tego napisu jest\r\nmniejszy niż",
            24,
            FormatNumber(defaults.MaximumFiftyCharacterDisplaySeconds));
        _firstToLastEndSeconds = AddTimeRow(
            timingGroup,
            "a czas między wyświetleniem pierwszego napisu a schowaniem\r\nostatniego jest mniejszy niż",
            66,
            FormatNumber(defaults.MaximumFirstToLastEndSeconds),
            "sekund");
        _firstToLastStartSeconds = AddTimeRow(
            timingGroup,
            "lub czas między wyświetleniami pierwszego i ostatniego napisu jest\r\nmniejszy niż",
            108,
            FormatNumber(defaults.MaximumFirstToLastStartSeconds),
            "sekund");

        var lineGroup = new GroupBox
        {
            Text = "Sposób tworzenia wierszy nowego napisu",
            Location = new Point(12, 192),
            Size = new Size(506, 72)
        };
        _addAsConsecutiveLines = new RadioButton
        {
            Text = "Dodaj jako kolejne wiersze",
            Location = new Point(12, 20),
            AutoSize = true,
            Checked = defaults.AddAsConsecutiveLines
        };
        _limitLineCount = new RadioButton
        {
            Text = "Złącz tak, żeby nie było więcej niż",
            Location = new Point(12, 43),
            AutoSize = true,
            Checked = !defaults.AddAsConsecutiveLines
        };
        _maximumLines = new TextBox
        {
            Location = new Point(264, 40),
            Size = new Size(34, 23),
            MaxLength = 1,
            Text = defaults.MaximumLines.ToString(CultureInfo.InvariantCulture)
        };
        lineGroup.Controls.AddRange([
            _addAsConsecutiveLines,
            _limitLineCount,
            _maximumLines,
            new Label { Text = "wierszy", Location = new Point(304, 42), AutoSize = true }
        ]);
        _addAsConsecutiveLines.CheckedChanged += (_, _) => UpdateAvailability();
        _limitLineCount.CheckedChanged += (_, _) => UpdateAvailability();

        _limitCharacters = new CheckBox
        {
            Text = "Nie łącz jeżeli długość napisu przekroczy",
            Location = new Point(17, 278),
            AutoSize = true,
            Checked = defaults.LimitCharacters
        };
        _maximumCharacters = new TextBox
        {
            Location = new Point(279, 275),
            Size = new Size(48, 23),
            MaxLength = 3,
            Text = defaults.MaximumCharacters.ToString(CultureInfo.InvariantCulture)
        };
        _limitCharacters.CheckedChanged += (_, _) => UpdateAvailability();
        var charactersLabel = new Label { Text = "znaków.", Location = new Point(334, 278), AutoSize = true };

        var scopeGroup = new GroupBox
        {
            Text = "Obszar zmian napisów",
            ForeColor = Color.Blue,
            Location = new Point(12, 307),
            Size = new Size(506, 42)
        };
        _selectionScope = AddScopeOption(scopeGroup, "Według zaznaczenia", 10);
        _fromCurrentLineScope = AddScopeOption(scopeGroup, "Od aktualnej linii", 175);
        _toCurrentLineScope = AddScopeOption(scopeGroup, "Do aktualnej linii", 335);
        _selectionScope.Checked = true;

        _saveAsDefault = new CheckBox
        {
            Text = "Zachowaj ustawienia jako domyślne",
            Location = new Point(17, 359),
            AutoSize = true
        };
        var ok = new Button { Text = "OK", Location = new Point(184, 388), Size = new Size(75, 25) };
        ok.Click += (_, _) => AcceptOptions();
        var cancel = new Button
        {
            Text = "Anuluj",
            Location = new Point(267, 388),
            Size = new Size(75, 25),
            DialogResult = DialogResult.Cancel
        };
        Controls.AddRange([timingGroup, lineGroup, _limitCharacters, _maximumCharacters, charactersLabel, scopeGroup, _saveAsDefault, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
        UpdateAvailability();
        Shown += (_, _) =>
        {
            _fiftyCharactersSeconds.Focus();
            _fiftyCharactersSeconds.SelectAll();
        };
    }

    public bool SaveAsDefault => _saveAsDefault.Checked;

    public RapidSubtitleMergeScope SelectedScope => _fromCurrentLineScope.Checked
        ? RapidSubtitleMergeScope.FromCurrentLine
        : _toCurrentLineScope.Checked ? RapidSubtitleMergeScope.ToCurrentLine : RapidSubtitleMergeScope.Selection;

    public RapidSubtitleMergeSettings CreateSettings() => new()
    {
        MaximumFiftyCharacterDisplaySeconds = ParseNumber(_fiftyCharactersSeconds.Text),
        MaximumFirstToLastEndSeconds = ParseNumber(_firstToLastEndSeconds.Text),
        MaximumFirstToLastStartSeconds = ParseNumber(_firstToLastStartSeconds.Text),
        AddAsConsecutiveLines = _addAsConsecutiveLines.Checked,
        MaximumLines = int.Parse(_maximumLines.Text, CultureInfo.InvariantCulture),
        LimitCharacters = _limitCharacters.Checked,
        MaximumCharacters = int.Parse(_maximumCharacters.Text, CultureInfo.InvariantCulture)
    };

    private TextBox AddTimeRow(Control parent, string label, int top, string value, string suffix = "sekund")
    {
        parent.Controls.Add(new Label { Text = label, Location = new Point(18, top), Size = new Size(350, 34) });
        var textBox = new TextBox { Text = value, Location = new Point(372, top + 3), Size = new Size(48, 23), MaxLength = 3 };
        parent.Controls.Add(textBox);
        parent.Controls.Add(new Label { Text = suffix, Location = new Point(426, top + 6), AutoSize = true });
        return textBox;
    }

    private static RadioButton AddScopeOption(Control parent, string text, int left) => new()
    {
        Text = text,
        ForeColor = SystemColors.ControlText,
        Location = new Point(left, 17),
        AutoSize = true,
        Parent = parent
    };

    private void UpdateAvailability()
    {
        _maximumLines.Enabled = _limitLineCount.Checked;
        _maximumCharacters.Enabled = _limitCharacters.Checked;
    }

    private void AcceptOptions()
    {
        if (!IsPositiveNumber(_fiftyCharactersSeconds) ||
            !IsPositiveNumber(_firstToLastEndSeconds) ||
            !IsPositiveNumber(_firstToLastStartSeconds) ||
            !IsPositiveInteger(_maximumLines, 1, 9) ||
            (_limitCharacters.Checked && !IsPositiveInteger(_maximumCharacters, 1, 999)))
        {
            return;
        }
        DialogResult = DialogResult.OK;
    }

    private bool IsPositiveNumber(TextBox textBox)
    {
        if (TryParseNumber(textBox.Text, out var value) && value is >= 0.1d and <= 999d)
        {
            return true;
        }
        ShowValidation(textBox, "Wpisz wartość z zakresu 0,1–999.");
        return false;
    }

    private bool IsPositiveInteger(TextBox textBox, int minimum, int maximum)
    {
        if (int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            value >= minimum &&
            value <= maximum)
        {
            return true;
        }
        ShowValidation(textBox, $"Wpisz wartość z zakresu {minimum}–{maximum}.");
        return false;
    }

    private void ShowValidation(TextBox textBox, string message)
    {
        MessageBox.Show(this, message, "Ostrzeżenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        textBox.Focus();
        textBox.SelectAll();
    }

    private static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static double ParseNumber(string text) => TryParseNumber(text, out var value) ? value : 0d;
    private static string FormatNumber(double value) => value.ToString("0.########", CultureInfo.CurrentCulture);
}
