using System.Globalization;

namespace SubEdit.Player.WinForms.Controls;

internal readonly record struct SubtitleInfoSnapshot(
    int EditorColumn,
    int EditorLineNumber,
    int EditorLineCount,
    int? TextCursorPosition,
    int? TextLength,
    int? SubtitleLineCount,
    int? SubtitleLineCursorPosition,
    int? SubtitleLineLength,
    int? SubtitleLineNumber,
    long? DisplayDurationMilliseconds,
    long? FiftyCharactersDurationMilliseconds);

/// <summary>Compact editor information bar reconstructed from FormSubEdit.dfm.</summary>
internal sealed class SubtitleInfoBar : Panel
{
    private readonly Font _regularFont = new("Verdana", 7f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _boldFont = new("Verdana", 7f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly ToolTip _toolTip = new();
    private readonly Label _textCursor;
    private readonly Label _textLength;
    private readonly Label _subtitleLineCount;
    private readonly Label _subtitleLineCursor;
    private readonly Label _subtitleLineLength;
    private readonly Label _subtitleLineNumber;
    private readonly Label _displayDuration;
    private readonly Label _fiftyCharactersDuration;
    private readonly Label _editorColumn;
    private readonly Label _editorLine;
    private readonly Label _editorLineCount;
    private readonly List<(Label Label, int Left, int Width)> _labelLayout = [];
    private int _editorTextLeft;

    public SubtitleInfoBar()
    {
        Dock = DockStyle.Bottom;
        Height = 16;
        Margin = Padding.Empty;
        Padding = Padding.Empty;
        BackColor = SystemColors.Control;
        TabStop = false;
        HandleCreated += (_, _) => UpdateDpiLayout();
        DpiChangedAfterParent += (_, _) => UpdateDpiLayout();

        _textCursor = AddLabel(4, 26, "Pozycja kursora w tekście napisu", ContentAlignment.MiddleRight);
        _textLength = AddLabel(34, 30, "Długość tekstu napisu", ContentAlignment.MiddleLeft, Color.Blue);
        _subtitleLineCount = AddLabel(66, 22, "Ilość wierszy w napisie", ContentAlignment.MiddleLeft, SystemColors.ControlDarkDark);
        _subtitleLineCursor = AddLabel(92, 29, "Pozycja kursora w wierszu", ContentAlignment.MiddleRight);
        _subtitleLineLength = AddLabel(123, 30, "Długość wiersza", ContentAlignment.MiddleLeft, Color.Blue);
        _subtitleLineNumber = AddLabel(157, 30, "Nr wiersza, na którym jest kursor", ContentAlignment.MiddleLeft, SystemColors.ControlDarkDark);
        _displayDuration = AddLabel(191, 41, "Czas pokazywania napisu", ContentAlignment.MiddleLeft, Color.FromArgb(230, 0, 0));
        _fiftyCharactersDuration = AddLabel(242, 44, "Czas pokazania 50 znaków napisu", ContentAlignment.MiddleLeft, Color.FromArgb(230, 0, 0));
        _editorColumn = AddLabel(296, 33, "Pozycja kursora w linii", ContentAlignment.MiddleRight);
        _editorLine = AddLabel(335, 40, "Nr linii w edytorze", ContentAlignment.MiddleRight);
        _editorLineCount = AddLabel(377, 34, "Ilość linii w edytorze", ContentAlignment.MiddleLeft, Color.Blue);

        SetSnapshot(new SubtitleInfoSnapshot(1, 1, 1, null, null, null, null, null, null, null, null));
    }

    public void SetSnapshot(SubtitleInfoSnapshot snapshot)
    {
        _textCursor.Text = snapshot.TextCursorPosition?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
        _textLength.Text = snapshot.TextLength is { } textLength ? $":{textLength}" : ":N/A";
        _subtitleLineCount.Text = snapshot.SubtitleLineCount is { } lineCount ? $"({lineCount})" : "(0)";
        _subtitleLineCursor.Text = snapshot.SubtitleLineCursorPosition?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
        _subtitleLineLength.Text = snapshot.SubtitleLineLength is { } lineLength ? $":{lineLength}" : ":N/A";
        _subtitleLineNumber.Text = snapshot.SubtitleLineNumber is { } lineNumber ? $"({lineNumber})" : "N/A";
        _displayDuration.Text = snapshot.DisplayDurationMilliseconds is { } duration
            ? $"t {FormatSeconds(duration)}"
            : "t N/A";
        _fiftyCharactersDuration.Text = snapshot.FiftyCharactersDurationMilliseconds is { } durationForFifty
            ? $"t' {FormatSeconds(durationForFifty)}"
            : "t' N/A";
        _editorColumn.Text = $"x {snapshot.EditorColumn}";
        _editorLine.Text = $"y {snapshot.EditorLineNumber}";
        _editorLineCount.Text = $":{snapshot.EditorLineCount}";
    }

    public void SetEditorTextLeft(int editorTextLeft)
    {
        _editorTextLeft = Math.Max(0, editorTextLeft);
        UpdateDpiLayout();
    }

    public void UpdateDpiLayout()
    {
        var scale = DeviceDpi / 96f;
        var top = ScaleLogicalPixels(1, scale);
        var labelHeight = Math.Max(
            ScaleLogicalPixels(13, scale),
            TextRenderer.MeasureText("Hg", _regularFont, Size.Empty, TextFormatFlags.NoPadding).Height);

        foreach (var (label, left, width) in _labelLayout)
        {
            label.Left = _editorTextLeft + ScaleLogicalPixels(left, scale);
            label.Top = top;
            label.Width = ScaleLogicalPixels(width, scale);
            label.Height = labelHeight;
        }

        Height = Math.Max(ScaleLogicalPixels(16, scale), labelHeight + (2 * top));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
            _regularFont.Dispose();
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private Label AddLabel(
        int left,
        int width,
        string hint,
        ContentAlignment alignment,
        Color? color = null,
        bool bold = false)
    {
        var label = new Label
        {
            Left = left,
            Top = 1,
            Width = width,
            Height = 13,
            AutoSize = false,
            TextAlign = alignment,
            Font = bold ? _boldFont : _regularFont,
            ForeColor = color ?? SystemColors.WindowText,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        _toolTip.SetToolTip(label, hint);
        _labelLayout.Add((label, left, width));
        Controls.Add(label);
        return label;
    }

    private static int ScaleLogicalPixels(int value, float scale) =>
        Math.Max(1, (int)Math.Round(value * scale));

    private static string FormatSeconds(long milliseconds) =>
        (milliseconds / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + "s";
}
