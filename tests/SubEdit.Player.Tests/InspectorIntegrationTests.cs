using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Diagnostics UI")]
public class InspectorIntegrationTests(ITestOutputHelper output)
{
    private static DiagnosticsRequest Request() => new(new(new("SubEdit", "1.0.0", "261103", "commit", "DIRTY", "X64", ".NET 10"),
        [], new(false), DateTimeOffset.Now, "Europe/Warsaw"), null);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ui2_SessionPlacementFitsBeforeVisibilityAndRemembersOnlyUserMoves(bool diagnostics) => RunSta(() =>
    {
        var previous = SetThreadDpiAwarenessContext((IntPtr)(-4));
        try
        {
            using var owner = new Form { StartPosition = FormStartPosition.Manual };
            var screens = Screen.AllScreens.OrderByDescending(screen => screen.Primary).ToArray();
            var monitorDpis = new Dictionary<string, uint>();
            var position = new InspectorDialog.SessionPosition();
            var target = screens[^1];
            var moved = target.WorkingArea.Location + new Size(40, 40);
            foreach (var screen in screens.Concat(screens.Reverse()))
            {
                owner.Bounds = new Rectangle(screen.WorkingArea.Location + new Size(100, 80),
                    new Size(Math.Min(1100, screen.WorkingArea.Width - 110), Math.Min(850, screen.WorkingArea.Height - 90)));
                if (!owner.Visible) owner.Show();
                Pump();
                monitorDpis[screen.DeviceName] = GetDpiForWindow(owner.Handle);
                CheckOpening(screen, savedLocation: null, dialog =>
                {
                    dialog.Location += new Size(8, 8);
                    Assert.Null(position.LastMove);
                });
                Assert.Null(position.LastMove);
            }
            output.WriteLine($"Native owner and dialog DPI on each display: {string.Join(", ", monitorDpis.Values)}");

            CheckOpening(Screen.FromControl(owner), savedLocation: null, dialog =>
            {
                foreach (var screen in screens.Concat(screens.Reverse()).Append(target))
                {
                    var destination = screen.WorkingArea.Location + new Size(40, 40);
                    MoveThroughNativeLoop(dialog, destination);
                    Pump();
                    Assert.Equal(screen.DeviceName, Screen.FromControl(dialog).DeviceName);
                    Assert.Equal(monitorDpis[screen.DeviceName], GetDpiForWindow(dialog.Handle));
                    Assert.Equal((screen.DeviceName, destination - (Size)screen.Bounds.Location), position.LastMove);
                }
            });
            CheckOpening(target, moved);

            position.LastMove = (target.DeviceName, new Point(target.Bounds.Width - 5, target.Bounds.Height - 5));
            CheckOpening(target, target.Bounds.Location + new Size(target.Bounds.Width - 5, target.Bounds.Height - 5));
            position.LastMove = ("disconnected-display", Point.Empty);
            CheckOpening(Screen.FromControl(owner), savedLocation: null);

            void CheckOpening(Screen expectedScreen, Point? savedLocation, Action<InspectorDialog>? check = null)
            {
                var completion = new TaskCompletionSource<ProgramDiagnosticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                using InspectorDialog dialog = diagnostics
                    ? new ProgramDiagnosticsDialog(Request, _ => completion.Task)
                    : new VideoPropertiesDialog(VideoFilePropertiesTests.LargeSnapshot());
                dialog.PrepareForShow(owner, position);
                Rectangle? firstVisible = null;
                Exception? failure = null;
                dialog.VisibleChanged += (_, _) => { if (dialog.Visible) firstVisible ??= dialog.Bounds; };
                dialog.Shown += (_, _) => dialog.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        Assert.Equal(firstVisible, dialog.Bounds);
                        Assert.Equal(expectedScreen.DeviceName, Screen.FromControl(dialog).DeviceName);
                        var dpi = monitorDpis[expectedScreen.DeviceName];
                        Assert.Equal(dpi, GetDpiForWindow(dialog.Handle));
                        Assert.Equal(dpi, (uint)dialog.DeviceDpi);
                        Assert.Equal(dpi, (uint)dialog.LayoutView.DeviceDpi);
                        var area = expectedScreen.WorkingArea;
                        var desired = savedLocation ?? new Point(owner.Left + (owner.Width - dialog.Width) / 2,
                            owner.Top + (owner.Height - dialog.Height) / 2);
                        Assert.Equal(new Point(Math.Clamp(desired.X, area.Left, Math.Max(area.Left, area.Right - dialog.Width)),
                            Math.Clamp(desired.Y, area.Top, Math.Max(area.Top, area.Bottom - dialog.Height))), dialog.Location);
                        if (dialog is ProgramDiagnosticsDialog diagnostic)
                        {
                            var bounds = diagnostic.Bounds;
                            var lastMove = position.LastMove;
                            completion.SetResult(Request().Snapshot with { System = ProgramDiagnosticsDialogTests.SystemInfo() });
                            PumpUntil(() => diagnostic.ReportText.Contains("Example CPU"));
                            Assert.Equal(bounds, diagnostic.Bounds);
                            Assert.Equal(lastMove, position.LastMove);
                        }
                        check?.Invoke(dialog);
                    }
                    catch (Exception e) { failure = e; }
                    finally { dialog.Close(); }
                }));
                if (diagnostics)
                {
                    dialog.Show(owner);
                    PumpUntil(() => dialog.IsDisposed);
                }
                else dialog.ShowDialog(owner);
                if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
        finally { SetThreadDpiAwarenessContext(previous); }
    });

    private static void MoveThroughNativeLoop(Form form, Point destination)
    {
        var proposed = Marshal.AllocHGlobal(4 * sizeof(int));
        try
        {
            Marshal.Copy(new[] { destination.X, destination.Y, destination.X + form.Width, destination.Y + form.Height }, 0, proposed, 4);
            SendMessage(form.Handle, 0x0231, IntPtr.Zero, IntPtr.Zero); // WM_ENTERSIZEMOVE
            SendMessage(form.Handle, 0x0216, IntPtr.Zero, proposed); // WM_MOVING
            form.Location = destination;
            SendMessage(form.Handle, 0x0232, IntPtr.Zero, IntPtr.Zero); // WM_EXITSIZEMOVE
        }
        finally { Marshal.FreeHGlobal(proposed); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Diagnostics_FailurePreservesSnapshotOrShowsUnavailable(bool captureFails) => RunSta(() =>
    {
        var request = Request();
        using var dialog = new ProgramDiagnosticsDialog(
            () => captureFails ? throw new InvalidOperationException("capture") : request,
            _ => Task.FromException<ProgramDiagnosticsSnapshot>(new InvalidOperationException("complete")));
        dialog.Show();
        var expected = captureFails ? "Dane diagnostyczne są niedostępne." : ProgramDiagnosticsFormatter.Format(request.Snapshot);
        PumpUntil(() => dialog.LoadingTask?.IsCompleted == true && !dialog.ReportContent.Report.Text.Contains("Pobieranie"));
        Assert.Equal(expected, dialog.ReportText);
        if (!captureFails) Assert.Contains("SubEdit", dialog.ReportContent.Report.Text);
        Assert.DoesNotContain("capture", dialog.ReportText);
        Assert.DoesNotContain("complete", dialog.ReportText);
        Assert.True(dialog.LoadingTask!.IsCompletedSuccessfully);
        dialog.Close();
    });

    [Theory]
    [InlineData(false, Keys.Enter)]
    [InlineData(false, Keys.Escape)]
    [InlineData(true, Keys.Enter)]
    [InlineData(true, Keys.Escape)]
    public void Ui2_EnterEscapeCloseModalDialogAndRestoreOwner(bool diagnostics, Keys key) => RunSta(() =>
    {
        using var owner = new Form(); owner.Show();
        using InspectorDialog dialog = diagnostics
            ? new ProgramDiagnosticsDialog(Request, r => Task.FromResult(r.Snapshot))
            : new VideoPropertiesDialog(VideoFilePropertiesTests.LargeSnapshot());
        dialog.Shown += (_, _) => dialog.BeginInvoke((Action)(() =>
        {
            var message = new Message();
            var handled = (bool)typeof(InspectorDialog).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, [message, key])!;
            Assert.True(handled);
        }));
        Assert.Equal(DialogResult.OK, dialog.ShowDialog(owner));
        Assert.True(owner.Enabled);
        owner.Close();
    });

    [Fact]
    public void Diagnostics_CloseCancelsOnlyDialogWaitAndDropsLateResult() => RunSta(() =>
    {
        var completion = new TaskCompletionSource<ProgramDiagnosticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var dialog = new ProgramDiagnosticsDialog(Request, _ => completion.Task);
        dialog.Show(); PumpUntil(() => dialog.LoadingTask is not null);
        var task = dialog.LoadingTask!; var handle = dialog.ReportContent.Report.Handle;
        var menu = dialog.ReportContent.Report.ContextMenuStrip!;
        dialog.Close(); PumpUntil(() => task.IsCompleted);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(completion.Task.IsCompleted); // The shared collector is not cancelled.
        Assert.True(menu.IsDisposed);
        Assert.False(IsWindow(handle));
        completion.SetResult(Request().Snapshot);
        Pump();
    });

    [Fact]
    public void Diagnostics_CancelledCloseKeepsWaitAndOwnerUntilAcceptedClose() => RunSta(() =>
    {
        var completion = new TaskCompletionSource<ProgramDiagnosticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var owner = new Form(); owner.Show();
        using var dialog = new ProgramDiagnosticsDialog(Request, _ => completion.Task);
        dialog.Show(owner); PumpUntil(() => dialog.LoadingTask is not null);
        var hwnd = dialog.Handle; var ownerAtDestroy = false; var closed = 0;
        dialog.HandleDestroyed += (_, _) => ownerAtDestroy = dialog.Owner == owner;
        dialog.FormClosed += (_, _) => closed++;
        FormClosingEventHandler cancel = (_, e) => e.Cancel = true;
        dialog.FormClosing += cancel; dialog.Close();
        Assert.True(IsWindow(hwnd)); Assert.True(dialog.Visible); Assert.False(dialog.LoadingTask!.IsCompleted);
        completion.SetResult(Request().Snapshot with { System = ProgramDiagnosticsDialogTests.SystemInfo() });
        PumpUntil(() => dialog.ReportContent.Report.Text.Contains("Example CPU"));
        dialog.FormClosing -= cancel; dialog.Close();
        Assert.Equal(1, closed); Assert.True(ownerAtDestroy); Assert.False(IsWindow(hwnd)); Assert.True(owner.Enabled);
        owner.Close();
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ui2_FontRoundTripAndHwndRecreationPreserveDocumentAndZoom(bool diagnostics) => RunSta(() =>
    {
        using InspectorDialog dialog = diagnostics
            ? new ProgramDiagnosticsDialog(Request, r => Task.FromResult(r.Snapshot))
            : new VideoPropertiesDialog(VideoFilePropertiesTests.LargeSnapshot());
        dialog.Show(); PumpUntil(() => dialog.ReportContent.Report.TextLength > 50 && (!diagnostics || ((ProgramDiagnosticsDialog)dialog).LoadingTask?.IsCompleted == true)); Pump();
        var report = dialog.ReportContent.Report;
        var owner = (UiFontOwner)typeof(InspectorDialog).GetField("_fonts", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog)!;
        owner.SetSize(9); Pump(); var size = dialog.Size; var rtf = report.Rtf; var text = report.Text;
        foreach (var points in new[] { 12f, 15f, 18f, 24f, 9f })
        {
            owner.SetSize(points); Pump();
            foreach (var b in dialog.LayoutView.Actions.Buttons.Values)
                Assert.True(b.Height >= b.GetPreferredSize(new System.Drawing.Size(b.Width, 0)).Height);
        }
        Assert.Equal(size, dialog.Size); Assert.Equal(rtf, report.Rtf); Assert.Equal(text, report.Text);
        var created = 0; var destroyed = 0;
        report.HandleCreated += (_, _) => created++; report.HandleDestroyed += (_, _) => destroyed++;
        typeof(Control).GetMethod("RecreateHandle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(report, null);
        Pump(); report.Refresh();
        Assert.Equal(1, created); Assert.Equal(1, destroyed);
        Assert.Equal(rtf, report.Rtf); Assert.Equal(text, report.Text);
        Assert.InRange(Math.Abs(report.ZoomFactor - (float)GetDpiForWindow(report.Handle) / GetDpiForSystem()), 0, .001f);
        foreach (var zoom in new[] { .8f, 1.25f, 1f }) { report.ZoomFactor = zoom; Assert.Equal(rtf, report.Rtf); }
        report.GetType().GetMethod("ApplyDpiZoom", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(report, [false]);
        Assert.Equal(size, dialog.Size);
        dialog.Close();
    });

    [Theory]
    [InlineData(96, 96)]
    [InlineData(96, 120)]
    [InlineData(120, 96)]
    [InlineData(120, 120)]
    public void ProductionZoomPolicy_MatchesAccepted2D8(int system, int window)
    {
        var type = typeof(InspectorContentLayout).Assembly.GetType("SubEdit.Player.UI.DesignSystem.InspectorZoomState")!;
        var state = Activator.CreateInstance(type, true)!;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        float Zoom(int dpi, bool notification) => (float)type.GetMethod("Update", flags)!.Invoke(state, [(uint)dpi, (uint)system, notification])!;
        Assert.Equal((float)window / system, Zoom(window, false));
        Assert.Equal((float)window / system, Zoom(window, true));
        foreach (var dpi in new[] { window == 96 ? 120 : 96, window, window == 96 ? 120 : 96, window })
        { Assert.Equal(1, Zoom(dpi, true)); Assert.Equal(1, Zoom(dpi, false)); }
        type.GetMethod("Reset", flags)!.Invoke(state, null);
        Assert.Equal((float)window / system, Zoom(window, false));
    }

    [Fact]
    public void RepeatedUi2Close_ReleasesHwndAndKeepsGuiResourcesBounded() => RunSta(() =>
    {
        void Cycle()
        {
            using InspectorDialog d = new VideoPropertiesDialog(VideoFilePropertiesTests.LargeSnapshot());
            d.Show(); Pump(); var hwnd = d.Handle; d.Close(); Assert.False(IsWindow(hwnd));
            using var diagnostic = new ProgramDiagnosticsDialog(Request, r => Task.FromResult(r.Snapshot));
            diagnostic.Show(); PumpUntil(() => diagnostic.LoadingTask?.IsCompleted == true); diagnostic.Close(); Pump();
        }
        for (var i = 0; i < 3; i++) Cycle();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Pump();
        using var process = Process.GetCurrentProcess();
        var gdi = GetGuiResources(process.Handle, 0); var user = GetGuiResources(process.Handle, 1);
        for (var i = 0; i < 12; i++) Cycle();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Pump();
        Assert.InRange(GetGuiResources(process.Handle, 0), 0, gdi + 3);
        Assert.InRange(GetGuiResources(process.Handle, 1), 0, user + 3);
    });

    private static void Pump() { for (var i = 0; i < 6; i++) Application.DoEvents(); }
    private static void PumpUntil(Func<bool> done)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.Elapsed < TimeSpan.FromSeconds(15)) { Application.DoEvents(); Thread.Sleep(1); }
        Assert.True(done(), "UI completion timeout");
    }
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "UI test timeout");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetGuiResources(IntPtr process, int flag);
}
