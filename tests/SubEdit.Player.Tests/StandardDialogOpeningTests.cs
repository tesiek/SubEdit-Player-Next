using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class StandardDialogOpeningTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 9f)] [InlineData(false, 12f)] [InlineData(false, 24f)]
    [InlineData(true, 9f)] [InlineData(true, 12f)] [InlineData(true, 24f)]
    public void ModalOpening_IsFinalAtFirstVisibility_AndFontRoundTrip(bool complex, float points) => SubtitleDialogLayoutTests.Sta(() =>
    {
        foreach (var screen in Screen.AllScreens)
        {
            using var owner = CreateOwner(screen);
            owner.Show();
            using StandardDialog dialog = complex ? new LongSubtitleLineSplitDialog(new()) : new SubtitleFindDialog("Przykładowy tekst");
            // Exercise the real Form API too; no call-site wrapper may be required.
            RunModal(dialog, owner, points, () =>
            {
                var input = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().First();
                Assert.True(input.Focused, "Existing Shown focus must survive preparation");
                Key(input, Keys.Tab); Key(Focused(dialog), Keys.Shift | Keys.Tab);
                Assert.True(input.Focused, "Native Tab/Shift+Tab round-trip");
                AssertControlsFit(dialog);
                var original = dialog.Size;
                dialog.SetUiFont(points == 24 ? 9 : 24);
                dialog.BeginInvoke((Action)(() => CompleteSafely(dialog, () =>
                {
                    AssertControlsFit(dialog);
                    dialog.SetUiFont(points);
                    dialog.BeginInvoke((Action)(() => CompleteSafely(dialog, () =>
                    {
                        Assert.Equal(original, dialog.Size);
                        AssertControlsFit(dialog);
                        var cancelled = false;
                        FormClosingEventHandler cancel = (_, e) => { e.Cancel = true; cancelled = true; };
                        dialog.FormClosing += cancel;
                        dialog.Close();
                        dialog.FormClosing -= cancel;
                        Assert.True(cancelled && dialog.Visible && dialog.IsHandleCreated);
                        dialog.CancelButton!.PerformClick();
                    })));
                })));
            });
            owner.Close();
        }
    });

    [Theory]
    [InlineData(false, Keys.Enter)] [InlineData(false, Keys.Escape)]
    [InlineData(true, Keys.Enter)] [InlineData(true, Keys.Escape)]
    public void ModalOpening_PreservesNativeKeysAndProductionActions(bool complex, Keys key) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner = CreateOwner(Screen.PrimaryScreen!); owner.Show();
        using StandardDialog dialog = complex ? new LongSubtitleLineSplitDialog(new()) : new SubtitleFindDialog("fraza");
        var next = 0; var previous = 0;
        if (dialog is SubtitleFindDialog find)
        {
            find.FindNextRequested += (_, _) => next++;
            find.FindPreviousRequested += (_, _) => previous++;
        }
        var result = RunModal(dialog, owner, 12, () =>
        {
            Key(Focused(dialog), key);
            if (!complex && key == Keys.Enter)
            {
                Assert.Equal(1, next);
                Assert.True(dialog.Visible);
                var message = new Message();
                Assert.True((bool)typeof(SubtitleFindDialog).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog, [message, Keys.Shift | Keys.F3])!);
                Assert.Equal(1, previous);
                Assert.Equal("fraza", ((SubtitleFindDialog)dialog).Query);
                Key(Focused(dialog), Keys.Escape);
            }
        });
        Assert.Equal(complex && key == Keys.Enter ? DialogResult.OK : DialogResult.Cancel, result);
        if (complex)
        {
            var settings = ((LongSubtitleLineSplitDialog)dialog).CreateSettings();
            Assert.Equal(40, settings.MaximumLineLength); Assert.Equal(3, settings.MaximumLines);
        }
        owner.Close();
    });

    [Fact]
    public void OtherStandardDialogs_ModalOpeningSmoke() => SubtitleDialogLayoutTests.Sta(() =>
    {
        Func<StandardDialog>[] create = [
            () => new QuestionDialog("Czy zachować zmiany?", true),
            () => new FindLongSubtitleLineDialog(45), () => new SubtitleFormatDialog(SubtitleTimingFormat.MicroDvd),
            () => new SubtitleConversionDialog(), () => new DefaultFrameRateDialog(23.976, 25),
            () => new SubtitleFrameRateDialog(25), () => new FindShortSubtitleDialog(new(), 25),
            () => new FindSubtitleGapDialog(new(), 25), () => new GoToSubtitleDialog(25),
            () => new GoToVideoPositionDialog(25, 1000), () => new ItalicMarkerConversionDialog(),
            () => new SubtitleLineLimitDialog(new()), () => new UnknownSubtitleLineDialog(true),
            () => new SubtitleTimeShiftDialog(25, .5), () => new SubtitleCorrectionDialog(new()),
            () => new SubtitleDisplayTimeDialog(25, new()), () => new RapidSubtitleMergeDialog(new()),
            () => new MergeSubtitleSelectLineDialog(["Pierwsza linia", "Druga linia"], true),
            () => new GeometricSubtitleShiftDialog(new(0, "Początek", 1000, 1300), new(30, "Koniec", 10000, 10500), 25, true, true),
            () => new UnsortedSubtitlesWarningDialog(5)
        ];
        using var owner = CreateOwner(Screen.PrimaryScreen!); owner.Show();
        foreach (var factory in create)
        {
            using var dialog = factory();
            RunModal(dialog, owner, 9, () =>
            {
                AssertControlsFit(dialog);
                if (dialog.CancelButton is { } cancel) cancel.PerformClick();
                else dialog.Close(); // Countdown warning deliberately has no Cancel action.
            });
        }
        owner.Close();
    });

    private Exception? _callbackError;
    private void CompleteSafely(StandardDialog dialog, Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            _callbackError = error;
            dialog.DialogResult = DialogResult.Cancel;
        }
    }

    private DialogResult RunModal(StandardDialog dialog, Form owner, float points, Action ready)
    {
        _callbackError = null;
        Rectangle? firstVisible = null;
        var laterBounds = new List<Rectangle>();
        var shown = false; var painted = false; var posted = false; var opening = true;
        void TryReady()
        {
            if (!shown || !painted || posted) return;
            posted = true;
            dialog.BeginInvoke((Action)(() => CompleteSafely(dialog, () =>
            {
                opening = false;
                output.WriteLine($"{dialog.GetType().Name} {points}pt DPI={dialog.DeviceDpi}: first={firstVisible}; ready={dialog.Bounds}; late={laterBounds.Count}");
                Assert.NotNull(firstVisible);
                Assert.Equal(firstVisible.Value, dialog.Bounds);
                Assert.Empty(laterBounds);
                Assert.True(dialog.Modal); Assert.Same(owner, dialog.Owner);
                Assert.False(IsWindowEnabled(owner.Handle), "Native modal loop disables the owner HWND, not Control.Enabled");
                Assert.Equal(FormStartPosition.CenterParent, dialog.StartPosition);
                var area = Screen.FromControl(owner).WorkingArea;
                Assert.Equal(Math.Clamp(owner.Left + (owner.Width - dialog.Width) / 2, area.Left, Math.Max(area.Left, area.Right - dialog.Width)), dialog.Left);
                Assert.Equal(Math.Clamp(owner.Top + (owner.Height - dialog.Height) / 2, area.Top, Math.Max(area.Top, area.Bottom - dialog.Height)), dialog.Top);
                Assert.Equal(owner.DeviceDpi, dialog.DeviceDpi);
                Assert.All(SubtitleDialogLayoutTests.Descendants(dialog), c => Assert.True(c.IsHandleCreated, c.GetType().Name));
                ready();
            })));
        }
        dialog.Load += (_, _) => dialog.SetUiFont(points);
        dialog.VisibleChanged += (_, _) => { if (dialog.Visible && firstVisible is null) firstVisible = dialog.Bounds; };
        void BoundsChanged(object? sender, EventArgs e)
        {
            if (opening && firstVisible is { } first && dialog.Visible && dialog.Bounds != first)
                laterBounds.Add(dialog.Bounds);
        }
        dialog.SizeChanged += BoundsChanged; dialog.LocationChanged += BoundsChanged;
        dialog.Shown += (_, _) => { shown = true; TryReady(); };
        dialog.Paint += (_, _) => { painted = true; TryReady(); };
        var result = ((Form)dialog).ShowDialog(owner);
        Assert.True(owner.Enabled && IsWindowEnabled(owner.Handle)); Assert.False(dialog.IsHandleCreated);
        if (_callbackError is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_callbackError).Throw();
        Assert.True(posted, "Reached first Paint and Shown");
        return result;
    }

    private static Form CreateOwner(Screen screen)
    {
        var area = screen.WorkingArea;
        return new Form { StartPosition = FormStartPosition.Manual,
            Bounds = new Rectangle(area.Left + 80, area.Top + 65, Math.Min(1050, area.Width - 100), Math.Min(780, area.Height - 100)) };
    }
    private static Control Focused(StandardDialog dialog) => SubtitleDialogLayoutTests.Descendants(dialog).First(c => c.Focused);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr handle);
    private static void Key(Control control, Keys key) => Assert.True((bool)typeof(Control)
        .GetMethod("ProcessDialogKey", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [key])!);
    private static void AssertControlsFit(StandardDialog dialog)
    {
        Assert.False(dialog.Shell.Viewport.HorizontalScroll.Visible);
        foreach (var text in SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().Where(c => c.Visible))
        {
            Assert.True(text.Height >= text.PreferredHeight, $"TextBox {text.Bounds} vs native height {text.PreferredHeight}");
            Assert.True(text.Parent!.ClientRectangle.Contains(text.Bounds), $"TextBox outside {text.Parent.Bounds}: {text.Bounds}");
        }
        foreach (var label in SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<Label>().Where(c => c.Visible && c.Text.Length > 0))
            Assert.True(label.Height >= label.GetPreferredSize(new Size(label.Width, 0)).Height - 1, $"Clipped: {label.Text}");
        Assert.All(dialog.Shell.Actions.Buttons.Values, b => Assert.True(b.Height >= b.GetPreferredSize(Size.Empty).Height));
    }
}
