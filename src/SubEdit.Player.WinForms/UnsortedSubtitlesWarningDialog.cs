namespace SubEdit.Player.WinForms;

internal sealed class UnsortedSubtitlesWarningDialog : StandardDialog
{
    private readonly Label _countLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _secondsRemaining = 5;

    public UnsortedSubtitlesWarningDialog(int subtitleLineNumber) : base(340)
    {
        Text = "Ostrzeżenie";
        Page.AddSection("Nieposortowane napisy").AddText($"Nieposortowany napis w linii {subtitleLineNumber}");
        _countLabel = new Label { Text = _secondsRemaining.ToString(), AutoSize = true };
        Page.AddSection("Automatyczne zamknięcie").AddAdaptiveRow().AddControl(_countLabel);
        Shell.Actions.Configure(SubEdit.Player.UI.DesignSystem.DialogActionProfile.CenteredOkCancel,
            new SubEdit.Player.UI.DesignSystem.DialogAction(SubEdit.Player.UI.DesignSystem.DialogActionRole.Ok, "OK", DialogResult.OK));
        AcceptButton = Shell.Actions.Button(SubEdit.Player.UI.DesignSystem.DialogActionRole.Ok);
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => AdvanceCountdown();
        Shown += (_, _) => _timer.Start(); FormClosed += (_, _) => _timer.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AdvanceCountdown()
    {
        _secondsRemaining--;
        if (_secondsRemaining == 0)
        {
            DialogResult = DialogResult.OK;
            return;
        }

        _countLabel.Text = _secondsRemaining.ToString();
    }
}
