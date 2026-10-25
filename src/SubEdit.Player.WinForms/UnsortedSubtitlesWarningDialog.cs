namespace SubEdit.Player.WinForms;

internal sealed class UnsortedSubtitlesWarningDialog : SubEditDialog
{
    private readonly Label _countLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _secondsRemaining = 5;

    public UnsortedSubtitlesWarningDialog(int subtitleLineNumber)
    {
        Text = "Ostrzeżenie";
        ClientSize = new Size(320, 106);

        var icon = new PictureBox
        {
            Image = SystemIcons.Warning.ToBitmap(),
            Location = new Point(15, 15),
            Size = new Size(32, 32),
            SizeMode = PictureBoxSizeMode.AutoSize
        };
        var message = new Label
        {
            Text = $"Nieposortowany napis w linii {subtitleLineNumber}",
            Location = new Point(65, 25),
            AutoSize = true
        };
        _countLabel = new Label
        {
            Text = _secondsRemaining.ToString(),
            Location = new Point(0, 45),
            Size = new Size(320, 16),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(122, 70),
            Size = new Size(75, 22)
        };
        Controls.AddRange([icon, message, _countLabel, ok]);
        AcceptButton = ok;

        _timer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _timer.Tick += (_, _) => AdvanceCountdown();
        Shown += (_, _) => _timer.Start();
        FormClosed += (_, _) => _timer.Stop();
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
