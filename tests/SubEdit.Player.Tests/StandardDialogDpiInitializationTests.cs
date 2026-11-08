using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class StandardDialogDpiInitializationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("Find")]
    [InlineData("Correction")]
    [InlineData("SaveQuestion")]
    [InlineData("Numeric")]
    public void DefaultFont_ShowDialogOwner_UsesOneDpiScaleFromFirstVisibility(string kind) => SubtitleDialogLayoutTests.Sta(() =>
    {
        var previousContext = SetThreadDpiAwarenessContext((IntPtr)(-4));
        try
        {
            using var owner = new Form { StartPosition = FormStartPosition.Manual };
            foreach (var screen in Screen.AllScreens.Concat(Screen.AllScreens.Reverse()))
            {
                owner.Bounds = new Rectangle(screen.WorkingArea.Left + 80, screen.WorkingArea.Top + 60, 900, 600);
                if (!owner.Visible) owner.Show();
                for (var opening = 0; opening < 2; opening++)
                {
                    using StandardDialog dialog = kind switch
                    {
                        "Find" => new SubtitleFindDialog("Przykładowy tekst"),
                        "Correction" => new SubtitleCorrectionDialog(new()),
                        "SaveQuestion" => new QuestionDialog("Aktualny plik tekstowy jest zmodyfikowany.\r\n\r\nCzy zachować zmiany?", true),
                        _ => new NumericDialog()
                    };
                    Rectangle? first = null;
                    var lateBounds = new List<Rectangle>();
                    var shown = false; var painted = false; var posted = false; var measuringOpening = true;
                    Exception? failure = null;
                    dialog.VisibleChanged += (_, _) => { if (dialog.Visible && first is null) first = dialog.Bounds; };
                    void Changed(object? sender, EventArgs e)
                    {
                        if (measuringOpening && first is { } original && dialog.Visible && dialog.Bounds != original)
                            lateBounds.Add(dialog.Bounds);
                    }
                    dialog.SizeChanged += Changed; dialog.LocationChanged += Changed;
                    void Ready()
                    {
                        if (!shown || !painted || posted) return;
                        posted = true;
                        dialog.BeginInvoke((Action)(() =>
                        {
                            try
                            {
                                measuringOpening = false;
                                var expected = UiDesignTokens.Current.DialogFont.SizeInPoints * owner.DeviceDpi / GetDpiForSystem();
                                Assert.Same(owner, dialog.Owner);
                                Assert.True(dialog.Modal);
                                Assert.Equal((uint)owner.DeviceDpi, GetDpiForWindow(dialog.Handle));
                                Assert.Equal(first, dialog.Bounds);
                                Assert.Empty(lateBounds);
                                CheckFonts(dialog, expected);
                                if (kind == "Find") Assert.True(SubtitleDialogLayoutTests.Descendants(dialog).OfType<TextBox>().Single().Focused);
                                if (opening == 0)
                                {
                                    var bounds = dialog.Bounds;
                                    typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null);
                                    Assert.Equal(bounds, dialog.Bounds);
                                    CheckFonts(dialog, expected);
                                }
                                output.WriteLine($"{kind} screen={screen.DeviceName} nativeDpi={GetDpiForWindow(dialog.Handle)} root={dialog.Font.SizeInPoints} expected={expected} bounds={dialog.Bounds}; lateBounds=0; opening={opening}");
                            }
                            catch (Exception e) { failure = e; }
                            finally { dialog.DialogResult = DialogResult.Cancel; }
                        }));
                    }
                    dialog.Shown += (_, _) => { shown = true; Ready(); };
                    dialog.Paint += (_, _) => { painted = true; Ready(); };
                    // No SetUiFont or Load override: this is the default production path.
                    dialog.ShowDialog(owner);
                    if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                    Assert.True(posted);
                    Assert.False(dialog.IsHandleCreated);
                    Assert.True(owner.Enabled);
                }
            }
            owner.Close();
        }
        finally { SetThreadDpiAwarenessContext(previousContext); }
    });

    private static void CheckFonts(StandardDialog dialog, float expected)
    {
        Assert.InRange(Math.Abs(dialog.Font.SizeInPoints - expected), 0, 0.01f);
        foreach (var child in SubtitleDialogLayoutTests.Descendants(dialog).Where(c => c is Label or ButtonBase or TextBox or NumericUpDown))
        {
            Assert.Equal(dialog.DeviceDpi, child.DeviceDpi);
            Assert.InRange(Math.Abs(child.Font.SizeInPoints - expected), 0, 0.01f);
        }
        foreach (var number in SubtitleDialogLayoutTests.Descendants(dialog).OfType<NumericUpDown>())
            Assert.True(number.Height >= number.PreferredSize.Height);
    }

    // Production StandardDialog composition; existing production numeric dialogs use
    // TextBox. This fixture additionally guards the native UpDown initialization path.
    private sealed class NumericDialog : StandardDialog
    {
        public NumericDialog() : base(400)
        {
            Page.AddSection("Wartość").AddField("Czas:", new NumericUpDown { Minimum = 0, Maximum = 99, DecimalPlaces = 2, Value = 5 });
            ConfigureActions();
        }
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
