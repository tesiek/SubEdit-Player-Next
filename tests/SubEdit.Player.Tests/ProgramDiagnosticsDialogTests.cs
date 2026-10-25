using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Diagnostics;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Diagnostics;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Diagnostics UI", DisableParallelization = true)]
public sealed class DiagnosticsUiCollection;

[Collection("Diagnostics UI")]
public class ProgramDiagnosticsDialogTests
{
    [Fact]
    public void Dialog_ShowsBeforeCaptureAndCopiesSelectionOrWholeCurrentReport()
    {
        RunSta(() =>
        {
            var captureCount = 0;
            var copied = new List<string>();
            var request = Request();
            var completion = new TaskCompletionSource<ProgramDiagnosticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var owner = new Form { Opacity = 0 };
            owner.Show();
            using var dialog = new ProgramDiagnosticsDialog(() => { captureCount++; return request; }, _ => completion.Task, copied.Add)
                { Opacity = 0 };
            dialog.Show(owner);
            Assert.True(dialog.Visible);
            Assert.True(owner.Enabled);
            Assert.False(dialog.Modal);
            Assert.Equal(0, captureCount); // Show returned before the posted capture callback.
            PumpUntil(() => captureCount == 1);
            var text = Assert.Single(dialog.Controls.OfType<TextBox>());
            Assert.True(text.ReadOnly);
            Assert.True(text.Multiline);
            Assert.Equal(ScrollBars.Vertical, text.ScrollBars);
            Assert.Contains("[AKTUALNY FILM]", dialog.ReportText);
            Assert.Contains("Pobieranie danych systemowych...", dialog.ReportText);
            text.Select(0, 9);
            Assert.True(dialog.HandleTextShortcut(Keys.Control | Keys.C));
            Assert.Equal(text.SelectedText, copied[^1]);
            dialog.CopyAll();
            Assert.Equal(dialog.ReportText, copied[^1]);
            Assert.True(dialog.HandleTextShortcut(Keys.Control | Keys.A));
            Assert.Equal(text.TextLength, text.SelectionLength);
            Assert.False(dialog.HandleTextShortcut(Keys.F9));
            var context = text.ContextMenuStrip!;
            var selectionItem = Assert.Single(context.Items.OfType<ToolStripMenuItem>());
            Assert.Equal("Kopiuj zaznaczenie", selectionItem.Text);
            RaiseOpening(context);
            Assert.True(selectionItem.Enabled);
            text.Select(0, 0);
            RaiseOpening(context);
            Assert.False(selectionItem.Enabled);
            completion.SetResult(request.Snapshot with { System = SystemInfo() });
            PumpUntil(() => !dialog.ReportText.Contains("Pobieranie danych systemowych..."));
            dialog.CopyAll();
            Assert.Equal(dialog.ReportText, copied[^1]);
            Assert.Contains("CPU: Example CPU", copied[^1]);
            Assert.Equal(1, captureCount);
            dialog.Close();
            owner.Close();
        });
    }

    [Fact]
    public void ClosedDialog_DropsLateCompletionAndDoesNotBlockOwner()
    {
        RunSta(() =>
        {
            var completion = new TaskCompletionSource<ProgramDiagnosticsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var owner = new Form { Opacity = 0 };
            owner.Show();
            using var dialog = new ProgramDiagnosticsDialog(Request, _ => completion.Task) { Opacity = 0 };
            dialog.Show(owner);
            PumpUntil(() => dialog.LoadingTask is not null);
            var task = dialog.LoadingTask!;
            dialog.Close();
            Assert.True(dialog.IsDisposed);
            Assert.True(owner.Enabled);
            completion.SetResult(Request().Snapshot with { System = SystemInfo() });
            PumpUntil(() => task.IsCompleted);
            Assert.True(task.IsCompletedSuccessfully);
            Application.DoEvents();
            owner.Close();
        });
    }

    [Theory]
    [InlineData(PlaybackState.Playing)]
    [InlineData(PlaybackState.Paused)]
    [InlineData(PlaybackState.Stopped)]
    public void HelpMenu_ReusesNonmodalWindowAndKeepsCapturedPlaybackState(PlaybackState state)
    {
        RunSta(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SubEditDiagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var engine = DispatchProxy.Create<IPlaybackEngine, DiagnosticsPlaybackProxy>();
                var probe = (DiagnosticsPlaybackProxy)(object)engine;
                using var form = new Form1(new AppSettings(), new PortableSettingsStore(directory), new TestFactory(engine)) { Opacity = 0 };
                var cacheField = typeof(Form1).GetField("_diagnosticsSystemCache", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.Null(cacheField.GetValue(form));
                form.Show();
                Assert.Null(cacheField.GetValue(form)); // No hardware work at startup.
                var reads = 0;
                cacheField.SetValue(form, new SystemDiagnosticsCache(() => { Interlocked.Increment(ref reads); return SystemInfo(); }));
                probe.State = state;
                probe.Open = true;
                var menu = Assert.Single(form.Controls.OfType<MenuStrip>());
                var help = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Pomoc");
                var entry = Assert.Single(help.DropDownItems.OfType<ToolStripMenuItem>(), item => item.Text == "Diagnostyka programu");
                entry.PerformClick();
                var first = Assert.Single(form.OwnedForms.OfType<ProgramDiagnosticsDialog>());
                PumpUntil(() => first.ReportText.Contains("CPU: Example CPU"));
                Assert.False(first.Modal);
                Assert.True(form.Enabled);
                Assert.Equal(state, engine.State);
                Assert.Equal(0, probe.PauseCalls);
                var initial = first.ReportText;
                probe.Open = false; // Later Close/Open must not change the existing snapshot.
                entry.PerformClick();
                Assert.Same(first, Assert.Single(form.OwnedForms.OfType<ProgramDiagnosticsDialog>()));
                Application.DoEvents();
                Assert.Equal(initial, first.ReportText);
                first.Close();
                entry.PerformClick();
                var second = Assert.Single(form.OwnedForms.OfType<ProgramDiagnosticsDialog>());
                Assert.NotSame(first, second);
                PumpUntil(() => second.ReportText.Contains("CPU: Example CPU"));
                Assert.Contains("Brak otwartego pliku.", second.ReportText);
                Assert.Equal(1, reads);
                second.Close();
                form.Close();
            }
            finally { Directory.Delete(directory, recursive: true); }
        });
    }

    private static DiagnosticsRequest Request() => new(new(new("SubEdit", "1.0.0", "261018", "abc", "CLEAN", "X64", ".NET 10"),
        [], new(false), DateTimeOffset.Now, "Europe/Warsaw"), null);

    internal static DiagnosticSystemInfo SystemInfo() => new("Windows 11", "10.0", "26100", "Example CPU", 17179869184,
        [new("Example GPU", "1.2.3")]);

    private static void RaiseOpening(ContextMenuStrip context) => typeof(ToolStripDropDown)
        .GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(context, [new CancelEventArgs()]);

    private static void PumpUntil(Func<bool> condition)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(15)) { Application.DoEvents(); Thread.Sleep(1); }
        Assert.True(condition(), "Diagnostics UI completion timed out.");
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Diagnostics STA test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public class DiagnosticsPlaybackProxy : DispatchProxy
    {
        public PlaybackState State;
        public bool Open;
        public int PauseCalls;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            "get_State" => State,
            "get_IsMediaOpen" => Open,
            "get_AreTracksReady" => Open,
            "get_MediaInfo" => Open ? new PlaybackMediaInfo(true, 1280, 640, 1280, 640, "H.264", "", true) : null,
            "get_DurationMilliseconds" => 60000L,
            "get_PositionMilliseconds" or "get_SessionId" => 0L,
            "get_AudioTracks" => Array.Empty<PlaybackAudioTrack>(),
            "get_VideoTracks" => Array.Empty<PlaybackVideoTrack>(),
            "get_EmbeddedSubtitleTracks" => Array.Empty<PlaybackSubtitleTrack>(),
            "get_Chapters" => Array.Empty<PlaybackChapter>(),
            "Pause" => CountPause(),
            _ => method.ReturnType == typeof(void) || !method.ReturnType.IsValueType ? null : Activator.CreateInstance(method.ReturnType)
        };
        private object? CountPause() { PauseCalls++; return null; }
    }

    private sealed class TestFactory(IPlaybackEngine engine) : IPlaybackEngineFactory
    {
        public IPlaybackEngine CreateEngine(Control? dispatcher, int initialVolume, Func<VideoDecodingMode> mode) => engine;
        public IVideoHost CreateVideoHost(Panel container, IPlaybackEngine playback, ContextMenuStrip context) => new TestHost(container);
    }

    private sealed class TestHost(Control control) : IVideoHost
    {
        public event EventHandler? VideoResized { add { } remove { } }
        public Control HostControl => control;
        public void ApplyVerticalOffset(int value) { }
        public void ApplyHorizontalOffset(int value) { }
        public void ApplyZoom(int value) { }
        public void ShowContextMenuCentered() { }
        public void Focus() { }
        public void RegisterFileDrop(DragEventHandler enter, DragEventHandler drop) { }
        public void Dispose() { }
    }
}
