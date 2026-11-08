namespace SubEdit.Player.UI.DesignSystem;

internal sealed class OptionsTabControl : TabControl
{
    internal OptionsTabControl() => DrawMode = TabDrawMode.Normal;

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        if (SelectedTab is not { IsHandleCreated: false } page
            || FindForm() is not { Visible: true } || Parent is not OptionsDialogLayout options)
        {
            base.OnSelectedIndexChanged(e);
            return;
        }

        // The native TabControl realizes exactly this page during selection.
        // Hold layout callbacks until its controls have their actual native metrics.
        var controls = Tree(page).ToArray();
        var wasDeferred = options.ArrangementDeferred;
        options.ArrangementDeferred = true;
        foreach (var control in controls) control.SuspendLayout();
        try { base.OnSelectedIndexChanged(e); }
        finally
        {
            for (var i = controls.Length - 1; i >= 0; i--) controls[i].ResumeLayout(false);
            options.ArrangementDeferred = wasDeferred;
        }
        options.ArrangeSelectedPage();
    }

    private static IEnumerable<Control> Tree(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (var descendant in Tree(child))
                yield return descendant;
    }
}
