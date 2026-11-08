namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Native peer radios/checkboxes plus native labels; no custom drawing.</summary>
public sealed class NativeOptionList : UserControl
{
    private sealed record Entry(ButtonBase Button, Label Caption, Label? Description, ButtonBase? Parent)
    {
        internal IDialogContent? Body { get; set; }
    }
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<(string Text, Font Font, int Dpi, int Width), Size> _measurements = [];
    private readonly Dictionary<Font, int> _lineHeights = [];
    private bool _arranging;
    private Func<int>? _leadingInset;
    private int LeadingInset => _leadingInset?.Invoke() ?? 0;
    public void AlignToEditorColumn(FieldValueLayout column) { _leadingInset=()=>column.EditorColumnOffset;InvalidateMeasurements(); }
    public event EventHandler? MetricsChanged;
    public Label CaptionFor(ButtonBase button) => _entries.Single(entry => entry.Button == button).Caption;

    public T Add<T>(T button, string caption, string? description = null, ButtonBase? dependsOn = null) where T : ButtonBase
    {
        button.Text = string.Empty;
        button.AccessibleName = caption;
        button.AutoSize = false;
        button.FlatStyle = FlatStyle.Standard;
        if (button is RadioButton radio) radio.CheckAlign = ContentAlignment.MiddleCenter;
        if (button is CheckBox check) check.CheckAlign = ContentAlignment.MiddleCenter;
        var label = new OptionCaption(button) { Text = caption, AutoSize = false, TextAlign = ContentAlignment.TopLeft,
            UseCompatibleTextRendering = false, UseMnemonic = true, TabStop = false,
            Padding = new Padding(UiDesignTokens.Pixels(2, OptionsMeasureContext.Dpi(this))) };
        if (dependsOn is not null && !_entries.Any(entry => entry.Button == dependsOn))
            throw new ArgumentException("The dependency must precede its child in the same list.", nameof(dependsOn));
        button.TabIndex = _entries.Count * 3;
        label.Enabled = button.Enabled;
        label.Click += (_, _) => Activate(button);
        button.EnabledChanged += (_, _) => { label.Enabled = button.Enabled; TabStop=_entries.Any(entry=>entry.Button.Enabled); };
        button.GotFocus += (_, _) => label.Invalidate();
        button.LostFocus += (_, _) => label.Invalidate();
        Label? detail = null;
        if (!string.IsNullOrEmpty(description))
        {
            detail = new Label { Text = description, AutoSize = false, TabStop = false,
                UseCompatibleTextRendering = false, UseMnemonic = false, Padding = label.Padding };
            detail.Click += (_, _) => Activate(button);
            detail.Enabled = button.Enabled;
            button.EnabledChanged += (_, _) => detail.Enabled = button.Enabled;
            Controls.Add(detail);
        }
        void CaptionChanged(object? sender, EventArgs args)
        {
            button.AccessibleName = detail is null ? label.Text : $"{label.Text} {detail.Text}";
            InvalidateMeasurements();
        }
        label.TextChanged += CaptionChanged;
        if (detail is not null) detail.TextChanged += CaptionChanged;
        _entries.Add(new Entry(button, label, detail, dependsOn));
        button.AccessibleName = detail is null ? label.Text : $"{label.Text} {detail.Text}";
        if (dependsOn is not null) BindEnabled(button, dependsOn);
        Controls.Add(button);
        Controls.Add(label); // All RadioButtons share this parent: native grouping and arrow navigation.
        InvalidateMeasurements();
        return button;
    }

    /// <summary>Child content follows its owner in reading/Tab order without another layout table.</summary>
    public void AddDependentContent(ButtonBase owner, IDialogContent content)
    {
        var entry = _entries.Single(item => item.Button == owner);
        if (entry.Body is not null) throw new InvalidOperationException("Owner already has a dependent block.");
        entry.Body = content;
        content.View.TabIndex = owner.TabIndex + 1;
        Controls.Add(content.View);
        content.MetricsChanged += (_, _) => InvalidateMeasurements();
        BindEnabled(content.View, owner);
        InvalidateMeasurements();
    }

    public static void BindEnabled(Control dependent, ButtonBase owner)
    {
        void Update(object? sender, EventArgs e) => dependent.Enabled = owner.Enabled && owner switch
        {
            CheckBox check => check.Checked,
            RadioButton radio => radio.Checked,
            _ => throw new ArgumentException("Dependency owner must be a native option.", nameof(owner))
        };
        if (owner is CheckBox check) check.CheckedChanged += Update;
        else if (owner is RadioButton radio) radio.CheckedChanged += Update;
        else throw new ArgumentException("Dependency owner must be a native option.", nameof(owner));
        owner.EnabledChanged += Update;
        Update(null, EventArgs.Empty);
    }

    private int Depth(Entry entry) => entry.Parent is null ? 0 : 1 + Depth(_entries.Single(item => item.Button == entry.Parent));

    private static void Activate(ButtonBase button)
    {
        if (!button.Enabled) return;
        button.Focus();
        if (button is RadioButton radio) radio.PerformClick();
        else button.AccessibilityObject.DoDefaultAction();
    }

    private void InvalidateMeasurements()
    {
        _measurements.Clear();
        _lineHeights.Clear();
        PerformLayout();
        MetricsChanged?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); InvalidateMeasurements(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); InvalidateMeasurements(); }

    private Size Preferred(Label label, int width)
    {
        var key = (label.Text, OptionsMeasureContext.FontOf(label), OptionsMeasureContext.Dpi(this), width);
        if (_measurements.TryGetValue(key, out var result)) return result;
        if (_measurements.Count >= 256) _measurements.Clear();
        return _measurements[key] = OptionsMeasureContext.Preferred(label, new Size(Math.Max(2, width), 0));
    }

    public int PreferredContentWidth => _entries.Select(entry =>
        Preferred(entry.Caption, int.MaxValue / 4).Width + OptionsMeasureContext.Preferred(entry.Button, Size.Empty).Width
            + UiDesignTokens.Pixels(UiDesignTokens.Current.OptionGap, OptionsMeasureContext.Dpi(this))).DefaultIfEmpty(0).Max()+LeadingInset;

    public int MinimumContentWidth => _entries.Select(entry =>
    {
        int Word(Label label) => label.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => TextRenderer.MeasureText(word, OptionsMeasureContext.FontOf(label)).Width).DefaultIfEmpty(0).Max() + label.Padding.Horizontal;
        var indent = UiDesignTokens.Current.Space(UiDesignTokens.Current.DependentIndent, OptionsMeasureContext.FontOf(this), OptionsMeasureContext.Dpi(this));
        var body = Math.Max(Word(entry.Caption), entry.Description is null ? 0 : Word(entry.Description));
        return Math.Max(body + OptionsMeasureContext.Preferred(entry.Button, Size.Empty).Width + UiDesignTokens.Pixels(6, OptionsMeasureContext.Dpi(this)),
            entry.Body is null ? 0 : entry.Body.MinimumContentWidth + indent) + Depth(entry) * indent;
    }).DefaultIfEmpty(0).Max()+LeadingInset;

    public int MeasureHeight(int width) => LayoutEntries(width, false);
    private int LayoutEntries(int width, bool apply)
    {
        var top = 0;
        var tokens = UiDesignTokens.Current;
        var gap = UiDesignTokens.Pixels(tokens.OptionGap, OptionsMeasureContext.Dpi(this));
        var indent = tokens.Space(tokens.DependentIndent, OptionsMeasureContext.FontOf(this), OptionsMeasureContext.Dpi(this));
        var inset = UiDesignTokens.Pixels(2, OptionsMeasureContext.Dpi(this));
        var captionColumn = _entries.Where(entry => entry.Description is not null)
            .Select(entry => Preferred(entry.Caption, int.MaxValue / 4).Width).DefaultIfEmpty(0).Max();
        foreach (var entry in _entries)
        {
            var left = LeadingInset + Depth(entry) * indent;
            var native = OptionsMeasureContext.Preferred(entry.Button, Size.Empty);
            if (!_lineHeights.TryGetValue(OptionsMeasureContext.FontOf(entry.Caption), out var lineHeight))
                _lineHeights[OptionsMeasureContext.FontOf(entry.Caption)] = lineHeight = TextRenderer.MeasureText("Ągjyp", OptionsMeasureContext.FontOf(entry.Caption)).Height;
            var firstHeight = Math.Max(native.Height, lineHeight);
            var glyphWidth = native.Width;
            var available = Math.Max(2, width - left - glyphWidth - gap);
            var descriptionWidth = entry.Description is null ? 0 : Preferred(entry.Description, int.MaxValue / 4).Width;
            var sideBySide = entry.Description is not null && captionColumn + gap + descriptionWidth <= available;
            var labelWidth = sideBySide ? captionColumn : available;
            var text = Preferred(entry.Caption, labelWidth);
            var captionTop = inset + (firstHeight - lineHeight) / 2 - entry.Caption.Padding.Top;
            var rowHeight = Math.Max(inset + firstHeight, captionTop + text.Height);
            if (apply)
            {
                entry.Button.Bounds = new Rectangle(left, top + inset, glyphWidth, firstHeight);
                entry.Caption.Bounds = new Rectangle(left + glyphWidth + gap, top + captionTop, labelWidth, text.Height);
            }
            if (entry.Description is { } detail)
            {
                var detailWidth = sideBySide ? available - captionColumn - gap : available;
                var detailSize = Preferred(detail, detailWidth);
                var detailTop = sideBySide ? captionTop : rowHeight + gap;
                if (apply) detail.Bounds = new Rectangle(left + glyphWidth + gap + (sideBySide ? captionColumn + gap : 0),
                    top + detailTop, detailWidth, detailSize.Height);
                rowHeight = Math.Max(rowHeight, detailTop + detailSize.Height);
            }
            top += rowHeight + inset + gap;
            if (entry.Body is { } body)
            {
                var bodyWidth = Math.Max(1, width - left - indent);
                var height = apply ? body.MeasureForWidth(bodyWidth) : body.MeasureHeightForWidth(bodyWidth);
                if (apply) body.View.Bounds = new Rectangle(left + indent, top, bodyWidth, height);
                top += height + tokens.Space(tokens.IndependentGroupGap, OptionsMeasureContext.FontOf(this), OptionsMeasureContext.Dpi(this));
            }
        }
        return Math.Max(1, top - (_entries.Count == 0 ? 0 : gap));
    }
    protected override void OnLayout(LayoutEventArgs e)
    {

        base.OnLayout(e);
        if (_arranging) return;
        _arranging = true;
        try { LayoutEntries(ClientSize.Width, true); }
        finally { _arranging = false; }
    }

    private sealed class OptionCaption(ButtonBase button) : Label
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // A textless native glyph cannot show the caption's focus cue by itself.
            if (button.Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1));
        }
        protected override bool ProcessMnemonic(char charCode)
        {
            if (Enabled && UseMnemonic && IsMnemonic(charCode, Text)) { Activate(button); return true; }
            return base.ProcessMnemonic(charCode);
        }
    }
}
