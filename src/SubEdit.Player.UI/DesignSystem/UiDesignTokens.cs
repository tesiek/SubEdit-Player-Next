namespace SubEdit.Player.UI.DesignSystem;

public enum UiVisualProfile { Standard, Options, Operation }

/// <summary>Logical values at 96 DPI, based on the current SubEdit dialogs.</summary>
public sealed record UiDesignTokens
{
    public static UiDesignTokens Current { get; } = new();
    public Font DialogFont => SystemFonts.MessageBoxFont!;
    public Color Background => SystemColors.Control;
    public Color Text => SystemColors.ControlText;
    public Color OperationCaption => Color.Blue;
    public Color InspectorBackground => SystemColors.Control;
    public Color InspectorHeading => SystemColors.HotTrack;
    public Color Separator => SystemColors.ControlDark;
    public int BodyInset { get; init; } = 15;
    public int OptionsInset { get; init; } = 12;
    public int SectionGap { get; init; } = 8;
    // Semantic roles retain the accepted base rhythm; they need not have equal values.
    public int LabelFieldGap => FieldGap;
    public int OptionGap => RowGap;
    public int IndependentGroupGap => SectionGap;
    public int DependentIndent { get; init; } = 18;
    public int GroupHorizontalPadding { get; init; } = 12;
    public int GroupTopPadding { get; init; } = 8;
    public int GroupBottomPadding { get; init; } = 9;
    public int MultilineTextBreathing { get; init; } = 6;
    public int FieldGap { get; init; } = 6;
    public int RowGap { get; init; } = 3;
    public int ActionGap { get; init; } = 6;
    public int ActionWidth { get; init; } = 75;
    public int ActionHeight { get; init; } = 25;
    // Standard adapter: border/default-button allowance (6) + text/image inset (4).
    public int ButtonVerticalInsets { get; init; } = 10;
    public int OptionsActionHeight { get; init; } = 27;
    public int ScreenInset { get; init; } = 12;
    public int ScrollbarGap { get; init; } = 15;
    public int CompactFieldMinWidth { get; init; } = 56;
    public int FieldChromeWidth { get; init; } = 12;
    public int MessageLineEm { get; init; } = 48;
    public int FormLabelPreferredEm { get; init; } = 24;
    public int FormLabelMinimumEm { get; init; } = 10;
    public int GroupCaptionHorizontalInset { get; init; } = 16;
    public int MinimumEditableFieldEm { get; init; } = 8;
    public int InspectorLabelWidth { get; init; } = 180;

    // Scale spacing once from logical units, never from a previously scaled size.
    public int Space(int logical, Font font, int dpi) =>
        (int)Math.Round(logical * dpi / 96d * Math.Max(1d, font.SizeInPoints / DialogFont.SizeInPoints));

    public static int Pixels(int logical, int dpi) => (int)Math.Round(logical * dpi / 96d);
    public int Inset(UiVisualProfile profile, int dpi) =>
        Pixels(profile == UiVisualProfile.Options ? OptionsInset : BodyInset, dpi);
}
