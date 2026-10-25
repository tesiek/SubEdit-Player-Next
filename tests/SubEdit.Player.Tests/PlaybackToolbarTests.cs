using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Playback toolbar UI", DisableParallelization = true)]
public sealed class PlaybackToolbarUiCollection;

[Collection("Playback toolbar UI")]
public class PlaybackToolbarTests
{
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void EverySymbolRendersNormalActiveAndDisabledAtSupportedDpiWithoutClipping(int dpi)
    {
        OnSta(() =>
        {
            foreach (var command in Enum.GetValues<PlaybackToolbarCommand>())
            {
                using var button = new PlaybackToolbarButton(command, "tooltip");
                button.ApplyDpi(dpi);
                Assert.Equal(PlaybackToolbarIcons.ButtonSize(command, dpi), button.Size);
                foreach (var state in new[] { (false, true), (true, true), (true, false) })
                {
                    button.Checked = state.Item1;
                    button.Enabled = state.Item2;
                    using var bitmap = Render(button);
                    Assert.Contains(Pixels(bitmap), c => c.A > 0);
                    if (!state.Item1)
                    {
                        // The normal symbol, including its dropdown arrow, fits inside the button.
                        for (var x = 0; x < bitmap.Width; x++)
                        {
                            Assert.Equal(0, bitmap.GetPixel(x, 0).A);
                            Assert.Equal(0, bitmap.GetPixel(x, bitmap.Height - 1).A);
                        }
                        for (var y = 0; y < bitmap.Height; y++)
                        {
                            Assert.Equal(0, bitmap.GetPixel(0, y).A);
                            Assert.Equal(0, bitmap.GetPixel(bitmap.Width - 1, y).A);
                        }
                    }
                }
                button.ApplyDpi(96);
                Assert.Equal(PlaybackToolbarIcons.ButtonSize(command, 96), button.Size);
            }
        });
    }

    [Fact]
    public void PlayPauseStopUseBlueActiveSymbolsAndDisabledIsDistinct()
    {
        OnSta(() =>
        {
            foreach (var command in new[] { PlaybackToolbarCommand.Play, PlaybackToolbarCommand.Pause, PlaybackToolbarCommand.Stop })
            {
                using var button = new PlaybackToolbarButton(command, "tooltip");
                using var normal = Render(button);
                Assert.Contains(Pixels(normal), c => c.ToArgb() == PlaybackToolbarIcons.Normal.ToArgb());
                button.Checked = true;
                using var active = Render(button);
                Assert.Contains(Pixels(active), c => c.ToArgb() == PlaybackToolbarIcons.Active.ToArgb());
                Assert.Contains(Pixels(active), c => c.ToArgb() == PlaybackToolbarIcons.ActiveBackground.ToArgb());
                button.Enabled = false;
                using var disabled = Render(button);
                Assert.Contains(Pixels(disabled), c => c.ToArgb() == PlaybackToolbarIcons.Disabled.ToArgb());
                Assert.DoesNotContain(Pixels(disabled), c => c.ToArgb() == PlaybackToolbarIcons.Active.ToArgb());
            }
        });
    }

    [Fact]
    public void VolumeHasNoDropdownAndMuteHasRedSlashWhileAudioUsesDifferentGeometry()
    {
        OnSta(() =>
        {
            Assert.Equal(new[] { PlaybackToolbarCommand.Subtitles, PlaybackToolbarCommand.Audio,
                PlaybackToolbarCommand.Video, PlaybackToolbarCommand.Chapters },
                Enum.GetValues<PlaybackToolbarCommand>().Where(PlaybackToolbarIcons.HasMenu));
            using var volume = new PlaybackToolbarButton(PlaybackToolbarCommand.Volume, "Głośność");
            using var unmuted = Render(volume);
            Assert.DoesNotContain(Pixels(unmuted), c => c.R > c.B + 60 && c.R > c.G + 60);
            volume.Checked = true;
            using var muted = Render(volume);
            Assert.Contains(Pixels(muted), c => c.R > c.B + 60 && c.R > c.G + 60);
            using var audio = new PlaybackToolbarButton(PlaybackToolbarCommand.Audio, "Ścieżka audio");
            Assert.NotEqual(volume.Width, audio.Width);
            Assert.False(volume.CheckOnClick); // Existing volume logic owns the checked/muted state.
        });
    }

    [Fact]
    public void TransportPreservesOrderTooltipsAndMomentaryActions()
    {
        WithForm((form, _) =>
        {
            var toolbar = Field<ToolStrip>(form, "_transportToolbar");
            var buttons = toolbar.Items.OfType<PlaybackToolbarButton>().ToArray();
            Assert.Equal(Enum.GetValues<PlaybackToolbarCommand>(), buttons.Select(b => b.IconCommand));
            Assert.Equal(new[] { "Odtwarzaj (Ctrl+L)", "Pauza (Ctrl+L)", "Stop (Ctrl+M)",
                "Wstecz o 5 sekund (Ctrl+[)", "W przód o 5 sekund (Ctrl+])", "Wczytaj poprzedni plik (Alt+,)",
                "Wczytaj następny plik (Alt+.)", "Odtwarzaj filmy w aktualnym katalogu (Ctrl+Alt+.)",
                "Napisy", "Ścieżka audio", "Ścieżka wideo", "Rozdziały", "Głośność" },
                buttons.Select(b => b.ToolTipText));
            Assert.All(buttons, b => { Assert.False(b.AutoToolTip); Assert.Null(b.Image); });
            Assert.False(toolbar.ShowItemToolTips);
            Assert.Equal(new[] { PlaybackToolbarCommand.Play, PlaybackToolbarCommand.Pause, PlaybackToolbarCommand.PlayDirectory },
                buttons.Where(b => b.CheckOnClick).Select(b => b.IconCommand));
            Assert.False(Button(toolbar, PlaybackToolbarCommand.Stop).Checked);
            var slider = Field<Control>(form, "_volumeSlider");
            Assert.Equal(90, slider.Width); // Existing ToolStripControlHost expands its control to its 90px width.
            var seek = Field<Control>(form, "_seekSlider");
            Assert.Equal(22, seek.Height);
        });
    }

    [Fact]
    public void RepeatMenuAndExistingShortcutShareTheSameStateAndLoopImplementation()
    {
        WithForm((form, playback) =>
        {
            var menu = Field<MenuStrip>(form, "_menu");
            var movie = menu.Items.OfType<ToolStripMenuItem>().Single(i => i.Text == "Film");
            var repeat = Assert.IsType<ToolStripMenuItem>(movie.DropDownItems["movieRepeat"]);
            Assert.Single(movie.DropDownItems.Cast<ToolStripItem>(), i => i.Name == "movieRepeat");
            Assert.Equal("R", repeat.ShortcutKeyDisplayString);
            Assert.False(repeat.Checked);
            repeat.PerformClick();
            Assert.True(repeat.Checked);
            playback.IsOpen = true;
            Assert.True((bool)Invoke(form, "TryLoopCurrentVideo")!);
            Invoke(form, "ToggleLoopPlaybackFromShortcut");
            Assert.False(repeat.Checked);
            Assert.False((bool)Invoke(form, "TryLoopCurrentVideo")!);
            Assert.DoesNotContain(Field<ToolStrip>(form, "_transportToolbar").Items.Cast<ToolStripItem>(),
                i => (i.ToolTipText ?? string.Empty).Contains("Powtarzaj"));
        });
    }

    [Fact]
    public void PlaybackMuteAndFullScreenKeepTheSameButtonsAndHandlers()
    {
        WithForm((form, playback) =>
        {
            playback.IsOpen = true;
            var toolbar = Field<ToolStrip>(form, "_transportToolbar");
            var buttons = toolbar.Items.OfType<PlaybackToolbarButton>().ToArray();
            Button(toolbar, PlaybackToolbarCommand.Play).PerformClick();
            Assert.True(Button(toolbar, PlaybackToolbarCommand.Play).Checked);
            Button(toolbar, PlaybackToolbarCommand.Pause).PerformClick();
            Assert.True(Button(toolbar, PlaybackToolbarCommand.Pause).Checked);
            Assert.False(Button(toolbar, PlaybackToolbarCommand.Play).Checked);
            var volume = Button(toolbar, PlaybackToolbarCommand.Volume);
            volume.PerformClick();
            Assert.True(volume.Checked);
            volume.PerformClick();
            Assert.False(volume.Checked);
            Invoke(form, "MoveTransportControlsToFullScreen");
            Assert.Same(Field<Panel>(form, "_fullScreenControlsPanel"), toolbar.Parent);
            Assert.Equal(buttons, toolbar.Items.OfType<PlaybackToolbarButton>());
            volume.PerformClick();
            Assert.True(volume.Checked);
            Invoke(form, "RestoreTransportControlsFromFullScreen");
            Assert.Same(Field<SplitContainer>(form, "_splitContainer").Panel1, toolbar.Parent);
            Assert.Equal(buttons, toolbar.Items.OfType<PlaybackToolbarButton>());
        });
    }

    [Fact]
    public void LargerBaseGeometryAndAudioWaveformAreExplicit()
    {
        Assert.Equal(new Size(29, 29), PlaybackToolbarIcons.ButtonSize(PlaybackToolbarCommand.Play, 96));
        Assert.Equal(new Size(35, 29), PlaybackToolbarIcons.ButtonSize(PlaybackToolbarCommand.Audio, 96));
        using var image = new Bitmap(28, 22);
        using var g = Graphics.FromImage(image);
        PlaybackToolbarIcons.Draw(g, PlaybackToolbarCommand.Audio, new RectangleF(0, 0, 26, 20), Color.Black, false);
        // Six independent signal bars cross the center, separated by fully transparent gaps.
        foreach (var x in new[] { 3, 6, 9, 12, 15, 18 }) Assert.True(image.GetPixel(x, 10).A > 0);
        foreach (var x in new[] { 4, 7, 10, 13, 16 }) Assert.Equal(0, image.GetPixel(x, 10).A);
    }

    [Theory]
    [InlineData((int)PlaybackToolbarCommand.Subtitles)]
    [InlineData((int)PlaybackToolbarCommand.Audio)]
    [InlineData((int)PlaybackToolbarCommand.Video)]
    [InlineData((int)PlaybackToolbarCommand.Chapters)]
    public void DropdownFirstClickOpensSecondMouseDownCloses(int commandValue)
    {
        WithDropdowns((toolbar, controller) =>
        {
            var button = Button(toolbar, (PlaybackToolbarCommand)commandValue);
            button.PerformClick();
            Assert.True(controller.ActiveMenu?.Visible);
            Assert.True(button.Checked);
            var point = toolbar.PointToScreen(new Point(button.Bounds.Left + 2, button.Bounds.Top + 2));
            Assert.True(controller.HandleMouseDown(point));
            Assert.Null(controller.ActiveMenu);
            Assert.False(button.Checked);
            button.PerformClick();
            button.PerformClick(); // Also works for keyboard/programmatic Click, without mouse replay.
            Assert.Null(controller.ActiveMenu);
            Assert.False(button.Checked);
        });
    }

    [Fact]
    public void DropdownOutsideClickAndSelectionClearCheckedState()
    {
        WithDropdowns((toolbar, controller) =>
        {
            var button = Button(toolbar, PlaybackToolbarCommand.Audio);
            foreach (var reason in new[] { ToolStripDropDownCloseReason.AppClicked, ToolStripDropDownCloseReason.ItemClicked, ToolStripDropDownCloseReason.AppFocusChange })
            {
                button.PerformClick();
                Assert.True(button.Checked);
                controller.ActiveMenu!.Close(reason);
                Assert.Null(controller.ActiveMenu);
                Assert.False(button.Checked);
            }
        });
    }

    [Fact]
    public void SwitchingDropdownsClosesOldAndModeChangeClosesNew()
    {
        WithDropdowns((toolbar, controller) =>
        {
            var audio = Button(toolbar, PlaybackToolbarCommand.Audio);
            var video = Button(toolbar, PlaybackToolbarCommand.Video);
            audio.PerformClick();
            var previous = controller.ActiveMenu!;
            Assert.True(controller.HandleMouseDown(toolbar.PointToScreen(new Point(video.Bounds.Left + 2, video.Bounds.Top + 2))));
            Assert.False(previous.Visible);
            Assert.False(audio.Checked);
            Assert.True(video.Checked);
            Assert.True(controller.ActiveMenu!.Visible);
            using var panel = new Panel();
            panel.Controls.Add(toolbar);
            Assert.Null(controller.ActiveMenu);
            Assert.False(video.Checked);
        });
    }

    private static void WithDropdowns(Action<ToolStrip, TransportDropdownController> action) => OnSta(() =>
    {
        using var form = new Form { ShowInTaskbar = false };
        using var toolbar = new ToolStrip();
        form.Controls.Add(toolbar);
        _ = form.Handle;
        _ = toolbar.Handle;
        using var controller = new TransportDropdownController(toolbar);
        foreach (var command in Enum.GetValues<PlaybackToolbarCommand>().Where(PlaybackToolbarIcons.HasMenu))
        {
            var button = new PlaybackToolbarButton(command, command.ToString());
            toolbar.Items.Add(button);
            button.Click += (_, _) =>
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("Choice");
                controller.Toggle(menu, button);
            };
        }
        toolbar.PerformLayout();
        action(toolbar, controller);
    });

    private static PlaybackToolbarButton Button(ToolStrip toolbar, PlaybackToolbarCommand command) =>
        Assert.IsType<PlaybackToolbarButton>(toolbar.Items[command.ToString()]);

    private static Bitmap Render(PlaybackToolbarButton button)
    {
        var bitmap = new Bitmap(button.Width, button.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        typeof(PlaybackToolbarButton).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(button, [new PaintEventArgs(graphics, new Rectangle(Point.Empty, bitmap.Size))]);
        return bitmap;
    }

    private static IEnumerable<Color> Pixels(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++) yield return bitmap.GetPixel(x, y);
    }

    private static T Field<T>(Form1 form, string name) =>
        (T)typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    private static object? Invoke(Form1 form, string method) =>
        typeof(Form1).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);

    private static void WithForm(Action<Form1, WindowTitleTests.TitlePlayback> action) => OnSta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "SubEditToolbar-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var engine = DispatchProxy.Create<IPlaybackEngine, WindowTitleTests.TitlePlayback>();
            using var form = new Form1(new AppSettings { Volume = 27 }, new PortableSettingsStore(directory), new TestFactory(engine));
            _ = form.Handle;
            action(form, (WindowTitleTests.TitlePlayback)(object)engine);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    });

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Toolbar STA test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
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
