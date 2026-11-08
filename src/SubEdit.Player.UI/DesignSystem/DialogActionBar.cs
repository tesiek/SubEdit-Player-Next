using System.ComponentModel;

namespace SubEdit.Player.UI.DesignSystem;

public enum DialogActionRole { Ok, Cancel, Apply, Yes, No, Secondary }
public enum DialogActionProfile { CenteredOkCancel, Options, Confirmation, Vertical }
public sealed record DialogAction(DialogActionRole Role, string Caption, DialogResult Result = DialogResult.None);

/// <summary>Only presentation and explicit button roles; no persistence or validation policy.</summary>
public sealed class DialogActionBar : UserControl
{
    private readonly TableLayoutPanel _alignment = new();
    private readonly FlowLayoutPanel _flow = new();
    private readonly Dictionary<DialogActionRole, Button> _buttons = [];
    private bool _measuring;
    private DialogActionProfile _profile;

    public DialogActionBar()
    {
        AutoScaleMode = AutoScaleMode.Inherit;
        Margin = Padding.Empty;
        _alignment.Dock = DockStyle.Fill;
        _alignment.Margin = Padding.Empty;
        _alignment.ColumnCount = 1;
        _alignment.RowCount = 1;
        _alignment.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _alignment.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _flow.Anchor = AnchorStyles.Top;
        _flow.Margin = Padding.Empty;
        _flow.Padding = Padding.Empty;
        _alignment.Controls.Add(_flow, 0, 0);
        Controls.Add(_alignment);
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public UiDesignTokens Tokens { get; set; } = UiDesignTokens.Current;
    [Browsable(false)]
    public IReadOnlyDictionary<DialogActionRole, Button> Buttons => _buttons;

    public Button Button(DialogActionRole role) => _buttons[role];
    // All shells need the renderer's vertical insets, including UI #2.
    [DefaultValue(true)] public bool UseNativeButtonMetrics { get; set; } = true;
    [DefaultValue(false)] public bool CompactContentButtons { get; set; }

    public void Configure(DialogActionProfile profile, params DialogAction[] actions)
    {
        if (actions.Select(action => action.Role).Distinct().Count() != actions.Length)
            throw new ArgumentException("Action roles must be unique.", nameof(actions));
        foreach (var button in _buttons.Values) button.Dispose();
        _buttons.Clear();
        _flow.Controls.Clear();
        _profile = profile;
        _flow.FlowDirection = profile == DialogActionProfile.Vertical
            ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        // Actions are supplied in visual order, not inferred from their captions.
        for (var index = 0; index < actions.Length; index++)
        {
            var action = actions[index];
            var button = new Button
            {
                Text = action.Caption, DialogResult = action.Result,
                UseVisualStyleBackColor = true, TabIndex = index
            };
            _buttons.Add(action.Role, button);
            NativeControlLayout.ConfigureButton(button);
            _flow.Controls.Add(button);
        }
        _flow.Anchor = profile == DialogActionProfile.Options
            ? AnchorStyles.Top | AnchorStyles.Right : AnchorStyles.Top;
        MeasureForWidth(Math.Max(1, Width));
    }

    public int MeasureForWidth(int availableWidth)
    {
        if (_measuring) return Height;
        _measuring = true;
        try
        {
            if (CompactContentButtons) return MeasureCompact(availableWidth);
            var dpi = DeviceDpi;
            var gap = UiDesignTokens.Pixels(Tokens.ActionGap, dpi);
            var minHeight = UiDesignTokens.Pixels(
                _profile == DialogActionProfile.Options ? Tokens.OptionsActionHeight : Tokens.ActionHeight, dpi);
            var width = Math.Max(1, availableWidth);
            var buttonWidth = _buttons.Count == 0 ? 0 : Math.Min(width, Math.Max(
                UiDesignTokens.Pixels(Tokens.ActionWidth, dpi),
                _buttons.Values.Max(button => button.GetPreferredSize(Size.Empty).Width)));
            var height = minHeight;
            foreach (var button in _buttons.Values)
            {
                var text = TextRenderer.MeasureText(button.Text, Font,
                    new Size(Math.Max(1, buttonWidth - gap * 2), int.MaxValue),
                    TextFormatFlags.WordBreak);
                height = Math.Max(height, text.Height + gap);
            }
            if (UseNativeButtonMetrics)
                height = Math.Max(height, _buttons.Values.Select(button => NativeControlLayout.MeasureButtonHeight(button, buttonWidth)).DefaultIfEmpty(0).Max());
            _flow.SuspendLayout();
            foreach (var button in _buttons.Values)
            {
                button.Size = new Size(buttonWidth, height);
                button.Margin = _profile == DialogActionProfile.Vertical
                    ? new Padding(0, 0, 0, gap) : new Padding(0, 0, gap, gap);
            }
            if (_buttons.Count > 0)
                _buttons.Values.Last().Margin = Padding.Empty;
            _flow.MaximumSize = new Size(width, 0);
            _flow.WrapContents = _profile != DialogActionProfile.Vertical;
            _flow.ResumeLayout(true);
            var count = _buttons.Count;
            var vertical = _profile == DialogActionProfile.Vertical;
            var perRow = vertical ? 1 : Math.Max(1, (width + gap) / Math.Max(1, buttonWidth + gap));
            var rows = Math.Max(1, (count + perRow - 1) / perRow);
            var measuredHeight = rows * height + (rows - 1) * gap;
            var flowWidth = vertical ? buttonWidth : Math.Min(width, count * buttonWidth + Math.Max(0, count - 1) * gap);
            _flow.Size = new Size(flowWidth, measuredHeight);
            _flow.PerformLayout();
            Height = measuredHeight;
            _alignment.PerformLayout();
            return measuredHeight;
        }
        finally { _measuring = false; }
    }
    private int MeasureCompact(int availableWidth)
    {
        var gap = UiDesignTokens.Pixels(Tokens.ActionGap, DeviceDpi);
        var height = UiDesignTokens.Pixels(Tokens.ActionHeight, DeviceDpi);
        foreach (var button in _buttons.Values)
            height = Math.Max(height, TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine).Height
                + UiDesignTokens.Pixels(Tokens.ButtonVerticalInsets, DeviceDpi));
        _flow.SuspendLayout();
        foreach (var button in _buttons.Values)
        {
            var text = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine);
            button.Size = new Size(Math.Max(UiDesignTokens.Pixels(Tokens.ActionWidth, DeviceDpi), text.Width + 2 * gap), height);
            button.Margin = new Padding(0, 0, gap, gap);
        }
        if (_buttons.Count > 0) _buttons.Values.Last().Margin = Padding.Empty;
        _flow.MaximumSize = new Size(Math.Max(1, availableWidth), 0);
        _flow.WrapContents = true;
        _flow.ResumeLayout(false);
        var rowWidth = 0;
        var flowWidth = 0;
        var rows = 1;
        foreach (var button in _buttons.Values)
        {
            var occupied = button.Width + button.Margin.Horizontal;
            if (rowWidth > 0 && rowWidth + occupied > availableWidth)
            {
                flowWidth = Math.Max(flowWidth, rowWidth);
                rowWidth = 0;
                rows++;
            }
            rowWidth += occupied;
        }
        var measuredHeight = rows * (height + gap) - gap;
        _flow.Size = new Size(Math.Max(flowWidth, rowWidth), measuredHeight);
        _flow.PerformLayout();
        Height = measuredHeight;
        _alignment.PerformLayout();
        return measuredHeight;
    }
}
