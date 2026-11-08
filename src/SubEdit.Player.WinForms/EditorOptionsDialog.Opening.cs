namespace SubEdit.Player.WinForms;

internal sealed partial class EditorOptionsDialog
{
    internal DialogResult ShowForOwner(Form owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Owner = owner;
        StartPosition = FormStartPosition.Manual;
        // Use a point inside the owner's working area before creating the dialog HWND.
        var work = Screen.FromControl(owner).WorkingArea;
        Location = work.Location + new Size(40, 40);
        _fit.HoldInitialLayout();
        return ShowDialog(owner);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Owner is not null) StartPosition = FormStartPosition.CenterParent;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _fit.CompleteInitialLayout();
        if (Owner is not null) CenterToParent();
    }
}
