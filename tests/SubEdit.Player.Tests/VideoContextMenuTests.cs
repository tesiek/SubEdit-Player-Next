using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Video context menu UI", DisableParallelization = true)]
public sealed class VideoContextMenuUiCollection;

[Collection("Video context menu UI")]
public class VideoContextMenuTests
{
    [Fact]
    public void Menu_PreservesExistingItemsAndAppendsSeparatorAndClose()
    {
        WithForm((_, factory, _, _) =>
        {
            Assert.Equal(new[]
            {
                "Otwórz plik wideo", "Otwórz plik napisów", "-", "Pauza / Odtwarzanie", "Stop", "-",
                "Przesunięcie obrazu", "Powiększenie", "Resetuj transformacje", "Prędkość odtwarzania", "-",
                "Tryb pełnoekranowy", "-", "Opcje", "-", "Zamknij"
            }, factory.Menu.Items.Cast<ToolStripItem>().Select(item => item is ToolStripSeparator ? "-" : item.Text));
            Assert.IsType<ToolStripSeparator>(factory.Menu.Items[^2]);
            Assert.Equal("Zamknij", CloseItem(factory).Text);
        });
    }

    [Fact]
    public void CloseCommand_RunsStandardFormClosingSettingsSaveAndCleanup()
    {
        WithForm((form, factory, playback, directory) =>
        {
            var closing = 0;
            form.FormClosing += (_, e) => { closing++; Assert.Equal(CloseReason.UserClosing, e.CloseReason); };
            CloseItem(factory).PerformClick();
            Assert.Equal(1, closing);
            Assert.True(form.IsDisposed);
            Assert.True(playback.Disposed);
            Assert.True(factory.HostDisposed);
            Assert.True(File.Exists(Path.Combine(directory, "SubEdit.Player.Next.json")));
            Assert.Equal(27, new PortableSettingsStore(directory).Load().Volume);
        });
    }

    [Fact]
    public void CloseCommand_AllowsStandardClosingToBeCancelled()
    {
        WithForm((form, factory, playback, directory) =>
        {
            form.CancelClose = true;
            CloseItem(factory).PerformClick();
            Assert.Equal(1, form.CloseRequests);
            Assert.False(form.IsDisposed);
            Assert.False(playback.Disposed);
            Assert.False(File.Exists(Path.Combine(directory, "SubEdit.Player.Next.json")));
            form.CancelClose = false;
            CloseItem(factory).PerformClick();
            Assert.Equal(2, form.CloseRequests);
            Assert.True(form.IsDisposed);
            Assert.True(playback.Disposed);
        });
    }

    private static ToolStripMenuItem CloseItem(TestFactory factory) => Assert.IsType<ToolStripMenuItem>(factory.Menu.Items[^1]);

    private static void WithForm(Action<TestForm, TestFactory, TestPlayback, string> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SubEditVideoMenu-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var engine = DispatchProxy.Create<IPlaybackEngine, TestPlayback>();
                var factory = new TestFactory(engine);
                using var form = new TestForm(new AppSettings { Volume = 27 }, new PortableSettingsStore(directory), factory);
                _ = form.Handle;
                try { action(form, factory, (TestPlayback)(object)engine, directory); }
                finally
                {
                    form.CancelClose = false;
                    if (!form.IsDisposed) form.Close();
                    factory.Menu.Dispose();
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Video menu STA test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class TestForm(AppSettings settings, PortableSettingsStore store, TestFactory factory)
        : Form1(settings, store, factory)
    {
        internal bool CancelClose;
        internal int CloseRequests;
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CloseRequests++;
            if (CancelClose) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }
    }

    public class TestPlayback : DispatchProxy
    {
        public bool Disposed;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "Dispose": Disposed = true; return null;
                case "get_AudioTracks": return Array.Empty<PlaybackAudioTrack>();
                case "get_VideoTracks": return Array.Empty<PlaybackVideoTrack>();
                case "get_EmbeddedSubtitleTracks": return Array.Empty<PlaybackSubtitleTrack>();
                case "get_Chapters": return Array.Empty<PlaybackChapter>();
                default: return method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
                    ? null : Activator.CreateInstance(method.ReturnType);
            }
        }
    }

    private sealed class TestFactory(IPlaybackEngine engine) : IPlaybackEngineFactory
    {
        internal ContextMenuStrip Menu = null!;
        internal bool HostDisposed;
        public IPlaybackEngine CreateEngine(Control? dispatcher, int initialVolume, Func<VideoDecodingMode> mode) => engine;
        public IVideoHost CreateVideoHost(Panel container, IPlaybackEngine playback, ContextMenuStrip context)
        {
            Menu = context;
            return new TestHost(container, () => HostDisposed = true);
        }
    }

    private sealed class TestHost(Control control, Action dispose) : IVideoHost
    {
        public event EventHandler? VideoResized { add { } remove { } }
        public Control HostControl => control;
        public void ApplyVerticalOffset(int value) { }
        public void ApplyHorizontalOffset(int value) { }
        public void ApplyZoom(int value) { }
        public void ShowContextMenuCentered() { }
        public void Focus() { }
        public void RegisterFileDrop(DragEventHandler enter, DragEventHandler drop) { }
        public void Dispose() => dispose();
    }
}
