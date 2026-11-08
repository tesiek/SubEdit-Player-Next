namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Optional native unit/description next to a compact editor; no extra layout table.</summary>
public sealed class FieldValueLayout : UserControl
{
    public Control Editor { get; }
    public Label Description { get; }
    private readonly bool _keepTogether;
    private readonly bool _descriptionFirst;
    private readonly int? _logicalEditorWidth;
    private readonly string? _widthSample;
    public FieldValueLayout(Control editor, string description, bool keepTogether = false, bool descriptionFirst = false, int? logicalEditorWidth = null, string? widthSample = null)
    {
        _keepTogether = keepTogether;
        _descriptionFirst = descriptionFirst;
        _logicalEditorWidth = logicalEditorWidth;
        _widthSample = widthSample;
        Editor = editor;
        Description = new Label { Text = description, AutoSize = false, TabStop = false, UseMnemonic = false };
        editor.TabIndex = 0;
        Controls.Add(editor);
        Controls.Add(Description);
        Margin = Padding.Empty;
        AutoScaleMode = AutoScaleMode.Inherit;
    }
    internal int EditorColumnOffset => _descriptionFirst ? DescriptionWidth : 0;
    internal Func<int>? CaptionWidthProvider;
    public int DescriptionWidth => (CaptionWidthProvider?.Invoke() ?? TextRenderer.MeasureText(Description.Text, OptionsMeasureContext.FontOf(Description)).Width)
        + UiDesignTokens.Pixels(UiDesignTokens.Current.LabelFieldGap, OptionsMeasureContext.Dpi(this));
    public Size NaturalSize
    {
        get
        {
            var width = _logicalEditorWidth is { } logical
                ? (int)Math.Ceiling(UiDesignTokens.Pixels(logical, OptionsMeasureContext.Dpi(this)) * OptionsMeasureContext.FontOf(this).SizeInPoints / UiDesignTokens.Current.DialogFont.SizeInPoints)
                : Math.Max(UiDesignTokens.Pixels(UiDesignTokens.Current.CompactFieldMinWidth, OptionsMeasureContext.Dpi(this)),
                    TextRenderer.MeasureText(_widthSample ?? "999", OptionsMeasureContext.FontOf(Editor)).Width + UiDesignTokens.Pixels(UiDesignTokens.Current.FieldChromeWidth, OptionsMeasureContext.Dpi(this)));
            if(Editor is NumericUpDown number)
            {
                width = NativeControlLayout.NumericWidth(number);
            }
            return new Size(Math.Max(Editor.MinimumSize.Width, width) + DescriptionWidth,
                Math.Max(OptionsMeasureContext.Preferred(Editor, Size.Empty).Height,TextRenderer.MeasureText(Description.Text,OptionsMeasureContext.FontOf(Description)).Height));
        }
    }
    public int MeasureHeight(int width) => Arrange(width, false);
    private int Arrange(int width, bool apply)
    {
        var gap = UiDesignTokens.Pixels(UiDesignTokens.Current.LabelFieldGap, OptionsMeasureContext.Dpi(this));
        var fieldHeight = OptionsMeasureContext.Preferred(Editor, Size.Empty).Height;
        var descriptionWidth = DescriptionWidth - gap;
        var fieldMinimum = Math.Max(UiDesignTokens.Pixels(UiDesignTokens.Current.CompactFieldMinWidth, OptionsMeasureContext.Dpi(this)),
            TextRenderer.MeasureText(_widthSample ?? new string('9', Math.Clamp((Editor as TextBox)?.MaxLength ?? 3, 2, 8)), OptionsMeasureContext.FontOf(Editor)).Width
                + UiDesignTokens.Pixels(UiDesignTokens.Current.FieldChromeWidth, OptionsMeasureContext.Dpi(this)));
        if(Editor is NumericUpDown numeric) fieldMinimum=NativeControlLayout.NumericWidth(numeric);
        var stacked = !_keepTogether && fieldMinimum + gap + descriptionWidth > width;
        var editorWidth = stacked ? Math.Min(width, fieldMinimum) : Math.Max(1, width - descriptionWidth - gap);
        if(Editor is NumericUpDown numericEditor) editorWidth=NativeControlLayout.NumericWidth(numericEditor);
        var labelWidth = stacked ? width : descriptionWidth;
        var text = TextRenderer.MeasureText(Description.Text, OptionsMeasureContext.FontOf(Description), new Size(Math.Max(1, labelWidth), int.MaxValue), TextFormatFlags.WordBreak);
        var height = stacked ? fieldHeight + gap + text.Height : Math.Max(fieldHeight, text.Height);
        if (apply)
        {
            Editor.Bounds = new Rectangle(_descriptionFirst && !stacked ? descriptionWidth + gap : 0, _descriptionFirst ? (height - fieldHeight) / 2 : 0, Math.Max(1, editorWidth), fieldHeight);
            Description.Bounds = new Rectangle(stacked || _descriptionFirst ? 0 : editorWidth + gap, stacked ? fieldHeight + gap : (height - text.Height) / 2,
                Math.Max(1, labelWidth), text.Height);
        }
        return height;
    }
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Arrange(ClientSize.Width, true); }
}
