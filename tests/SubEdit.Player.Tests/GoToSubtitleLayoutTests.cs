using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class GoToSubtitleLayoutTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 0f)] // Default production font, without a Load override.
    [InlineData(false, 9f)]
    [InlineData(false, 12f)]
    [InlineData(false, 24f)]
    [InlineData(true, 0f)]
    [InlineData(true, 9f)]
    [InlineData(true, 12f)]
    [InlineData(true, 24f)]
    public void Clock_RemainsOneCompactGroup_AtNativeWindowDpi(bool video, float points) => SubtitleDialogLayoutTests.Sta(() =>
    {
        var previous = SetThreadDpiAwarenessContext((IntPtr)(-4));
        try
        {
            using var owner = new Form { StartPosition = FormStartPosition.Manual };
            var opening = 0;
            foreach (var screen in Screen.AllScreens.Concat(Screen.AllScreens.Reverse()))
            {
                owner.Bounds = new Rectangle(screen.WorkingArea.Left + 80, screen.WorkingArea.Top + 60, 900, 600);
                if (!owner.Visible) owner.Show();
                using StandardDialog dialog = video ? new GoToVideoPositionDialog(25, 3723000) : new GoToSubtitleDialog(25);
                if (points > 0) dialog.Load += (_, _) => dialog.SetUiFont(points);
                var result = Run(dialog, owner, () =>
                {
                    Assert.Equal((uint)owner.DeviceDpi, GetDpiForWindow(dialog.Handle));
                    var inputs = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().ToArray();
                    Assert.Equal(5, inputs.Length);
                    Assert.Equal(new[] { 10, 13, 2, 2, 2 }, inputs.Select(t => t.MaxLength));
                    Assert.True(inputs[0].Focused);
                    foreach (var input in inputs.Skip(1))
                    {
                        Key(Focused(dialog), Keys.Tab);
                        Assert.True(input.Focused);
                    }
                    for (var i = 3; i >= 0; i--)
                    {
                        Key(Focused(dialog), Keys.Shift | Keys.Tab);
                        Assert.True(inputs[i].Focused);
                    }
                    if (video)
                    {
                        inputs[4].Focus(); Key(Focused(dialog), Keys.Tab);
                        Assert.Equal("&Aktualny", Focused(dialog).Text);
                        Key(Focused(dialog), Keys.Shift | Keys.Tab); Assert.True(inputs[4].Focused);
                        inputs[0].Focus();
                    }
                    var row = Assert.Single(SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<AdaptiveSettingsRow>(), r => r.Controls.OfType<FieldValueLayout>().Any());
                    var fields = row.Controls.OfType<FieldValueLayout>().ToArray();
                    Assert.True(row.KeepTogether);
                    Assert.Equal(row.PreferredContentWidth, row.MinimumContentWidth);
                    Assert.Equal(3, fields.Length);
                    Assert.Equal(new[] { "g", "m", "s" }, fields.Select(f => f.Description.Text));
                    var axis = inputs[0].PointToScreen(Point.Empty).X;
                    Assert.Equal(axis, inputs[1].PointToScreen(Point.Empty).X);
                    Assert.Equal(axis, inputs[2].PointToScreen(Point.Empty).X);
                    var captions = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<Label>()
                        .Where(l => new[] { "&Sekundy:", "&Klatki:", "&Czas:" }.Contains(l.Text)).ToArray();
                    Assert.Equal(3, captions.Length);
                    Assert.All(captions, c => Assert.Equal(ContentAlignment.MiddleRight, c.TextAlign));
                    Assert.Single(captions.Select(c => c.PointToScreen(new Point(c.Width, 0)).X).Distinct());
                    var samples = new[] { "99", "59", "59" };
                    for (var i = 0; i < fields.Length; i++)
                    {
                        var field = fields[i];
                        var expected = Math.Max(UiDesignTokens.Pixels(UiDesignTokens.Current.CompactFieldMinWidth, field.DeviceDpi),
                            TextRenderer.MeasureText(samples[i], field.Editor.Font).Width + UiDesignTokens.Pixels(UiDesignTokens.Current.FieldChromeWidth, field.DeviceDpi));
                        Assert.Equal(expected, field.Editor.Width);
                        Assert.True(field.ClientRectangle.Contains(field.Editor.Bounds));
                        Assert.True(field.ClientRectangle.Contains(field.Description.Bounds));
                        Assert.Equal(UiDesignTokens.Pixels(UiDesignTokens.Current.LabelFieldGap, field.DeviceDpi), field.Description.Left - field.Editor.Right);
                        Assert.Equal(fields[0].Top, field.Top);
                        Assert.True(row.ClientRectangle.Contains(field.Bounds));
                        if (i > 0) Assert.Equal(UiDesignTokens.Pixels(UiDesignTokens.Current.SectionGap, row.DeviceDpi), field.Left - fields[i - 1].Right);
                    }
                    Assert.False(dialog.Shell.Viewport.HorizontalScroll.Visible);
                    Assert.False(dialog.Shell.Viewport.VerticalScroll.Visible);
                    var size = dialog.Size;
                    var widths = inputs.Select(t => t.Width).ToArray();
                    inputs[0].Text = "359999";
                    Assert.Equal(new[] { "99", "59", "59" }, inputs.Skip(2).Select(t => t.Text));
                    Assert.Equal(widths, inputs.Select(t => t.Width));
                    Assert.Equal(size, dialog.Size);
                    inputs[2].Text = "01"; inputs[3].Text = "02"; inputs[4].Text = "03";
                    Assert.Equal(3723d, double.Parse(inputs[0].Text, CultureInfo.CurrentCulture));
                    Assert.Equal("93075", inputs[1].Text);
                    Assert.Equal(widths, inputs.Select(t => t.Width));
                    output.WriteLine($"{dialog.GetType().Name}; DPI={dialog.DeviceDpi}; font={dialog.Font.SizeInPoints}; size={dialog.Size}; minimum={dialog.MinimumSize}; clock={row.Size}; fields={string.Join(",", inputs.Skip(2).Select(t => t.Width))}");
                    if (opening++ % 2 == 0)
                    {
                        Key(Focused(dialog), Keys.Enter);
                        Assert.Equal(3723d, Target(dialog));
                    }
                    else Key(Focused(dialog), Keys.Escape);
                });
                Assert.Equal(opening % 2 == 1 ? DialogResult.OK : DialogResult.Cancel, result);
                Assert.True(owner.Enabled);
            }
            owner.Close();
        }
        finally { SetThreadDpiAwarenessContext(previous); }
    });

    [Fact]
    public void VideoPosition_RetainsCurrentPositionButtonAndActions() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner = new Form(); owner.Show();
        using var dialog = new GoToVideoPositionDialog(25, 3723000);
        Assert.Equal(DialogResult.OK, Run(dialog, owner, () =>
        {
            var rows = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<AdaptiveSettingsRow>().ToArray();
            Assert.Equal(2, rows.Length); // Clock plus the existing Current-position button.
            var row = Assert.Single(rows, r => r.Controls.OfType<FieldValueLayout>().Any());
            Assert.True(row.KeepTogether);
            Assert.Equal(3, row.Controls.OfType<FieldValueLayout>().Count());
            var currentRow = Assert.Single(rows, r => r.Controls.OfType<Button>().Any());
            Assert.True(currentRow.Top >= row.Bottom);
            Assert.Equal(0, Assert.Single(currentRow.Controls.OfType<Button>()).Left);
            var inputs = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().ToArray();
            Assert.Equal(new[] { 10, 13, 2, 2, 2 }, inputs.Select(t => t.MaxLength));
            SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<Button>().Single(b => b.Text == "&Aktualny").PerformClick();
            Assert.Equal(new[] { "01", "02", "03" }, inputs.Skip(2).Select(t => t.Text));
            dialog.AcceptButton!.PerformClick();
            Assert.Equal(3723d, dialog.TargetSeconds);
        }));
        owner.Close();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Clock_ValidatesRangesAndPreservesUnboundedSecondsAndFrames(bool video) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner = new Form(); owner.Show();
        using StandardDialog dialog = video ? new GoToVideoPositionDialog(25, 360000000) : new GoToSubtitleDialog(25);
        Assert.Equal(DialogResult.OK, Run(dialog, owner, () =>
        {
            var inputs = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().ToArray();
            var toolTip = (ToolTip)dialog.GetType().GetField("_toolTip", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
            var size = dialog.Size;
            var widths = inputs.Select(t => t.Width).ToArray();
            void Clock(string h, string m, string s) { inputs[2].Text = h; inputs[3].Text = m; inputs[4].Text = s; }
            void CheckClock(string h, string m, string s) => Assert.Equal(new[] { h, m, s }, inputs.Skip(2).Select(t => t.Text));
            double Seconds() => double.Parse(inputs[0].Text, CultureInfo.CurrentCulture);
            Clock("00", "00", "00");
            Assert.Equal(0, Seconds()); Assert.Equal("0", inputs[1].Text);
            if (!video)
            {
                RejectOk(dialog); // Existing subtitle navigation requires a positive time.
                Assert.True(inputs[0].Focused);
            }
            Clock("99", "59", "59");
            Assert.Equal(359999, Seconds()); Assert.Equal("8999975", inputs[1].Text);
            foreach (var invalid in new[] { new[] { "100", "59", "59" }, new[] { "99", "60", "59" }, new[] { "99", "59", "60" } })
            {
                Clock("99", "59", "59");
                Clock(invalid[0], invalid[1], invalid[2]);
                Assert.Equal(359999, Seconds()); // No silent normalization or stale-value acceptance.
                RejectOk(dialog);
                Assert.True(inputs[2].Focused);
            }
            inputs[0].Text = "360000";
            Assert.Equal("9000000", inputs[1].Text); CheckClock("", "", "");
            Assert.All(inputs.Skip(2), t => Assert.Equal("Czas poza zakresem zegara 00:00:00–99:59:59", toolTip.GetToolTip(t)));
            inputs[1].Text = "9000025";
            Assert.Equal(360001, Seconds()); Assert.Equal("9000025", inputs[1].Text); CheckClock("", "", "");
            inputs[0].Text = "359999.5";
            Assert.Equal(359999.5, Seconds()); CheckClock("99", "59", "59"); // Existing fractional-second formatting.
            Assert.All(inputs.Skip(2), t => Assert.DoesNotContain("poza zakresem", toolTip.GetToolTip(t)));
            inputs[0].Text = "360000";
            Clock("01", "02", "03");
            Assert.Equal(3723, Seconds()); Assert.Equal("93075", inputs[1].Text);
            Assert.All(inputs.Skip(2), t => Assert.DoesNotContain("poza zakresem", toolTip.GetToolTip(t)));
            inputs[0].Text = "360000";
            if (video)
                SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<Button>().Single(b => b.Text == "&Aktualny").PerformClick();
            Assert.Equal(360000, Seconds()); CheckClock("", "", "");
            Assert.Equal(widths, inputs.Select(t => t.Width)); Assert.Equal(size, dialog.Size);
            dialog.AcceptButton!.PerformClick();
            Assert.Equal(360000, Target(dialog));
        }));
        owner.Close();
    });

    [Theory]
    [InlineData(false, 359999d)]
    [InlineData(true, 359999d)]
    [InlineData(false, 360001d)]
    [InlineData(true, 360001d)]
    [InlineData(true, 0d)]
    public void SecondsAndFrames_AcceptBoundaryAndBeyondClockWithoutClamping(bool video, double seconds) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner = new Form(); owner.Show();
        foreach (var source in new[] { "Seconds", "Frames", "Clock" })
        {
            if (source == "Clock" && seconds >= 360000) continue;
            using StandardDialog dialog = video ? new GoToVideoPositionDialog(25, 0) : new GoToSubtitleDialog(25);
            Assert.Equal(DialogResult.OK, Run(dialog, owner, () =>
            {
                var inputs = SubtitleDialogLayoutTests.Descendants(dialog.Page).OfType<TextBox>().ToArray();
                if (source == "Clock")
                {
                    inputs[2].Text = ((int)seconds / 3600).ToString("00", CultureInfo.InvariantCulture);
                    inputs[3].Text = ((int)seconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture);
                    inputs[4].Text = ((int)seconds % 60).ToString("00", CultureInfo.InvariantCulture);
                }
                else inputs[source == "Frames" ? 1 : 0].Text = (source == "Frames" ? seconds * 25 : seconds).ToString(CultureInfo.InvariantCulture);
                dialog.AcceptButton!.PerformClick();
                Assert.Equal(seconds, Target(dialog));
            }));
        }
        owner.Close();
    });

    private static void RejectOk(StandardDialog dialog)
    {
        var dismissed = false;
        Hook callback = (code, window, data) =>
        {
            var name = new System.Text.StringBuilder(32);
            GetClassName(window, name, name.Capacity);
            if (code == 5 && name.ToString() == "#32770") // HCBT_ACTIVATE, this test thread only.
            {
                dismissed = true;
                PostMessage(GetDlgItem(window, 1), 0x00F5, IntPtr.Zero, IntPtr.Zero); // BM_CLICK / IDOK after native activation.
            }
            return CallNextHookEx(IntPtr.Zero, code, window, data);
        };
        var hook = SetWindowsHookEx(5, callback, IntPtr.Zero, GetCurrentThreadId());
        Assert.NotEqual(IntPtr.Zero, hook);
        try { dialog.AcceptButton!.PerformClick(); }
        finally { UnhookWindowsHookEx(hook); GC.KeepAlive(callback); }
        Assert.True(dismissed, "Expected native validation MessageBox");
        Assert.True(dialog.Visible); Assert.Equal(DialogResult.None, dialog.DialogResult);
    }

    private static DialogResult Run(StandardDialog dialog, Form owner, Action ready)
    {
        Rectangle? first = null;
        var late = new List<Rectangle>();
        var shown = false; var painted = false; var posted = false; var measuring = true;
        Exception? error = null;
        dialog.VisibleChanged += (_, _) => { if (dialog.Visible && first is null) first = dialog.Bounds; };
        void Changed(object? sender, EventArgs e) { if (measuring && first is { } bounds && dialog.Visible && bounds != dialog.Bounds) late.Add(dialog.Bounds); }
        dialog.SizeChanged += Changed; dialog.LocationChanged += Changed;
        void Ready()
        {
            if (!shown || !painted || posted) return;
            posted = true;
            dialog.BeginInvoke((Action)(() =>
            {
                try
                {
                    measuring = false;
                    Assert.Equal(first, dialog.Bounds); Assert.Empty(late);
                    Assert.Same(owner, dialog.Owner); Assert.True(dialog.Modal);
                    ready();
                }
                catch (Exception e) { error = e; dialog.DialogResult = DialogResult.Cancel; }
            }));
        }
        dialog.Shown += (_, _) => { shown = true; Ready(); };
        dialog.Paint += (_, _) => { painted = true; Ready(); };
        var result = dialog.ShowDialog(owner);
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        Assert.True(posted); Assert.False(dialog.IsHandleCreated);
        return result;
    }
    private static Control Focused(Control parent) => SubtitleDialogLayoutTests.Descendants(parent).First(c => c.Focused);
    private static double Target(StandardDialog dialog) => dialog is GoToSubtitleDialog subtitle ? subtitle.TargetSeconds : ((GoToVideoPositionDialog)dialog).TargetSeconds;
    private static void Key(Control control, Keys key) => Assert.True((bool)typeof(Control).GetMethod("ProcessDialogKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [key])!);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    private delegate IntPtr Hook(int code, IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int id, Hook callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr window, IntPtr data);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
