using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Window title UI", DisableParallelization = true)]
public sealed class WindowTitleUiCollection;

[Collection("Window title UI")]
public class WindowTitleTests
{
    [Fact]
    public void NoMedia_UsesApplicationName()
    {
        WithForm((form, _, _) =>
        {
            Assert.Equal("SubEdit-Player Next", form.Text);
            Invoke(form, "CloseVideoFile");
            Assert.Equal("SubEdit-Player Next", form.Text);
        });
    }

    [Fact]
    public void SuccessfulOpen_ChangesTitleAfterSuccessAndKeepsItDuringPlaybackCommands()
    {
        WithForm((form, playback, directory) =>
        {
            var titlesDuringOpen = new List<string>();
            playback.BeforeOpen = () => titlesDuringOpen.Add(form.Text);
            var first = Path.Combine(directory, "Film pierwszy [żółć].mkv");
            Assert.True(Load(form, first));
            Assert.Equal(Path.GetFileName(first), form.Text);
            Assert.DoesNotContain(directory, form.Text);
            foreach (var command in new[] { "Pause", "Play", "StopPlayback" })
            {
                Invoke(form, command);
                Assert.Equal(Path.GetFileName(first), form.Text);
            }
            var second = Path.Combine(directory, "Film drugi.mp4");
            Assert.True(Load(form, second));
            Assert.Equal(Path.GetFileName(second), form.Text);
            Assert.Equal(new[] { "SubEdit-Player Next", Path.GetFileName(first) }, titlesDuringOpen);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedOpen_ReturnsToNoMediaTitleWithoutDisplayingFailedFilename(bool hadVideo)
    {
        WithForm((form, playback, directory) =>
        {
            if (hadVideo) Assert.True(Load(form, Path.Combine(directory, "Valid.mkv")));
            var previousTitle = form.Text;
            string? titleDuringOpen = null;
            var titles = new List<string>();
            form.TextChanged += (_, _) => titles.Add(form.Text);
            playback.BeforeOpen = () => titleDuringOpen = form.Text;
            playback.FailOpen = true;
            Assert.False(Load(form, Path.Combine(directory, "Broken.mkv")));
            Assert.Equal(previousTitle, titleDuringOpen);
            Assert.False(playback.IsOpen);
            Assert.Equal("SubEdit-Player Next", form.Text);
            Assert.DoesNotContain("Broken.mkv", titles);
        });
    }

    [Fact]
    public void Close_ResetsTitle()
    {
        WithForm((form, playback, directory) =>
        {
            Assert.True(Load(form, Path.Combine(directory, "Movie.mkv")));
            Invoke(form, "CloseVideoFile");
            Assert.False(playback.IsOpen);
            Assert.Equal("SubEdit-Player Next", form.Text);
        });
    }

    [Fact]
    public void Close_KeepsLastVideoPathAndPlayReopensIt()
    {
        WithForm((form, playback, directory) =>
        {
            var fileStatus = (ToolStripStatusLabel)typeof(Form1)
                .GetField("_fileStatus", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
            Assert.True(string.IsNullOrEmpty(fileStatus.ToolTipText));

            foreach (var name in new[] { "First.mkv", "Second.mp4" })
            {
                var path = Path.Combine(directory, name);
                File.WriteAllBytes(path, []);
                Assert.True(Load(form, path));
                Invoke(form, "UpdateStatusBar", 5_823_000L, 7_246_234L);
                Invoke(form, "CloseVideoFile");
                Assert.False(playback.IsOpen);
                Assert.Equal(path, fileStatus.ToolTipText);
                Assert.Equal("{0}{0}", fileStatus.Owner!.Items[0].Text);
                Assert.Equal("[0][0]", fileStatus.Owner.Items[1].Text);

                Invoke(form, "Play");
                Assert.True(playback.IsOpen);
                Assert.Equal(name, form.Text);
                Assert.Equal(path, fileStatus.ToolTipText);
            }
        });
    }

    [Fact]
    public void LoadingSubtitleAlone_DoesNotChangeTitleWithOrWithoutVideo()
    {
        WithForm((form, _, directory) =>
        {
            var subtitle = Path.Combine(directory, "Separate.txt");
            File.WriteAllText(subtitle, "{25}{50}Przykładowy napis");
            Assert.True((bool)Invoke(form, "LoadSubtitleFile", subtitle, false, false)!);
            Assert.Equal("SubEdit-Player Next", form.Text);
            Assert.True(Load(form, Path.Combine(directory, "Movie.mkv")));
            Assert.True((bool)Invoke(form, "LoadSubtitleFile", subtitle, false, false)!);
            Assert.Equal("Movie.mkv", form.Text);
        });
    }

    [Fact]
    public void StartupArgument_UsesSameSuccessfulOpenTitle()
    {
        WithForm((form, playback, _) =>
        {
            Invoke(form, "OpenStartupVideo");
            Assert.True(playback.IsOpen);
            Assert.Equal("Startup.mkv", form.Text);
        });
    }

    [Fact]
    public void StatusTimingCounters_FitLongMovieAndKeepWidthsDuringPlayback()
    {
        WithForm((form, playback, directory) =>
        {
            Assert.True(Load(form, Path.Combine(directory, "Long.mkv")));
            var microDvd = GetCounter("_mDVDStatus");
            var mpl2 = GetCounter("_mpl2Status");
            _ = microDvd.Owner!.Handle;

            const long duration = 7_246_234;
            Invoke(form, "UpdateStatusBar", 94_000L, duration);
            var longWidths = (microDvd.Width, mpl2.Width);

            foreach (var position in new[] { 5_823_000L, duration })
            {
                Invoke(form, "UpdateStatusBar", position, duration);
                Assert.Equal(longWidths, (microDvd.Width, mpl2.Width));
                Assert.True(microDvd.Width >= microDvd.GetPreferredSize(System.Drawing.Size.Empty).Width, microDvd.Text);
                Assert.True(mpl2.Width >= mpl2.GetPreferredSize(System.Drawing.Size.Empty).Width, mpl2.Text);
            }

            Assert.True(Load(form, Path.Combine(directory, "Short.mkv")));
            Invoke(form, "UpdateStatusBar", 94_000L, 120_000L);
            Assert.True(microDvd.Width < longWidths.Item1);
            Assert.True(mpl2.Width < longWidths.Item2);

            ToolStripStatusLabel GetCounter(string name) => (ToolStripStatusLabel)typeof(Form1)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        });
    }

    private static bool Load(Form1 form, string path) => (bool)Invoke(form, "LoadVideoFile", path, null, null)!;

    private static object? Invoke(Form1 form, string name, params object?[] args) =>
        typeof(Form1).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args);

    private static void WithForm(Action<Form1, TitlePlayback, string> action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "SubEditWindowTitle-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var startupPath = Path.Combine(directory, "Startup.mkv");
                File.WriteAllBytes(startupPath, []); // The fake backend opens it without native media processing.
                var engine = DispatchProxy.Create<IPlaybackEngine, TitlePlayback>();
                using var form = new Form1(new AppSettings(), new PortableSettingsStore(directory), new TestFactory(engine), startupPath);
                _ = form.Handle;
                try { action(form, (TitlePlayback)(object)engine, directory); }
                finally { form.Close(); }
            }
            catch (Exception exception) { failure = exception; }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Window title STA test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public class TitlePlayback : DispatchProxy
    {
        public bool IsOpen;
        public bool FailOpen;
        public Action? BeforeOpen;
        private PlaybackState _state = PlaybackState.Stopped;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "Open":
                    BeforeOpen?.Invoke();
                    IsOpen = !FailOpen;
                    if (FailOpen) throw new InvalidOperationException("Simulated failed Open");
                    return null;
                case "Close": IsOpen = false; _state = PlaybackState.Stopped; return null;
                case "Play": _state = PlaybackState.Playing; return null;
                case "Pause": _state = PlaybackState.Paused; return null;
                case "Stop": _state = PlaybackState.Stopped; return null;
                case "get_IsMediaOpen": return IsOpen;
                case "get_State": return _state;
                case "get_DurationMilliseconds": return 60000L;
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
