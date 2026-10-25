using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

public class VideoPropertiesDialogTests
{
    [Fact]
    public void Dialog_LongUnicodeMetadata_HasOneScrollRegionAndFixedButtonsAndFullReport()
    {
        RunSta(() =>
        {
            var snapshot = VideoFilePropertiesTests.LargeSnapshot();
            using var dialog = new VideoPropertiesDialog(snapshot) { Opacity = 0 };
            dialog.Show(); // Native WinForms layout only; no player or media runtime.
            var viewport = Assert.Single(dialog.Controls.OfType<Panel>(), panel => panel.AutoScroll);
            var footer = Assert.Single(dialog.Controls.OfType<Panel>(), panel => !panel.AutoScroll);
            var content = Assert.Single(viewport.Controls.OfType<TableLayoutPanel>());
            Assert.Equal(new[] { "OK", "Kopiuj" }, footer.Controls.OfType<Button>().Select(button => button.Text));
            Assert.Equal(VideoFilePropertiesFormatter.FormatText(snapshot), dialog.ReportText);
            var subtitleRows = Assert.Single(VideoFilePropertiesFormatter.GetSections(snapshot), section => section.Heading.StartsWith("NAPISY")).Rows;
            foreach (var (_, value) in subtitleRows)
            {
                Assert.Contains(content.Controls.OfType<Label>(), label => label.Text == value);
                Assert.Contains(value, dialog.ReportText);
            }
            Assert.True(viewport.VerticalScroll.Visible);
            Assert.False(viewport.HorizontalScroll.Visible);
            var area = Screen.FromControl(dialog).WorkingArea;
            Assert.True(area.Contains(dialog.Bounds));
            foreach (var width in new[] { 500, 900, 600 })
            {
                dialog.Size = new System.Drawing.Size(width, 400);
                dialog.PerformLayout();
                content.PerformLayout();
                Assert.False(viewport.HorizontalScroll.Visible);
                Assert.True(content.Width <= viewport.ClientSize.Width);
                Assert.All(content.Controls.OfType<Label>().Where(label => label.AutoSize), label =>
                    Assert.True(label.Height >= label.GetPreferredSize(new System.Drawing.Size(label.Width, 0)).Height));
            }
            var footerBeforeScroll = footer.Bounds;
            viewport.AutoScrollPosition = new System.Drawing.Point(0, content.Height);
            Assert.Equal(footerBeforeScroll, footer.Bounds);
            Assert.Contains("Pełny tytuł ścieżki #40", dialog.ReportText);
            Assert.Contains("Rozdział #9", dialog.ReportText);
            Assert.All(footer.Controls.OfType<Button>(), button => Assert.True(footer.ClientRectangle.Contains(button.Bounds)));
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

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WinForms layout test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
