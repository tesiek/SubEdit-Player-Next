using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

[Collection("Diagnostics UI")]
public class VideoPropertiesDialogTests
{
    [Fact]
    public void Dialog_LongUnicodeMetadata_HasOneScrollRegionAndFixedButtonsAndFullReport()
    {
        RunSta(() =>
        {
            var snapshot = VideoFilePropertiesTests.LargeSnapshot();
            string? copied = null;
            using var dialog = new VideoPropertiesDialog(snapshot, value => copied = value);
            dialog.Show(); Pump();
            var content = dialog.ReportContent;
            var report = content.Report;
            var footer = dialog.LayoutView.Actions;
            Assert.Equal(new[] { "Kopiuj", "Zamknij" }, footer.Buttons.Values.Select(b => b.Text));
            Assert.Equal(VideoFilePropertiesFormatter.FormatText(snapshot), dialog.ReportText);
            foreach (var section in VideoFilePropertiesFormatter.GetSections(snapshot))
            {
                Assert.Contains(section.Heading, report.Text);
                foreach (var (label, value) in section.Rows)
                { Assert.Contains(label, report.Text); Assert.Contains(value, report.Text); }
            }
            Assert.True(report.ReadOnly && report.WordWrap);
            Assert.Equal(RichTextBoxScrollBars.Vertical, report.ScrollBars);
            Assert.True(Screen.FromControl(dialog).WorkingArea.Contains(dialog.Bounds));
            foreach (var width in new[] { 500, 900, 600 })
            {
                dialog.Size = new System.Drawing.Size(width, 400); Pump();
                Assert.True(content.ClientRectangle.Contains(report.Bounds));
                Assert.All(footer.Buttons.Values, button => Assert.True(footer.RectangleToScreen(footer.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle))));
            }
            var headerBefore = dialog.LayoutView.Header.Bounds;
            var footerBefore = footer.Bounds;
            content.ScrollToEnd(); Pump();
            Assert.Equal(headerBefore, dialog.LayoutView.Header.Bounds);
            Assert.Equal(footerBefore, footer.Bounds);
            Assert.Contains("Pełny tytuł ścieżki #40", report.Text);
            Assert.Contains("Rozdział #9", report.Text);
            report.Select(0, 12);
            Assert.True(dialog.HandleTextShortcut(Keys.Control | Keys.C));
            Assert.Equal(report.SelectedText, copied);
            footer.Button(DialogActionRole.Secondary).PerformClick();
            Assert.Equal(VideoFilePropertiesFormatter.FormatText(snapshot), copied);
            dialog.Close();
        });
    }

    [Fact]
    public void Dialog_EmptyFile_DoesNotRequireBackendAndUsesSameFallbackReport()
    {
        RunSta(() =>
        {
            var snapshot = new VideoFileProperties("file");
            using var dialog = new VideoPropertiesDialog(snapshot);
            Assert.Equal(VideoFilePropertiesFormatter.FormatText(snapshot), dialog.ReportText);
            Assert.Contains("[WIDEO]", dialog.ReportText);
            Assert.Contains("[AUDIO]", dialog.ReportText);
        });
    }

    private static void Pump() { for (var i = 0; i < 4; i++) Application.DoEvents(); }
    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WinForms UI test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
