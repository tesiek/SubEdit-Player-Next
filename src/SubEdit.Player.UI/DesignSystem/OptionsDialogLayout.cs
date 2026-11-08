using System.ComponentModel;
namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Registered pages share one measured window; only page content may scroll.</summary>
public sealed class OptionsDialogLayout : UserControl
{
    public TabControl Tabs { get; } = new OptionsTabControl() { Multiline = true, TabIndex = 0 };
    public DialogActionBar Actions { get; } = new() { TabIndex = 1 };
    public List<(SettingsPageLayout Page, VerticalContentViewport Viewport)> Pages { get; } = [];
    private bool _arranging;
    internal bool ArrangementDeferred;
    private bool _updatingMetrics;
    public OptionsDialogLayout()
    {
        Dock = DockStyle.Fill;
        AutoScaleMode = AutoScaleMode.Inherit;
        Controls.Add(Tabs); Controls.Add(Actions);
        Tabs.SelectedIndexChanged += (_, _) => ArrangeSelectedPage();
        SizeChanged += (_, _) => ArrangeViewport();
    }
    [Browsable(false)] public UiDesignTokens Tokens => UiDesignTokens.Current;
    public void Register(string title, SettingsPageLayout page)
    {
        var tab = new TabPage(title) { BackColor = Tokens.Background, Padding = Padding.Empty };
        var viewport = new VerticalContentViewport(arrangeFullWidthFirst: true) { TabIndex = 0 };
        viewport.SetContent(page);
        tab.Controls.Add(viewport);
        Tabs.TabPages.Add(tab);
        Pages.Add((page, viewport));
    }
    private int Inset => Tokens.Inset(UiVisualProfile.Options, DeviceDpi);
    private Size Chrome => Tabs.Size - Tabs.DisplayRectangle.Size;
    public int MinimumClientWidth => Pages.Select(p => p.Page.MinimumContentWidth).DefaultIfEmpty(0).Max()
        + Chrome.Width + 4 * Inset + SystemInformation.VerticalScrollBarWidth + UiDesignTokens.Pixels(Tokens.ScrollbarGap, DeviceDpi);
    internal int MeasurePreferredClientWidth(int minimumClientWidth) =>
        Math.Max(CompositionWidth + 4 * Inset + Chrome.Width + UiDesignTokens.Pixels(30, DeviceDpi), Math.Max(minimumClientWidth,
        Math.Max(UiDesignTokens.Pixels(650, DeviceDpi),
            Math.Max(Pages.SelectMany(p => Descendants(p.Page)).OfType<Label>().Select(l => TextRenderer.MeasureText(l.Text, OptionsMeasureContext.FontOf(l)).Width).DefaultIfEmpty(0).Max(),
                Pages.SelectMany(p => Descendants(p.Page)).OfType<AdaptiveSettingsRow>().Select(row => row.PreferredContentWidth).DefaultIfEmpty(0).Max()) + 4 * Inset + Chrome.Width + UiDesignTokens.Pixels(30, DeviceDpi))));
    private int CompositionWidth=>Pages.SelectMany(p=>Descendants(p.Page)).OfType<SettingsComposition>().Select(c=>c.PreferredContentWidth).DefaultIfEmpty(0).Max();
    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    public int MinimumClientHeight => Actions.Height + Chrome.Height + 4 * Inset + Font.Height * 2;
    /// <summary>Coalesce native layout transactions while a host-wide font is propagated.</summary>
    public void UpdateMetrics(Action update)
    {
        var root = (Control?)FindForm() ?? this;
        var controls = new[] { root }.Concat(Descendants(root)).ToArray();
        _updatingMetrics = true;
        foreach (var control in controls) control.SuspendLayout();
        try { update(); }
        finally
        {
            // The measured Fit owns arrangement. Resume autoscaling without repeating every
            // intermediate TableLayoutPanel layout for each changed child font.
            foreach (var control in controls.Reverse()) control.ResumeLayout(false);
            _updatingMetrics = false;
        }
    }
    public int MeasurePreferredHeight(int clientWidth)
    {
        // Native tab header metrics must be measured at the proposed width, before page sizing.
        Tabs.Width = Math.Max(1, clientWidth - 2 * Inset);
        Tabs.PerformLayout();
        var bodyWidth = Math.Max(1, Tabs.DisplayRectangle.Width - 2 * Inset);
        var maximumHeight=0;
        for(var i=0;i<Pages.Count;i++)
        {
            var height = Pages[i].Page.MeasureHeightForWidth(bodyWidth);
            maximumHeight=Math.Max(maximumHeight,height);
        }
        return maximumHeight + Chrome.Height + 4 * Inset + UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi)
            + Actions.MeasureForWidth(clientWidth - 2 * Inset);
    }
    internal void ArrangeSelectedPage()
    {
        if (ArrangementDeferred || _arranging || _updatingMetrics || IsDisposed) return;
        var index = Tabs.SelectedIndex;
        if ((uint)index >= (uint)Pages.Count) return;
        _arranging = true;
        try
        {
            var viewport = Pages[index].Viewport;
            viewport.Bounds = PageViewportBounds(Tabs.DisplayRectangle.Size);
            viewport.ArrangeContent();
        }
        finally { _arranging = false; }
    }

    private Rectangle PageViewportBounds(Size body) =>
        new(Inset, Inset, Math.Max(1, body.Width - 2 * Inset), Math.Max(1, body.Height - 2 * Inset));

    public void ArrangeViewport()
    {
        if (_arranging || ArrangementDeferred || _updatingMetrics || IsDisposed) return;
        _arranging = true;
        try
        {
            var gap = UiDesignTokens.Pixels(Tokens.SectionGap, DeviceDpi);
            var width = Math.Max(1, ClientSize.Width - 2 * Inset);
            var footer = Actions.MeasureForWidth(width);
            Actions.Bounds = new Rectangle(Inset, Math.Max(Inset, Height - Inset - footer), width, footer);
            Tabs.Bounds = new Rectangle(Inset, Inset, width, Math.Max(1, Actions.Top - gap - Inset));
            var body = Tabs.DisplayRectangle.Size;
            foreach (var entry in Pages)
            {
                entry.Viewport.Bounds = PageViewportBounds(body);
                if (entry.Viewport.Parent == Tabs.SelectedTab)
                {
                    entry.Viewport.ArrangeContent();
                }
            }
        }
        finally { _arranging = false; }
    }
}
