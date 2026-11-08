using System.ComponentModel;

namespace SubEdit.Player.UI.DesignSystem;

public enum FieldWidthProfile { Compact, Preferred, Fill, Content }

/// <summary>A width-constrained stack of native sections. Heights come from text and controls.</summary>
public sealed class SettingsPageLayout : UserControl, IDialogContent
{
    private readonly TableLayoutPanel _stack = new();
    private readonly List<Control> _sections = [];
    private bool _measuring;
    public event EventHandler? MetricsChanged;

    private void InvalidateMinimum(object? sender, EventArgs e)
    {
        MetricsChanged?.Invoke(this, EventArgs.Empty);
    }

    [Browsable(false)]
    public int MinimumContentWidth => _sections.Select(section => section switch
    {
        SettingsSection group => group.MeasureMinimumWidth(),
        MessageSection message => message.MeasureMinimumWidth(),
        _ => section.MinimumSize.Width
    }).DefaultIfEmpty(0).Max();

    public SettingsPageLayout()
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        Margin = Padding.Empty;
        _stack.Dock = DockStyle.Fill;
        _stack.ColumnCount = 1;
        _stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _stack.Margin = Padding.Empty;
        Controls.Add(_stack);
        FontChanged += InvalidateMinimum;
        DpiChangedAfterParent += InvalidateMinimum;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public UiDesignTokens Tokens { get; set; } = UiDesignTokens.Current;
    [Browsable(false)]
    public Control View => this;

    public int MeasureUnwrappedMessageWidth() => _sections.OfType<MessageSection>()
        .Select(section => section.MeasureUnwrappedWidth()).DefaultIfEmpty(0).Max();

    public int MeasureSmallFormWidth() => _sections.OfType<SettingsSection>()
        .Select(section => section.PreferredFormWidth()).DefaultIfEmpty(0).Max();

    public SettingsSection AddSection(string title, UiVisualProfile profile = UiVisualProfile.Standard)
    {
        var section = new SettingsSection(title, profile, Tokens);
        section.MetricsChanged += InvalidateMinimum;
        Add(section);
        return section;
    }

    public void AddMessage(string message, Image? icon = null) => Add(new MessageSection(message, icon, Tokens));

    private void Add(Control section)
    {
        section.Dock = DockStyle.Fill;
        section.TabIndex = _sections.Count;
        _stack.RowCount++;
        _stack.RowStyles.Add(new RowStyle(SizeType.Absolute));
        _stack.Controls.Add(section, 0, _sections.Count);
        _sections.Add(section);
        InvalidateMinimum(this, EventArgs.Empty);
    }

    public int MeasureForWidth(int width) => MeasurePage(width, true);
    public int MeasureHeightForWidth(int width) => MeasurePage(width, false);

    private int MeasurePage(int width, bool arrange)
    {
        if (_measuring) return Height;

        _measuring = true;
        if (arrange) _stack.SuspendLayout();
        try
        {
            width = Math.Max(1, width);
            if (arrange) Width = width;
            var gap = UiDesignTokens.Pixels(Tokens.SectionGap, OptionsMeasureContext.Dpi(this));
            var height = 0;
            for (var index = 0; index < _sections.Count; index++)
            {
                var section = _sections[index];
                var measured = section switch
                {
                    SettingsSection group => group.MeasureForWidth(width, arrange),
                    MessageSection message => message.MeasureForWidth(width, arrange),
                    _ => OptionsMeasureContext.Preferred(section, new Size(width, 0)).Height
                };
                var after = index == _sections.Count - 1 ? 0 : gap;
                if (arrange)
                {
                    section.Margin = new Padding(0, 0, 0, after);
                    _stack.RowStyles[index].Height = measured + after;
                }
                height += measured + after;
            }
            if (arrange) Height = Math.Max(1, height);
            return Math.Max(1, height);
        }
        finally
        {
            if (arrange) _stack.ResumeLayout(true);
            _measuring = false;
        }
    }

    public sealed class SettingsSection : GroupBox, IDialogContent
    {
        private readonly TableLayoutPanel _grid = new();
        private readonly UiDesignTokens _tokens;
        private sealed record FieldWidth(int LogicalWidth, FieldWidthProfile Profile, string? Sample);
        private readonly Dictionary<Control, FieldWidth> _fieldWidths = [];
        private readonly List<(Control First, Control? Second)> _rows = [];
        private readonly Dictionary<(string Text, Font Font, int Dpi, Size Size, TextFormatFlags Flags), Size> _textCache = [];
        private bool[]? _stackedRows;
        private readonly Dictionary<Control, Size> _knownMinimumSizes = [];
        private NativeOptionList? _options;
        public event EventHandler? MetricsChanged;
        public Control View => this;
        public int MinimumContentWidth => MeasureMinimumWidth();
        int IDialogContent.MeasureForWidth(int width) => MeasureForWidth(width, true);
        public int MeasureHeightForWidth(int width) => MeasureForWidth(width, false);
        private void InvalidateMinimum(object? sender, EventArgs e) => MetricsChanged?.Invoke(this, EventArgs.Empty);

        internal int MeasureMinimumWidth()
        {

            var caption = Measure(Text, OptionsMeasureContext.FontOf(this), Size.Empty, TextFormatFlags.SingleLine).Width
                + UiDesignTokens.Pixels(_tokens.GroupCaptionHorizontalInset, OptionsMeasureContext.Dpi(this));
            var body = 0;
            foreach (var (first, second) in _rows)
            {
                var row = MinimumControlWidth(first);
                if (second is not null)
                {
                    var other = MinimumControlWidth(second);
                    if (_rows.Any(r => r.Second is AdaptiveSettingsRow { KeepTogether: true }))
                        row = CaptionWidth(first) + UiDesignTokens.Pixels(_tokens.FieldGap, OptionsMeasureContext.Dpi(this))
                            + (_fieldWidths.TryGetValue(second, out var alignedSpec) ? PreferredFieldWidth(second, alignedSpec, OptionsMeasureContext.Dpi(this)) : other);
                    else if (_fieldWidths.ContainsKey(second)) row = Math.Max(row, other); // A field can stack.
                    else
                    {
                        // Existing radio/description pairs retain two columns (45% / remainder).
                        var leading = Measure(first.Text, OptionsMeasureContext.FontOf(first)).Width
                            + UiDesignTokens.Pixels((first is RadioButton ? 22 : 0) + _tokens.FieldGap, OptionsMeasureContext.Dpi(this));
                        row = Math.Max((int)Math.Ceiling(leading / 0.45), leading + other);
                    }
                }
                body = Math.Max(body, row);
            }
            return Math.Max(caption, body + UiDesignTokens.Pixels(24, OptionsMeasureContext.Dpi(this)));
        }

        private int MinimumControlWidth(Control control)
        {
            var width = control.MinimumSize.Width;
            if(control is NumericUpDown numeric)return NativeControlLayout.NumericWidth(numeric);
            if (control is SettingsComposition composition) return Math.Max(width, composition.MinimumContentWidth);
            if (control is AdaptiveSettingsRow adaptive) return Math.Max(width, adaptive.MinimumContentWidth);
            if (control is NativeOptionList options) return Math.Max(width, options.MinimumContentWidth);
            if (_fieldWidths.TryGetValue(control, out var spec))
            {
                if (spec.Profile == FieldWidthProfile.Compact) return Math.Max(width, PreferredFieldWidth(control, spec, OptionsMeasureContext.Dpi(this)));
                if (control is not ButtonBase) return Math.Max(width,
                    Measure(spec.Sample ?? new string('M', _tokens.MinimumEditableFieldEm), OptionsMeasureContext.FontOf(control)).Width
                        + UiDesignTokens.Pixels(control is ComboBox ? 24 : _tokens.FieldChromeWidth, OptionsMeasureContext.Dpi(this)));
            }
            if (control is Button)
                return Math.Max(width, Measure(control.Text, OptionsMeasureContext.FontOf(control), Size.Empty, TextFormatFlags.SingleLine).Width
                    + control.Padding.Horizontal + UiDesignTokens.Pixels(24, OptionsMeasureContext.Dpi(this)));
            var word = control.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(text => Measure(text, OptionsMeasureContext.FontOf(control)).Width).DefaultIfEmpty(0).Max();
            return Math.Max(width, word + UiDesignTokens.Pixels(control is CheckBox or RadioButton ? 22 : 0, OptionsMeasureContext.Dpi(this)));
        }

        [Browsable(false)]
        public int StackedFieldRows { get; private set; }

        private bool IsCompactForm => _rows.Count == 1 && _rows[0].Second is { } input
            && _fieldWidths.TryGetValue(input, out var spec) && spec.Profile == FieldWidthProfile.Compact;

        public int PreferredFormWidth()
        {
            if (_rows.Count != 1 || _rows[0].Second is not { } field || !_fieldWidths.TryGetValue(field, out var spec))
                return Width;
            return Math.Max(UiDesignTokens.Pixels(240, OptionsMeasureContext.Dpi(this)), Math.Min(Measure(_rows[0].First.Text, OptionsMeasureContext.FontOf(_rows[0].First)).Width,
                Measure(new string('M', _tokens.FormLabelPreferredEm), OptionsMeasureContext.FontOf(this)).Width)
                + PreferredFieldWidth(field, spec, OptionsMeasureContext.Dpi(this)) + UiDesignTokens.Pixels(36, OptionsMeasureContext.Dpi(this)));
        }

        private Size Measure(string text, Font font, Size proposed = default, TextFormatFlags flags = TextFormatFlags.Default)
        {
            var key = (text, font, OptionsMeasureContext.Dpi(this), proposed, flags);
            if (_textCache.TryGetValue(key, out var size)) return size;
            if (_textCache.Count >= 512) _textCache.Clear();
            return _textCache[key] = TextRenderer.MeasureText(text, font, proposed, flags);
        }

        internal SettingsSection(string title, UiVisualProfile profile, UiDesignTokens tokens)
        {
            _tokens = tokens;
            Text = title;
            ForeColor = profile == UiVisualProfile.Operation ? tokens.OperationCaption : tokens.Text;
            _grid.Dock = DockStyle.Fill;
            _grid.ColumnCount = 2;
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute));
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _grid.Margin = Padding.Empty;
            Controls.Add(_grid);
            FontChanged += (_, _) => { _textCache.Clear(); InvalidateMinimum(this, EventArgs.Empty); };
            DpiChangedAfterParent += (_, _) => { _textCache.Clear(); InvalidateMinimum(this, EventArgs.Empty); };
            TextChanged += InvalidateMinimum;
            _grid.Layout += (_, e) =>
            {
                // ResumeLayout may replay the last event. React only to an actual property change.
                if (e.AffectedProperty == nameof(MinimumSize) && e.AffectedControl is { } control
                    && _knownMinimumSizes.TryGetValue(control, out var previous) && previous != control.MinimumSize)
                {
                    _knownMinimumSizes[control] = control.MinimumSize;
                    InvalidateMinimum(this, EventArgs.Empty);
                }
            };
        }

        public Label AddField(string caption, Control field, int preferredWidth = 175,
            FieldWidthProfile widthProfile = FieldWidthProfile.Preferred, string? widthSample = null)
        {
            var label = NewLabel(caption);
            label.TextAlign = ContentAlignment.MiddleRight;
            field.AccessibleName ??= caption.Replace("&", "").TrimEnd(':');
            if (field is FieldValueLayout described)
            {
                described.Editor.AccessibleName ??= field.AccessibleName;
                described.Editor.AccessibleDescription ??= described.Description.Text;
            }
            label.Click += (_, _) => (field is FieldValueLayout value ? value.Editor : field).Focus();
            AddPair(label, field);
            _options = null;
            _fieldWidths.Add(field, new FieldWidth(preferredWidth, widthProfile, widthSample));
            InvalidateMinimum(this, EventArgs.Empty);
            if (field is ComboBox combo) NativeControlLayout.BindComboBox(combo);
            return label;
        }

        public void AddComposition(SettingsComposition composition)
        {
            composition.MetricsChanged += InvalidateMinimum;
            AddFullRow(composition); _options = null;
        }
        public AdaptiveSettingsRow AddAdaptiveRow(bool keepTogether = false)
        {
            var row = new AdaptiveSettingsRow { KeepTogether = keepTogether };
            row.MetricsChanged += InvalidateMinimum;
            AddFullRow(row); _options = null;
            return row;
        }

        public CheckBox AddCheckField(string caption, Control field, bool isChecked = false, string widthSample = "999", string? unit = null)
        {
            var options = new NativeOptionList { Margin = Padding.Empty };
            options.MetricsChanged += InvalidateMinimum;
            var check = options.Add(new CheckBox { Checked = isChecked, UseVisualStyleBackColor = true }, caption);
            field.AccessibleName ??= caption.Replace("&", "").TrimEnd(':');
            if (unit is not null)
            {
                field.AccessibleDescription = unit;
                field = new FieldValueLayout(field, unit, keepTogether: true);
            }
            AddPair(options, field);
            _fieldWidths.Add(field, new FieldWidth(0, FieldWidthProfile.Compact, widthSample));
            NativeOptionList.BindEnabled(field, check);
            _options = null; // Later options form a new block in reading order.
            InvalidateMinimum(this, EventArgs.Empty);
            return check;
        }

        public Label AddFieldWithUnit(string caption, Control field, string unit, string widthSample = "999") =>
            AddField(caption, new FieldValueLayout(field, unit), widthProfile: FieldWidthProfile.Compact, widthSample: widthSample);

        public CheckBox AddEditorAlignedCheckBox(FieldValueLayout column,string text,bool isChecked=false)
        {
            _options=null;
            var options=Options();options.AlignToEditorColumn(column);
            return options.Add(new CheckBox {Checked=isChecked,UseVisualStyleBackColor=true},text);
        }
        public CheckBox AddCheckBox(string text, bool isChecked = false, ButtonBase? dependsOn = null)
        {
            return Options().Add(new CheckBox { Checked = isChecked, UseVisualStyleBackColor = true }, text, dependsOn: dependsOn);
        }

        public RadioButton AddRadioOption(string text, string description = "", bool isChecked = false)
        {
            return Options().Add(new RadioButton { Checked = isChecked, Tag = text, UseVisualStyleBackColor = true },
                description.Length == 0 ? text : $"{text} {description}");
        }

        public RadioButton AddRadioChoice(string text, string description, bool isChecked = false) =>
            Options().Add(new RadioButton { Checked = isChecked, Tag = text, UseVisualStyleBackColor = true }, text, description);

        public SettingsSection AddDependentSettings(ButtonBase owner, string title)
        {
            var block = new SettingsSection(title, UiVisualProfile.Standard, _tokens);
            Options().AddDependentContent(owner, block);
            return block;
        }

        private NativeOptionList Options()
        {
            if (_options is not null) return _options;
            _options = new NativeOptionList { Margin = Padding.Empty };
            _options.MetricsChanged += InvalidateMinimum;
            AddFullRow(_options);
            return _options;
        }

        public void AddText(string text) { AddFullRow(NewLabel(text)); _options = null; }

        private static Label NewLabel(string text) => new() { Text = text, UseMnemonic = true };

        private void AddPair(Control first, Control second)
        {
            _rows.Add((first, second));
            var row = NewRow();
            Prepare(first, row * 2);
            Prepare(second, row * 2 + 1);
            _grid.Controls.Add(first, 0, row);
            _grid.Controls.Add(second, 1, row);
            InvalidateMinimum(this, EventArgs.Empty);
        }

        private void AddFullRow(Control control)
        {
            _rows.Add((control, null));
            var row = NewRow();
            Prepare(control, row * 2);
            _grid.Controls.Add(control, 0, row);
            _grid.SetColumnSpan(control, 2);
            InvalidateMinimum(this, EventArgs.Empty);
        }

        private int NewRow()
        {
            var row = _grid.RowCount++;
            _grid.RowStyles.Add(new RowStyle(SizeType.Absolute));
            return row;
        }

        private void Prepare(Control control, int tabIndex)
        {
            _knownMinimumSizes[control] = control.MinimumSize;
            control.ForeColor = _tokens.Text;
            control.TabIndex = tabIndex;
            control.Anchor = AnchorStyles.Left;
            if (control is Label or ButtonBase) control.AutoSize = false;
            if (control is Button button) NativeControlLayout.ConfigureButton(button);
            if (control is Label or ButtonBase) control.TextChanged += InvalidateMinimum;
            control.FontChanged += InvalidateMinimum;
        }

        internal int MeasureForWidth(int width, bool arrange = true)
        {
            if (arrange) { SuspendLayout(); _grid.SuspendLayout(); }
            try
            {
                var dpi = OptionsMeasureContext.Dpi(this);
                var gap = UiDesignTokens.Pixels(_tokens.FieldGap, dpi);
                var rowGap = UiDesignTokens.Pixels(_tokens.RowGap, dpi);
                var inset = UiDesignTokens.Pixels(_tokens.GroupHorizontalPadding, dpi);
                var padding = new Padding(inset, UiDesignTokens.Pixels(_tokens.GroupTopPadding, dpi), inset, UiDesignTokens.Pixels(_tokens.GroupBottomPadding, dpi));
                // Native caption/border insets are independent of requested width and managed Padding.
                var nativeTop = OptionsMeasureContext.GroupCaptionTop(this);
                var nativeBottom = Math.Max(0, ClientSize.Height - DisplayRectangle.Bottom - Padding.Bottom);
                var nativeHorizontal = Math.Max(0, ClientSize.Width - DisplayRectangle.Width - Padding.Horizontal);
                var innerWidth = Math.Max(1, width - padding.Horizontal - nativeHorizontal);
                var stacked = ChooseStackedRows(innerWidth, gap, dpi);
                var desiredCaption = _rows.Where((pair, index) => pair.Second is not null && !stacked[index])
                    .Select(pair => CaptionWidth(pair.First)
                        + (pair.First is RadioButton ? UiDesignTokens.Pixels(22, dpi) : 0) + gap).DefaultIfEmpty(0).Max();
                var firstWidth = Math.Min(desiredCaption, (int)(innerWidth * 0.45));
                if (IsCompactForm && _rows[0].Second is { } field && _fieldWidths.TryGetValue(field, out var spec))
                    firstWidth = Math.Min(Measure(_rows[0].First.Text, OptionsMeasureContext.FontOf(_rows[0].First)).Width + gap,
                        Math.Max(0, innerWidth - PreferredFieldWidth(field, spec, dpi)));
                var compactFields = _rows.Where((pair, index) => pair.Second is not null && !stacked[index]
                    && _fieldWidths.TryGetValue(pair.Second, out var widthSpec) && widthSpec.Profile == FieldWidthProfile.Compact).ToArray();
                if (compactFields.Length > 1)
                    firstWidth = Math.Min(desiredCaption, Math.Max(0, innerWidth - compactFields.Max(pair =>
                        PreferredFieldWidth(pair.Second!, _fieldWidths[pair.Second!], dpi))));
                if (arrange)
                {
                    Width = width;
                    Padding = padding;
                    ReflowRows(stacked);
                    _grid.ColumnStyles[0].Width = firstWidth;
                }
                var total = 0;
                var row = 0;
                for (var index = 0; index < _rows.Count; index++)
                {
                    var (first, second) = _rows[index];
                    var firstHeight = Fit(first, second is null || stacked[index] ? innerWidth : firstWidth,
                        second is not null && !stacked[index]);
                    if (stacked[index]) CommitRow(firstHeight);
                    var secondHeight = second is null ? 0 : Fit(second, stacked[index] ? innerWidth : innerWidth - firstWidth, false);
                    CommitRow(stacked[index] ? secondHeight : Math.Max(firstHeight, secondHeight));
                }
                var measuredHeight = padding.Vertical + nativeTop + nativeBottom + total;
                if (arrange) Height = measuredHeight;
                return measuredHeight;

                int Fit(Control control, int cellWidth, bool caption)
                {
                    var margin = new Padding(0, rowGap, caption ? gap : 0, rowGap);
                    var available = Math.Max(1, cellWidth - margin.Horizontal);
                    Size size;
                    if (control is SettingsComposition composition) size = new Size(available, composition.MeasureHeightForWidth(available));
                    else if (control is AdaptiveSettingsRow adaptive) size = new Size(available, adaptive.Measure(available, false));
                    else if (control is NativeOptionList options) size = new Size(available, options.MeasureHeight(available));
                    else if (_fieldWidths.TryGetValue(control, out var fieldWidth))
                    {
                        var fieldWidthPixels = control is not NumericUpDown && fieldWidth.Profile == FieldWidthProfile.Fill ? available
                            : Math.Min(available, PreferredFieldWidth(control, fieldWidth, dpi));
                        size = new Size(fieldWidthPixels, control is FieldValueLayout value ? value.MeasureHeight(fieldWidthPixels) : OptionsMeasureContext.Preferred(control, Size.Empty).Height);
                    }
                    else
                    {
                        var glyph = control is CheckBox or RadioButton ? UiDesignTokens.Pixels(22, dpi) : 0;
                        var text = Measure(control.Text, OptionsMeasureContext.FontOf(control), new Size(Math.Max(1, available - glyph), int.MaxValue), TextFormatFlags.WordBreak);
                        size = new Size(available, Math.Max(text.Height + rowGap, OptionsMeasureContext.FontOf(this).Height + rowGap));
                    }
                    if (arrange)
                    {
                        control.Margin = margin;
                        if (control.Size != size) {  control.Size = size; }
                    }
                    return size.Height + margin.Vertical;
                }
                void CommitRow(int height)
                {
                    if (arrange && _grid.RowStyles[row].Height != height)
                    {

                        _grid.RowStyles[row].Height = height;
                    }
                    row++;
                    total += height;
                }
            }
            finally
            {
                if (arrange) { ResumeLayout(true); _grid.ResumeLayout(true);  }
            }
        }

        private int CaptionWidth(Control caption) => caption is NativeOptionList options ? options.PreferredContentWidth : Measure(caption.Text, OptionsMeasureContext.FontOf(caption)).Width;

        private bool[] ChooseStackedRows(int width, int gap, int dpi) => _rows.Select(pair =>
        {
            if (pair.Second is not { } field || !_fieldWidths.TryGetValue(field, out var spec)) return false;
            var natural = CaptionWidth(pair.First);
            var longestWord = pair.First.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => Measure(word, OptionsMeasureContext.FontOf(pair.First)).Width).DefaultIfEmpty(0).Max();
            var captionMinimum = IsCompactForm ? Math.Min(natural,
                Math.Max(longestWord, Measure(new string('M', _tokens.FormLabelMinimumEm), OptionsMeasureContext.FontOf(pair.First)).Width)) : natural;
            return captionMinimum + gap + PreferredFieldWidth(field, spec, dpi) > width;
        }).ToArray();

        private void ReflowRows(bool[] stackedRows)
        {
            StackedFieldRows = stackedRows.Count(stacked => stacked);
            if (_stackedRows is not null && _stackedRows.SequenceEqual(stackedRows)) return;

            _stackedRows = stackedRows;
            var requiredRows = _rows.Count + StackedFieldRows;
            while (_grid.RowStyles.Count < requiredRows) { _grid.RowStyles.Add(new RowStyle(SizeType.Absolute));  }
            while (_grid.RowStyles.Count > requiredRows) { _grid.RowStyles.RemoveAt(_grid.RowStyles.Count - 1);  }
            var row = 0;
            var index = 0;
            foreach (var (first, second) in _rows)
            {
                var field = second is not null && _fieldWidths.TryGetValue(second, out _);
                var stacked = stackedRows[index++];
                if (first is Label label && field)
                    label.TextAlign = stacked || IsCompactForm ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight;
                _grid.SetCellPosition(first, new TableLayoutPanelCellPosition(0, row));
                _grid.SetColumnSpan(first, second is null || stacked ? 2 : 1);
                if (stacked)
                {
                    row++;
                }
                if (second is not null)
                {
                    _grid.SetCellPosition(second, new TableLayoutPanelCellPosition(stacked ? 0 : 1, row));
                    _grid.SetColumnSpan(second, stacked ? 2 : 1);
                }
                row++;
            }
            _grid.RowCount = row;
        }

        private int PreferredFieldWidth(Control control, FieldWidth spec, int dpi)
        {
            if (control is AdaptiveSettingsRow adaptive) return adaptive.PreferredContentWidth;
            if(control is NumericUpDown numeric)return NativeControlLayout.NumericWidth(numeric);
            if (control is FieldValueLayout value)
                return PreferredFieldWidth(value.Editor, spec, dpi) + value.DescriptionWidth;
            if (spec.Profile == FieldWidthProfile.Content && control is ComboBox combo)
                return Math.Max(control.MinimumSize.Width, combo.Items.Cast<object>().Select(item => Measure(combo.GetItemText(item) ?? string.Empty, OptionsMeasureContext.FontOf(combo)).Width)
                    .DefaultIfEmpty(0).Max() + UiDesignTokens.Pixels(24, dpi));
            if (spec.Profile == FieldWidthProfile.Compact)
                return Math.Max(control.MinimumSize.Width, Math.Max(UiDesignTokens.Pixels(_tokens.CompactFieldMinWidth, dpi),
                    Measure(spec.Sample ?? "99999", OptionsMeasureContext.FontOf(control)).Width
                    + UiDesignTokens.Pixels(_tokens.FieldChromeWidth, dpi)));
            return Math.Max(control.MinimumSize.Width, (int)Math.Ceiling(UiDesignTokens.Pixels(spec.LogicalWidth, dpi)
                * OptionsMeasureContext.FontOf(control).SizeInPoints / _tokens.DialogFont.SizeInPoints));
        }
    }

    private sealed class MessageSection : Panel
    {
        private readonly TableLayoutPanel _layout = new();
        private readonly Label _message = new();
        private readonly PictureBox? _icon;
        private readonly UiDesignTokens _tokens;

        public int MeasureMinimumWidth() => _message.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => TextRenderer.MeasureText(word, OptionsMeasureContext.FontOf(this)).Width).DefaultIfEmpty(0).Max()
            + (_icon is null ? 0 : UiDesignTokens.Pixels(32 + _tokens.BodyInset, OptionsMeasureContext.Dpi(this)));

        public MessageSection(string text, Image? icon, UiDesignTokens tokens)
        {
            _tokens = tokens;
            _layout.Dock = DockStyle.Fill;
            _layout.Margin = Padding.Empty;
            _layout.ColumnCount = 2;
            _layout.RowCount = 1;
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            if (icon is not null)
            {
                _icon = new PictureBox { Image = icon, SizeMode = PictureBoxSizeMode.CenterImage, Dock = DockStyle.Top };
                _layout.Controls.Add(_icon, 0, 0);
            }
            _message.Text = text;
            _message.UseMnemonic = false;
            _message.Dock = DockStyle.Fill;
            _message.Margin = Padding.Empty;
            _layout.Controls.Add(_message, 1, 0);
            Controls.Add(_layout);
        }

        public int MeasureForWidth(int width, bool arrange)
        {
            var iconSize = _icon is null ? 0 : UiDesignTokens.Pixels(32, OptionsMeasureContext.Dpi(this));
            var gap = _icon is null ? 0 : UiDesignTokens.Pixels(_tokens.BodyInset, OptionsMeasureContext.Dpi(this));
            if (arrange)
            {
                _layout.ColumnStyles[0].Width = iconSize + gap;
                if (_icon is not null) { _icon.Height = iconSize; _icon.Margin = new Padding(0, 0, gap, 0); }
            }
            var text = TextRenderer.MeasureText(_message.Text, OptionsMeasureContext.FontOf(this),
                new Size(Math.Max(1, width - iconSize - gap), int.MaxValue), TextFormatFlags.WordBreak);
            var measuredHeight = Math.Max(iconSize, text.Height + UiDesignTokens.Pixels(_tokens.MultilineTextBreathing, OptionsMeasureContext.Dpi(this)));
            if (arrange) Height = measuredHeight;
            return measuredHeight;
        }

        public int MeasureUnwrappedWidth()
        {
            var paragraphs = _message.Text.Replace("\r", "").Split('\n');
            var segments = paragraphs.SelectMany(paragraph =>
                System.Text.RegularExpressions.Regex.Split(paragraph, @"(?<=[.!?])\s+(?=\p{Lu})"));
            var textWidth = segments
                .Max(line => TextRenderer.MeasureText(line, OptionsMeasureContext.FontOf(this), Size.Empty, TextFormatFlags.SingleLine).Width);
            var lineBudget = TextRenderer.MeasureText(new string('M', _tokens.MessageLineEm), OptionsMeasureContext.FontOf(this)).Width;
            textWidth = Math.Min(textWidth, lineBudget);
            return textWidth + (_icon is null ? 0 : UiDesignTokens.Pixels(32 + _tokens.BodyInset, OptionsMeasureContext.Dpi(this)));
        }
    }
}
