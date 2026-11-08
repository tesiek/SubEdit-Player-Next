using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class VolumeOsdIntegrationTests
{
    [Fact]
    public void FastWheelSequence_PreservesEveryLevelAudioCallAndMuteState() => WithForm((form, audio, settings) =>
    {
        var expected = 50;
        var expectedAudio = new List<int>();
        var input = new[] { 0, 0 }.Concat(Enumerable.Repeat(120, 24))
            .Concat(Enumerable.Repeat(-120, 24)).Concat(Enumerable.Repeat(120, 8));
        var count = 0;
        foreach (var delta in input)
        {
            var previous = expected;
            expected = delta == 0 ? expected > 0 ? 0 : 50 : Math.Clamp(expected + Math.Sign(delta) * 5, 0, 100);
            if (expected != previous) expectedAudio.Add(expected);
            Invoke(form, delta == 0 ? "ToggleMute" : "AdjustVolume", delta == 0 ? [] : [delta]);
            Assert.Equal(expected, Field<VolumeTrackBar>(form, "_volumeSlider").Value);
            Assert.Equal(expected, settings.Volume);
            Assert.Equal(expected == 0, Field<ToolStripButton>(form, "_volumeButton").Checked);
            count++;
        }
        Assert.Equal(58, count);
        Assert.Equal(40, expected);
        Assert.Equal(40, audio.Volumes.Count);
        Assert.Equal(expectedAudio, audio.Volumes);
        Assert.Equal("Głośność: 40%", Field<VideoOverlayForm>(form, "_videoOverlay").VolumeText);
    });

    [Fact]
    public void IdenticalVolume_RenewsDeadline_AndOtherOsdTransitionsStillExpire() => WithForm((form, _, _) =>
    {
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        Invoke(form, "ShowVolumeOsd");
        var text = overlay.VolumeText;
        SetField(form, "_volumeOsdExpiresAt", DateTime.UtcNow.AddSeconds(-1));
        Invoke(form, "ShowVolumeOsd");
        Assert.Equal(text, overlay.VolumeText);
        Assert.True(Field<DateTime>(form, "_volumeOsdExpiresAt") > DateTime.UtcNow);
        Invoke(form, "ShowPlaybackOsd", "Pauza");
        Assert.Equal("Pauza", overlay.PlaybackText);
        Assert.Equal("", overlay.VolumeText);
        Invoke(form, "ShowVolumeOsd");
        Assert.Equal("", overlay.PlaybackText);
        Assert.Equal(default, Field<DateTime>(form, "_playbackOsdExpiresAt"));
        Invoke(form, "ShowAccumulatedSeekOsd", 10000L);
        Assert.Equal("W przód o 10s", overlay.PlaybackText);
        Invoke(form, "ShowVolumeOsd");
        Assert.Equal("", overlay.PlaybackText);
        SetField(form, "_volumeOsdExpiresAt", DateTime.UtcNow.AddSeconds(-1));
        Invoke(form, "UpdatePlaybackUi");
        Assert.Equal("", overlay.VolumeText);
        Assert.Equal(default, Field<DateTime>(form, "_volumeOsdExpiresAt"));
    });

    [Fact]
    public void HiddenAndClosedOverlay_HasNoDeferredVolumeWork() => WithForm((form, _, _) =>
    {
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        Invoke(form, "ShowVolumeOsd");
        overlay.Hide();
        Invoke(form, "ShowVolumeOsd");
        Assert.True(overlay.Visible);
        Assert.Equal("Głośność: 50%", overlay.VolumeText);
        Invoke(form, "ClearInformationOsd");
        Assert.Equal("", overlay.VolumeText);
        Assert.Equal("", overlay.PlaybackText);
        Assert.False(overlay.Visible);
        Invoke(form, "AdjustVolume", 120);
        Assert.True(overlay.Visible);
        form.Close();
        Assert.True(overlay.IsDisposed);
    });

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    public void AtomicVolume_FinalPixelsMatchPreviousTwoSetters(int dpi) => SubtitleDialogLayoutTests.Sta(() =>
    {
        foreach (var points in new[] { 18, 30 })
        foreach (var transparency in new[] { 0, 25, 50, 75, 100 })
        foreach (var subtitles in new[] { false, true })
        {
            using var old = CreateOverlay(points, transparency, subtitles);
            using var atomic = CreateOverlay(points, transparency, subtitles);
            old.PlaybackText = "Pauza"; atomic.PlaybackText = "Pauza";
            old.VolumeText = "Głośność: 50%"; atomic.VolumeText = "Głośność: 50%";
            old.PlaybackText = ""; old.VolumeText = "Głośność: 50%";
            atomic.SetVolumeOsd("Głośność: 50%"); // Must also clear a different playback state.
            Assert.Equal("", atomic.PlaybackText);
            var expected = ImageHash(old, dpi);
            Assert.Equal(expected, ImageHash(atomic, dpi));
            atomic.SetVolumeOsd("Głośność: 50%");
            Assert.Equal(expected, ImageHash(atomic, dpi));
        }
    });

    [Fact]
    public void IdenticalClockText_DoesNotUploadEvenWhileOtherOsdKeepsOverlayVisible() => WithForm((form, _, _) =>
    {
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        Invoke(form, "ShowVolumeOsd");
        Assert.True(Uploads(overlay) > 0);
        var count = Uploads(overlay);
        for (var i = 0; i < 10; i++)
            overlay.OsdText = "";
        Assert.Equal(count, Uploads(overlay));

        overlay.OsdText = "0:00:01 / 0:10:00";
        Assert.Equal(++count, Uploads(overlay));
        overlay.OsdText = "0:00:01 / 0:10:00";
        Assert.Equal(count, Uploads(overlay));

        overlay.OsdFontSize += 2;
        Assert.Equal(++count, Uploads(overlay));
        overlay.OsdText = "";
        Assert.Equal(++count, Uploads(overlay));
        overlay.OsdText = "";
        Assert.Equal(count, Uploads(overlay));
    });

    [Fact]
    public void EmbeddedCue_ReusesParsedContentUntilItsInputsOrSourceChange() => WithForm((form, engine, settings) =>
    {
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        engine.TrackId = 4;
        engine.Session = 1;
        engine.CurrentEmbeddedSubtitleText = "/Tekst";
        settings.SubtitlePlayback.ItalicLinesStartingWithSlash = true;
        Invoke(form, "UpdateSubtitleOverlay", 5000L);
        var first = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.Equal(SubtitleTextStyle.Italic, first.Lines[0].Runs[0].Style);
        var count = Uploads(overlay);
        Assert.True(count > 0);

        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        Assert.Same(first, overlay.SubtitleCue);
        Assert.Equal(count, Uploads(overlay));

        settings.SubtitlePlayback.ItalicLinesStartingWithSlash = false;
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        var second = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.NotSame(first, second);
        Assert.Equal(SubtitleTextStyle.None, second.Lines[0].Runs[0].Style);
        Assert.Equal(++count, Uploads(overlay));

        engine.TrackId = 5;
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        var third = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.NotSame(second, third);

        engine.Session = 2;
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        var fourth = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.NotSame(third, fourth);

        engine.CurrentEmbeddedSubtitleText = "Inna treść";
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        var changed = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.Equal("Inna treść", changed.Lines[0].Runs[0].Text);

        engine.CurrentEmbeddedSubtitleText = null;
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        Assert.Null(overlay.SubtitleCue);
        Assert.False(overlay.Visible);

        engine.CurrentEmbeddedSubtitleText = "Inna treść";
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        var restored = Assert.IsType<SubtitleCue>(overlay.SubtitleCue);
        Assert.NotSame(changed, restored);
        SetField(form, "_externalSubtitleOverlayEnabled", true);
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        SetField(form, "_externalSubtitleOverlayEnabled", false);
        Invoke(form, "UpdateSubtitleOverlay", 5200L);
        Assert.NotSame(restored, Assert.IsType<SubtitleCue>(overlay.SubtitleCue));
    });

    [Theory]
    [InlineData("subtitle")]
    [InlineData("volume")]
    [InlineData("seek")]
    public void ReshowAtUnchangedBounds_UploadsCurrentContentSynchronously(string item) => WithForm((form, _, _) =>
    {
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        void ShowContent()
        {
            if (item == "subtitle")
            {
                overlay.SubtitleCue = new SubtitleCue(0, 1000, [new SubtitleLine([new SubtitleTextRun("Napis", SubtitleTextStyle.None, null)])]);
                Invoke(form, "UpdateOverlayVisibility");
            }
            else if (item == "volume") Invoke(form, "ShowVolumeOsd");
            else Invoke(form, "ShowAccumulatedSeekOsd", 10000L);
        }

        ShowContent();
        Assert.True(overlay.Visible);
        Assert.True(Uploads(overlay) > 0);
        var bounds = overlay.Bounds;
        overlay.Hide();
        var count = Uploads(overlay);
        ShowContent();
        Assert.Equal(bounds, overlay.Bounds);
        Assert.True(overlay.Visible);
        Assert.Equal(count + 1, Uploads(overlay));
    });

    [Fact]
    public void RestoreWindow_ReshowsCurrentOverlayWithoutWaitingForTimer() => WithForm((form, _, _) =>
    {
        form.Show();
        Invoke(form, "ShowVolumeOsd");
        var overlay = Field<VideoOverlayForm>(form, "_videoOverlay");
        Assert.True(overlay.Visible);
        form.WindowState = FormWindowState.Minimized;
        Assert.False(overlay.Visible);

        var count = Uploads(overlay);
        form.WindowState = FormWindowState.Normal;
        Assert.True(overlay.Visible);
        Assert.True(Uploads(overlay) > count);
        Assert.Equal("Głośność: 50%", overlay.VolumeText);
    });

    private static int Uploads(VideoOverlayForm overlay) =>
        (int)typeof(VideoOverlayForm).GetField("_successfulUploads", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;

    private static VideoOverlayForm CreateOverlay(int points, int transparency, bool subtitles) => new()
    {
        ClientSize = new Size(900, 400), OsdFontSize = points, OsdText = "0:00:01 / 0:50:00",
        OsdBackgroundColor = Color.Red, OsdBorderColor = Color.Blue, OsdBackgroundTransparencyPercent = transparency,
        SubtitleCue = subtitles ? new SubtitleCue(0, 1000, [new SubtitleLine([new SubtitleTextRun("Napisy", SubtitleTextStyle.Bold, null)])]) : null
    };

    private static string ImageHash(VideoOverlayForm overlay, int dpi)
    {
        using var bitmap = new Bitmap(900, 400, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(dpi, dpi);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            typeof(VideoOverlayForm).GetMethod("DrawOverlay", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, [graphics]);
        }
        var bits = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(bits.Stride) * bitmap.Height];
            Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        finally { bitmap.UnlockBits(bits); }
    }

    private static T Field<T>(Form1 form, string name) => (T)typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void SetField(Form1 form, string name, object value) => typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, value);
    private static void Invoke(Form1 form, string method, params object?[] args) => typeof(Form1).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args);

    private static void WithForm(Action<Form1, VolumePlayback, AppSettings> test) => SubtitleDialogLayoutTests.Sta(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "VolumeOsd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var engine = DispatchProxy.Create<IPlaybackEngine, VolumePlayback>();
            var audio = (VolumePlayback)(object)engine;
            var settings = new AppSettings { Volume = 50, TimeDisplayMode = TimeDisplayMode.Hidden };
            using var form = new Form1(settings, new PortableSettingsStore(directory), new TestFactory(engine));
            _ = form.Handle;
            audio.Volumes.Clear();
            test(form, audio, settings);
        }
        finally { Directory.Delete(directory, true); }
    });

    public class VolumePlayback : WindowTitleTests.TitlePlayback, IEmbeddedTextSubtitleSource
    {
        public readonly List<int> Volumes = [];
        public int? TrackId;
        public long Session;
        public string? CurrentEmbeddedSubtitleText { get; set; }
        public event EventHandler? EmbeddedSubtitleTextChanged { add { } remove { } }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "get_ActiveEmbeddedSubtitleTrackId") return TrackId;
            if (method.Name == "get_SessionId") return Session;
            if (method.Name == "set_VolumePercent") Volumes.Add((int)args![0]!);
            return base.Invoke(method, args);
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
