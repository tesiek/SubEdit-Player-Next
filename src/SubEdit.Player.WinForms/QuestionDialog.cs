using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;
internal sealed class QuestionDialog : StandardDialog
{
    private readonly Bitmap _icon = SystemIcons.Question.ToBitmap();
    public QuestionDialog(string message, bool includeCancel, string yesText = "Tak", string noText = "Nie") : base(360)
    {
        Text = "Pytanie"; Page.AddMessage(message, _icon);
        var actions = new List<DialogAction> { new(DialogActionRole.Yes, yesText, DialogResult.Yes), new(DialogActionRole.No, noText, DialogResult.No) };
        if (includeCancel) actions.Add(new(DialogActionRole.Cancel, "Anuluj", DialogResult.Cancel));
        Shell.Actions.Configure(DialogActionProfile.Confirmation, actions.ToArray());
        AcceptButton = Shell.Actions.Button(DialogActionRole.Yes);
        CancelButton = Shell.Actions.Button(includeCancel ? DialogActionRole.Cancel : DialogActionRole.No);
        Adaptive.PreferredClientWidthProvider = () => Math.Max(UiDesignTokens.Pixels(360, DeviceDpi),
            Page.MeasureUnwrappedMessageWidth() + 2 * Shell.Tokens.Inset(Shell.Profile, DeviceDpi));
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _icon.Dispose(); }
}
