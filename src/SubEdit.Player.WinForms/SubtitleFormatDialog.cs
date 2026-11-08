using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;
internal sealed class SubtitleFormatDialog : StandardDialog
{
    private readonly Dictionary<SubtitleTimingFormat, RadioButton> _buttons = [];
    public SubtitleFormatDialog(SubtitleTimingFormat selectedFormat) : base(340)
    {
        Text = "Aktywny format napisów";
        var section = Page.AddSection("Format docelowy");
        foreach (var (format, caption, description) in new[] {
            (SubtitleTimingFormat.TmPlayerLong, "00:00:00", "- czasowy"), (SubtitleTimingFormat.TmPlayerShort, "0:00:00", "- czasowy"),
            (SubtitleTimingFormat.MicroDvd, "{000}{000}", "- klatkowy, mDVD"), (SubtitleTimingFormat.Mpl2, "[000][000]", "- czasowy, MPL2") })
            _buttons.Add(format, section.AddRadioOption(caption, description, format == selectedFormat));
        ConfigureActions();
        Shown += (_, _) => _buttons.Single(p => p.Value.Checked).Value.Focus();
    }
    public SubtitleTimingFormat SelectedFormat => _buttons.Single(pair => pair.Value.Checked).Key;
}
