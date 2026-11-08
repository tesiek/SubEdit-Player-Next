using System.Runtime.InteropServices;

namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Metric policies for native controls, without replacing their drawing or input behavior.</summary>
public static class NativeControlLayout
{
    private sealed class NumericMetric { public string Key=""; public Font? Font; public int Width; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NumericUpDown,NumericMetric> NumericMetrics=new();
    /// <summary>Range/formatted content + native spinner/border + a small symmetric reading allowance.</summary>
    public static int NumericWidth(NumericUpDown number)
    {
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        var spinner=number.Controls.Cast<Control>().FirstOrDefault(c=>c is not TextBox)?.Width??UiDesignTokens.Pixels(16,OptionsMeasureContext.Dpi(number));
        spinner=OptionsMeasureContext.SpinnerWidth(number,spinner);
        var inset=Math.Max(number.Width-number.ClientSize.Width,UiDesignTokens.Pixels(number.BorderStyle==BorderStyle.None?0:number.BorderStyle==BorderStyle.FixedSingle?2:4,OptionsMeasureContext.Dpi(number)));
        var key=$"{number.Minimum}|{number.Maximum}|{number.DecimalPlaces}|{number.ThousandsSeparator}|{number.Hexadecimal}|{OptionsMeasureContext.Dpi(number)}|{spinner}|{inset}|{culture.NumberFormat.NumberDecimalSeparator}|{culture.NumberFormat.NumberGroupSeparator}|{culture.NumberFormat.NegativeSign}";
        var cache=NumericMetrics.GetOrCreateValue(number);
        if(cache.Key==key&&ReferenceEquals(cache.Font,OptionsMeasureContext.FontOf(number)))return Math.Max(number.MinimumSize.Width,cache.Width);
        string Format(decimal v)=>number.Hexadecimal?((long)v).ToString("X",culture):v.ToString((number.ThousandsSeparator?"N":"F")+number.DecimalPlaces,culture);
        var samples=new[]{Format(number.Minimum),Format(number.Maximum)};
        var widestDigit=Enumerable.Range(0,10).Select(d=>d.ToString()).MaxBy(d=>TextRenderer.MeasureText(d,OptionsMeasureContext.FontOf(number)).Width)![0];
        // Reserve the widest glyph in every digit position, independent of the current Value.
        var text=samples.SelectMany(t=>new[]{t,new string(t.Select(c=>char.IsDigit(c)?widestDigit:c).ToArray())})
            .Max(t=>TextRenderer.MeasureText(t,OptionsMeasureContext.FontOf(number)).Width);
        cache.Key=key;cache.Font=OptionsMeasureContext.FontOf(number);cache.Width=text+spinner+inset+UiDesignTokens.Pixels(8,OptionsMeasureContext.Dpi(number));
        return Math.Max(number.MinimumSize.Width,cache.Width);
    }
    public static void ConfigureButton(Button button)
    {
        button.AutoSize = false;
        // Button.GetPreferredSizeCore unions with the previous Size in GrowOnly mode,
        // even when AutoSize is false. The layout owns Size; metrics must be reversible.
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = Padding.Empty;
        button.FlatStyle = FlatStyle.Standard;
        button.UseCompatibleTextRendering = false;
    }
    public static int MeasureButtonHeight(Button button, int width) => TextRenderer.MeasureText(button.Text, OptionsMeasureContext.FontOf(button),
        new Size(Math.Max(1, width - UiDesignTokens.Pixels(10, OptionsMeasureContext.Dpi(button))), int.MaxValue), TextFormatFlags.WordBreak).Height
        + button.Padding.Vertical + UiDesignTokens.Pixels(UiDesignTokens.Current.ButtonVerticalInsets, OptionsMeasureContext.Dpi(button));
    public static void BindComboBox(ComboBox combo)
    {
        combo.IntegralHeight = true;
        combo.MaxDropDownItems = 8;
        combo.DropDown += (_, _) =>
        {
            FitDropDown(combo);
            // Win32 can keep a wide popup anchored at the left edge of the combo.
            // Clamp its real HWND once it exists, keeping native painting and input.
            combo.BeginInvoke((Action)(() => { if (!combo.IsDisposed && combo.DroppedDown) FitNativePopup(combo); }));
        };
        combo.FontChanged += (_, _) => FitDropDown(combo);
        combo.DpiChangedAfterParent += (_, _) => FitDropDown(combo);
        var selectedFromList = false;
        void ShowBeginning()
        {
            if (combo.DropDownStyle != ComboBoxStyle.DropDown || combo.IsDisposed || !combo.IsHandleCreated) return;
            combo.BeginInvoke((Action)(() =>
            {
                if (combo.IsDisposed || combo.DroppedDown) return;
                selectedFromList = false;
                combo.Select(0, 0);
                var info = new ComboInfo { Size = Marshal.SizeOf<ComboInfo>() };
                if (GetComboBoxInfo(combo.Handle, ref info) && info.Edit != IntPtr.Zero)
                    SendMessage(info.Edit, 0x00B7, IntPtr.Zero, IntPtr.Zero); // EM_SCROLLCARET
            }));
        }
        combo.SelectionChangeCommitted += (_, _) => { selectedFromList = true; ShowBeginning(); };
        combo.DropDownClosed += (_, _) => { if (selectedFromList) ShowBeginning(); selectedFromList = false; };
    }

    public static int ContentWidth(ComboBox combo) => combo.Items.Cast<object>().Select(item =>
        TextRenderer.MeasureText(combo.GetItemText(item) ?? string.Empty, OptionsMeasureContext.FontOf(combo)).Width).DefaultIfEmpty(0).Max()
        + UiDesignTokens.Pixels(24, OptionsMeasureContext.Dpi(combo));

    public static void FitDropDown(ComboBox combo)
    {
        if (combo is OptionsFontCombo && !combo.IsHandleCreated) return;
        var area = Screen.FromControl(combo).WorkingArea;
        var padding = UiDesignTokens.Pixels(24, OptionsMeasureContext.Dpi(combo));
        combo.DropDownWidth = Math.Max(1, Math.Min(area.Width - padding, Math.Max(combo.Width, ContentWidth(combo))));
    }

    private static void FitNativePopup(ComboBox combo)
    {
        var info = new ComboInfo { Size = Marshal.SizeOf<ComboInfo>() };
        if (!GetComboBoxInfo(combo.Handle, ref info) || !GetWindowRect(info.List, out var rect)) return;
        var area = Screen.FromControl(combo).WorkingArea;
        var left = Math.Clamp(rect.Left, area.Left, Math.Max(area.Left, area.Right - (rect.Right - rect.Left)));
        var top = Math.Clamp(rect.Top, area.Top, Math.Max(area.Top, area.Bottom - (rect.Bottom - rect.Top)));
        if (left != rect.Left || top != rect.Top) SetWindowPos(info.List, IntPtr.Zero, left, top, 0, 0, 0x0015);
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ComboInfo { public int Size; public Rect Item, Button; public int State; public IntPtr Combo, Edit, List; }
    [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr window, ref ComboInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
