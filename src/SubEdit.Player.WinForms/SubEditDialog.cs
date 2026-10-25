namespace SubEdit.Player.WinForms;

internal abstract class SubEditDialog : Form
{
    protected SubEditDialog()
    {
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        BackColor = SystemColors.Control;
        ForeColor = SystemColors.ControlText;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
    }

    protected override void OnLoad(EventArgs e)
    {
        EnsureDpiScaling();
        base.OnLoad(e);
        NormalizeContentMargins();
        NormalizeStandardButtonRow();
        if (Owner is not { IsDisposed: false } owner)
        {
            return;
        }

        Location = new Point(
            owner.Left + (owner.Width - Width) / 2,
            owner.Top + (owner.Height - Height) / 2);
    }

    private void EnsureDpiScaling()
    {
        if (AutoScaleMode != AutoScaleMode.Font)
        {
            return;
        }

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        PerformAutoScale();
    }

    private void NormalizeContentMargins()
    {
        var margin = ScaleLogicalPixels(15);
        foreach (var groupBox in Controls.OfType<GroupBox>())
        {
            if (groupBox.Left <= margin && groupBox.Right >= ClientSize.Width - margin)
            {
                groupBox.Left = margin;
                groupBox.Width = ClientSize.Width - 2 * margin;
            }

            foreach (var option in groupBox.Controls.OfType<ButtonBase>().Where(control => control is CheckBox or RadioButton))
            {
                if (option.Left < margin)
                {
                    option.Left = margin;
                }
            }
        }

        var saveAsDefault = Controls
            .OfType<CheckBox>()
            .SingleOrDefault(checkBox => string.Equals(checkBox.Text, "Zachowaj ustawienia jako domyślne", StringComparison.Ordinal));
        if (saveAsDefault is null)
        {
            return;
        }

        var peerCheckBoxes = Controls
            .OfType<CheckBox>()
            .Where(checkBox => checkBox != saveAsDefault)
            .ToArray();
        saveAsDefault.Left = peerCheckBoxes.Length == 0
            ? margin
            : peerCheckBoxes.Min(checkBox => checkBox.Left);
    }

    private void NormalizeStandardButtonRow()
    {
        var ok = Controls
            .OfType<Button>()
            .SingleOrDefault(button => string.Equals(button.Text, "OK", StringComparison.Ordinal));
        var cancel = Controls
            .OfType<Button>()
            .SingleOrDefault(button => string.Equals(button.Text, "Anuluj", StringComparison.Ordinal));

        if (ok is null || (cancel is not null && ok.Left >= cancel.Left))
        {
            return;
        }

        var buttons = cancel is null ? new[] { ok } : new[] { ok, cancel };
        var buttonWidth = Math.Max(
            ScaleLogicalPixels(75),
            buttons.Max(button => button.GetPreferredSize(Size.Empty).Width));
        var buttonHeight = Math.Max(
            ScaleLogicalPixels(25),
            buttons.Max(button => button.GetPreferredSize(Size.Empty).Height));
        var gap = cancel is null
            ? 0
            : Math.Max(ScaleLogicalPixels(6), cancel.Left - ok.Right);
        var rowWidth = buttons.Length * buttonWidth + gap;
        var left = Math.Max(0, (ClientSize.Width - rowWidth) / 2);
        var top = buttons.Min(button => button.Top);

        ok.SetBounds(left, top, buttonWidth, buttonHeight);
        if (cancel is not null)
        {
            cancel.SetBounds(left + buttonWidth + gap, top, buttonWidth, buttonHeight);
        }

        var bottomMargin = ScaleLogicalPixels(12);
        ClientSize = new Size(ClientSize.Width, buttons.Max(button => button.Bottom) + bottomMargin);
    }

    private int ScaleLogicalPixels(int logicalPixels) =>
        (int)Math.Round(logicalPixels * DeviceDpi / 96d);
}
