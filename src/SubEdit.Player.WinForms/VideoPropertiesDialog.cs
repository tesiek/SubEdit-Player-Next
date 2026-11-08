using SubEdit.Player.Core.Playback;
using SubEdit.Player.UI.DesignSystem;

namespace SubEdit.Player.WinForms;

internal sealed class VideoPropertiesDialog : InspectorDialog
{
    public VideoPropertiesDialog(VideoFileProperties snapshot, Action<string>? writeClipboard = null)
        : base("Właściwości pliku wideo", snapshot.FileName, writeClipboard)
    {
        var sections = VideoFilePropertiesFormatter.GetSections(snapshot);
        SetReport(VideoFilePropertiesFormatter.FormatText(sections), sections.Select(section =>
            new InspectorSection(section.Heading, section.Rows.Select(row => new InspectorValue(row.Label, row.Value)).ToArray())).ToArray());
    }
}
