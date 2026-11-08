using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

// Scoped to the pure measurement pass. Never assigns a property on a page
// or creates a handle to query its metrics. The visible tab uses live metrics.
public sealed class OptionsMeasureContext : IDisposable
{
    [ThreadStatic] private static OptionsMeasureContext? current;
    private readonly OptionsMeasureContext? previous;
    private readonly int dpi;
    private readonly Font effectiveFont;
    private readonly OptionsDialogLayout layout;
    private Label? label;
    private CheckBox? check;
    private RadioButton? radio;
    private ComboBox? combo;
    private NumericUpDown? number;

    private OptionsMeasureContext(Form form, OptionsDialogLayout layout)
    {
        dpi = (int)GetDpiForWindow(form.Handle);
        if (dpi == 0 || dpi != form.DeviceDpi) throw new InvalidOperationException("Dialog DPI has not settled.");
        effectiveFont = form.Font;
        this.layout = layout;
        previous = current;
        current = this;
    }

    internal static OptionsMeasureContext Begin(Form form, OptionsDialogLayout layout) => new(form, layout);

    private static bool Project(Control control) =>
        current is { } context && !control.IsHandleCreated && context.dpi != control.DeviceDpi && context.layout.Contains(control);

    public static int Dpi(Control control) => Project(control) ? current!.dpi : control.DeviceDpi;
    public static Font FontOf(Control control) => Project(control) ? current!.effectiveFont : control.Font;

    internal static int GroupCaptionTop(GroupBox group)
    {
        var currentTop = group.DisplayRectangle.Top - group.Padding.Top;
        return Math.Max(0, currentTop + FontOf(group).Height - group.Font.Height);
    }

    internal static int SpinnerWidth(NumericUpDown control, int width) =>
        Project(control) ? (int)Math.Round((double)width * Dpi(control) / control.DeviceDpi) : width;

    internal static Size Preferred(Control control, Size proposed)
    {
        if (!Project(control)) return control.GetPreferredSize(proposed);

        var context = current!;
        Control? probe = control switch
        {
            Label => context.label ??= new Label(),
            RadioButton => context.radio ??= new RadioButton(),
            CheckBox => context.check ??= new CheckBox(),
            ComboBox => context.combo ??= new ComboBox(),
            NumericUpDown => context.number ??= new NumericUpDown(),
            // Options-specific custom controls implement their own projected measurements.
            _ => null,
        };
        if (probe is null) return control.GetPreferredSize(proposed);

        probe.Font = context.effectiveFont;
        probe.Padding = control.Padding;
        probe.MinimumSize = control.MinimumSize;
        probe.MaximumSize = control.MaximumSize;
        switch (control, probe)
        {
            case (Label source, Label target):
                target.BorderStyle = source.BorderStyle;
                target.FlatStyle = source.FlatStyle;
                target.UseCompatibleTextRendering = source.UseCompatibleTextRendering;
                target.UseMnemonic = source.UseMnemonic;
                target.Text = source.Text;
                break;
            case (ButtonBase source, ButtonBase target):
                target.FlatStyle = source.FlatStyle;
                target.UseCompatibleTextRendering = source.UseCompatibleTextRendering;
                target.Text = source.Text;
                break;
            case (ComboBox source, ComboBox target):
                target.DropDownStyle = source.DropDownStyle;
                target.FormattingEnabled = source.FormattingEnabled;
                target.Width = source.Width;
                break;
            case (NumericUpDown source, NumericUpDown target):
                target.BorderStyle = source.BorderStyle;
                target.Maximum = source.Maximum;
                target.Minimum = source.Minimum;
                target.DecimalPlaces = source.DecimalPlaces;
                target.Hexadecimal = source.Hexadecimal;
                target.ThousandsSeparator = source.ThousandsSeparator;
                break;
        }
        var size = probe.GetPreferredSize(proposed);
        if (probe.IsHandleCreated) throw new InvalidOperationException("A measurement probe created an HWND.");
        return size;
    }

    public void Dispose()
    {
        if (current != this) return;
        current = previous;
        label?.Dispose();
        check?.Dispose();
        radio?.Dispose();
        combo?.Dispose();
        number?.Dispose();
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
}
