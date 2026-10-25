using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests.Playback;

public class AboutDialogTests
{
    [Fact]
    public void VersionLabel_UsesSharedProductVersionAndOriginalBuildDateWithoutCommit()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new AboutDialog();
                var layout = Assert.Single(dialog.Controls.OfType<TableLayoutPanel>());
                var label = Assert.Single(layout.Controls.OfType<Label>(), value => value.Text.StartsWith("Version "));
                Assert.Equal($"Version {ApplicationBuildInfo.Current.ProductVersion}\r\nbuild date: {ApplicationBuildInfo.Current.BuildDate}", label.Text);
                Assert.StartsWith("Version 1.0.0\r\n", label.Text);
                Assert.DoesNotContain(ApplicationBuildInfo.Current.BuildCommit, label.Text);
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void DescriptionText_MatchesExactRequiredContract()
    {
        const string expected = "SubEdit-Player Next\r\nopracowany przy pomocy AI\r\nna podstawie SubEdit-Player build 4072";

        Assert.Equal(expected, AboutDialog.DescriptionText);
    }

    [Fact]
    public void DescriptionText_ContainsRequiredSubstringsAndCase()
    {
        var text = AboutDialog.DescriptionText;
        var lines = text.Split("\r\n");

        Assert.Equal(3, lines.Length);
        Assert.Equal("SubEdit-Player Next", lines[0]);
        Assert.Equal("opracowany przy pomocy AI", lines[1]);
        Assert.Equal("na podstawie SubEdit-Player build 4072", lines[2]);

        // Verifying specific constraints from user specification:
        Assert.StartsWith("opracowany", lines[1]); // lowercase start
        Assert.Contains("AI", lines[1]);
        Assert.StartsWith("na podstawie", lines[2]); // lowercase start
        Assert.Contains("SubEdit-Player", lines[2]);
        Assert.Contains("4072", lines[2]);

        // Verifying removed legacy tokens
        Assert.DoesNotContain("OpenAI", text);
        Assert.DoesNotContain("ChatGPT", text);
        Assert.DoesNotContain("Gemini", text);
        Assert.DoesNotContain("oryginalnego", text);
    }
}
