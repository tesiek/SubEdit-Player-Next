using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Subtitle dialog layout", DisableParallelization = true)]
public sealed class SubtitleDialogLayoutCollection;

[Collection("Subtitle dialog layout")]
public class SubtitleDialogLayoutTests
{
    private const string SrtQuestion = "Otwierany plik napisów jest w formacie SubRip (SRT).\r\n" +
                                       "Czy przekonwertować napisy do formatu mDVD?";

    [Theory]
    [InlineData(9f, 1f)]
    [InlineData(9.99f, 1f)]
    [InlineData(10.5f, 1f)]
    [InlineData(11.25f, 1.25f)]
    public void Correction_DialogueOptionsFitTheirRow(float fontSize, float geometryScale)
    {
        RunSta(() =>
        {
            using var font = new Font("Segoe UI", fontSize);
            using var dialog = new SubtitleCorrectionDialog(new SubtitleCorrectionSettings());
            ShowWithMetrics(dialog, font, geometryScale);
            var row = Assert.Single(dialog.Controls.OfType<Panel>());
            var radios = row.Controls.OfType<RadioButton>().ToArray();
            Assert.Equal(2, radios.Length);
            foreach (var radio in radios)
            {
                Assert.True(row.ClientRectangle.Contains(radio.Bounds), $"Row {row.ClientSize} clips {radio.Bounds} at {fontSize} pt.");
                Assert.True(radio.Height >= radio.GetPreferredSize(Size.Empty).Height);
                Assert.True(radio.Padding.Left >= (int)Math.Round(4 * geometryScale), "Radio glyph needs an inner left inset.");
            }
            Assert.True(radios[0].Right + (int)Math.Round(4 * geometryScale) <= radios[1].Left,
                $"First radio {radios[0].Bounds} overlaps second radio {radios[1].Bounds} at {fontSize} pt.");
            var next = dialog.Controls.OfType<CheckBox>().Single(control => control.Text == "&Usuwaj puste wiersze");
            Assert.True(row.Bottom <= next.Top, $"Dialogue row overlaps the following option at {fontSize} pt.");
            Assert.All(dialog.Controls.OfType<Button>(), button => Assert.True(dialog.ClientRectangle.Contains(button.Bounds)));
        });
    }

    [Theory]
    [InlineData(9f, 1f)]
    [InlineData(9.99f, 1f)]
    [InlineData(11.25f, 1.25f)]
    public void Question_EntireMessageAndButtonsFit(float fontSize, float geometryScale)
    {
        RunSta(() =>
        {
            using var font = new Font("Segoe UI", fontSize);
            foreach (var (message, includeCancel) in new[]
                     {
                         (SrtQuestion, false),
                         ("Posortować napisy rosnąco?", false),
                         ("Aktualny plik tekstowy jest zmodyfikowany.\r\n\r\nCzy zachować zmiany?", true),
                         ("Pierwszy wiersz\r\nDrugi wiersz\r\nTrzeci wiersz\r\nCzwarty wiersz", false)
                     })
            {
                using var dialog = new QuestionDialog(message, includeCancel, "Yes", "No");
                ShowWithMetrics(dialog, font, geometryScale);
                var label = Assert.Single(dialog.Controls.OfType<Label>());
                var buttons = Assert.Single(dialog.Controls.OfType<Panel>());
                Assert.Equal(message, label.Text);
                Assert.True(dialog.ClientRectangle.Contains(label.Bounds), $"Dialog {dialog.ClientSize} clips label {label.Bounds} at {fontSize} pt.");
                Assert.True(label.Width >= label.GetPreferredSize(Size.Empty).Width);
                Assert.True(dialog.ClientSize.Width - label.Right >= (int)Math.Round(18 * geometryScale),
                    "Question text needs a DPI-scaled right margin.");
                Assert.True(label.Bottom <= buttons.Top);
                Assert.All(buttons.Controls.OfType<Button>(), button => Assert.True(buttons.ClientRectangle.Contains(button.Bounds)));
                Assert.Equal(DialogResult.Yes, Assert.IsType<Button>(dialog.AcceptButton).DialogResult);
                Assert.Equal(includeCancel ? DialogResult.Cancel : DialogResult.No, Assert.IsType<Button>(dialog.CancelButton).DialogResult);
            }
        });
    }

    [Fact]
    public void Dialogs_FitWithActualSystemFontAndNormalDpiLifecycle()
    {
        RunSta(() =>
        {
            using var correction = new SubtitleCorrectionDialog(new SubtitleCorrectionSettings()) { Opacity = 0 };
            correction.Show();
            var row = Assert.Single(correction.Controls.OfType<Panel>());
            Assert.All(row.Controls.OfType<RadioButton>(), radio => Assert.True(row.ClientRectangle.Contains(radio.Bounds)));
            var radios = row.Controls.OfType<RadioButton>().ToArray();
            Assert.True(radios[0].Right + radios[0].Padding.Left <= radios[1].Left,
                $"System font {correction.Font}: first radio {radios[0].Bounds}, second radio {radios[1].Bounds}.");
            using var question = new QuestionDialog(SrtQuestion, false, "Yes", "No") { Opacity = 0 };
            question.Show();
            var label = Assert.Single(question.Controls.OfType<Label>());
            Assert.True(question.ClientRectangle.Contains(label.Bounds));
            Assert.True(question.ClientSize.Width - label.Right >= (int)Math.Round(18 * question.DeviceDpi / 96d));
        });
    }

    private static void ShowWithMetrics(Form dialog, Font font, float geometryScale)
    {
        // Simulate text-only growth separately from 120-DPI geometry without changing Windows settings.
        dialog.AutoScaleMode = AutoScaleMode.None;
        dialog.Font = font;
        dialog.Scale(new SizeF(geometryScale, geometryScale));
        dialog.Opacity = 0;
        dialog.Show();
        dialog.PerformLayout();
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Subtitle dialog layout test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
