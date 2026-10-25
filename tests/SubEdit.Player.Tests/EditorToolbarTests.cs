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

[CollectionDefinition("Editor toolbar UI", DisableParallelization = true)]
public sealed class EditorToolbarUiCollection;

[Collection("Editor toolbar UI")]
public class EditorToolbarTests
{
    private static readonly string[] Names = ["toolbarOpenVideo", "toolbarOpenSubtitles", "toolbarNew", "toolbarSave",
        "toolbarUndo", "toolbarRedo", "toolbarEditor", "toolbarFind", "toolbarActiveSubtitleFormat", "toolbarSubtitleTrace",
        "toolbarConvert", "toolbarAddTime", "toolbarShowTime", "toolbarTransparentSubtitles", "toolbarShowSubtitles",
        "toolbarAlwaysOnTop", "toolbarFullscreen", "toolbarOptions", "toolbarClose"];

    [Fact]
    public void OrderAndExactUndoRedoSeparatorsPreserveAllOriginalCommandsWithoutDuplicates()
    {
        WithForm((form, _) =>
        {
            var toolbar = Field<ToolStrip>(form, "_mainToolbar");
            var items = toolbar.Items.Cast<ToolStripItem>().ToArray();
            Assert.Equal(Names, items.Where(i => i is not ToolStripSeparator).Select(i => i.Name));
            Assert.Equal(Names.Length, items.Where(i => i is not ToolStripSeparator).Select(i => i.Name).Distinct().Count());
            var save = Array.FindIndex(items, i => i.Name == "toolbarSave");
            Assert.Equal(new[] { "toolbarSave", "|", "toolbarUndo", "toolbarRedo", "|", "toolbarEditor" },
                items.Skip(save).Take(6).Select(i => i is ToolStripSeparator ? "|" : i.Name));
            Assert.Equal(2, items.Skip(save).Take(6).Count(i => i is ToolStripSeparator));
            for (var i = 1; i < items.Length; i++) Assert.False(items[i] is ToolStripSeparator && items[i - 1] is ToolStripSeparator);
            foreach (var name in new[] { "toolbarAlwaysOnTop", "toolbarFullscreen", "toolbarOptions", "toolbarClose" })
                Assert.Single(items, i => i.Name == name);
            Assert.All(items.Where(i => i is not ToolStripSeparator), i => Assert.Null(i.Image));
        });
    }

    [Fact]
    public void ExistingTooltipsFindMenuAndCheckSemanticsArePreserved()
    {
        WithForm((form, _) =>
        {
            var toolbar = Field<ToolStrip>(form, "_mainToolbar");
            var expected = new[] { "Otwórz plik wideo (Ctrl+O)", "Otwórz plik napisów (Ctrl+Shift+O)", "Nowy tekst (Ctrl+N)",
                "Zapisz tekst (Ctrl+S)", "Cofnij (Ctrl+Z)", "Ponów (Ctrl+Y)", "Edytor napisów (Ctrl+E)", "Znajdź (Ctrl+F)" };
            Assert.Equal(expected, Names.Take(8).Select(n => toolbar.Items[n]!.ToolTipText));
            Assert.StartsWith("Aktywny format napisów:", toolbar.Items["toolbarActiveSubtitleFormat"]!.ToolTipText);
            Assert.Equal(new[] { "Śledzenie napisów w edytorze (Ctrl+J)", "Konwertuj format napisów (Ctrl+T)",
                "Przesuń czas napisów (Ctrl+D)", "Zmień czas wyświetlania napisów (Ctrl+W)", "Przezroczyste napisy (M)",
                "Pokaż/ukryj napisy (Ctrl+Shift+N)", "Zawsze na wierzchu (F6)", "Tryb pełnoekranowy (F11)",
                "Opcje (F12)", "Zakończ (Alt+F4)" }, Names.Skip(9).Select(n => toolbar.Items[n]!.ToolTipText));
            Assert.False(toolbar.ShowItemToolTips);
            Assert.All(toolbar.Items.Cast<ToolStripItem>().Where(i => i is not ToolStripSeparator), i => Assert.False(i.AutoToolTip));
            Assert.Equal(new[] { "toolbarEditor", "toolbarSubtitleTrace", "toolbarTransparentSubtitles", "toolbarShowSubtitles",
                "toolbarAlwaysOnTop", "toolbarFullscreen" }, toolbar.Items.OfType<ToolStripButton>().Where(b => b.CheckOnClick).Select(b => b.Name));
            var find = Assert.IsType<EditorToolbarSplitButton>(toolbar.Items["toolbarFind"]);
            Assert.Equal(new[] { "Znajdź", "Znajdź krótko wyświetlany napis", "Znajdź długi wiersz", "Znajdź przerwę w wyświetlaniu napisów",
                "Znajdź nieposortowany napis", "Znajdź następny" }, find.DropDownItems.Cast<ToolStripItem>().Select(i => i.Text));
        });
    }

    [Fact]
    public void FullScreenUsesF11InUiAndTogglesBothWaysWhileKeepingAltEnter()
    {
        WithForm((form, _) =>
        {
            var toolbar = Field<ToolStrip>(form, "_mainToolbar");
            var fullscreenButton = Assert.IsType<EditorToolbarButton>(toolbar.Items["toolbarFullscreen"]);
            Assert.Equal("Tryb pełnoekranowy (F11)", fullscreenButton.ToolTipText);

            var menu = Field<MenuStrip>(form, "_menu");
            var view = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Widok");
            var fullscreenItem = view.DropDownItems.OfType<ToolStripMenuItem>()
                .Single(item => item.Text?.StartsWith("Tryb pełnoekranowy", StringComparison.Ordinal) == true);
            Assert.Equal(Keys.F11, fullscreenItem.ShortcutKeys);
            Assert.Equal("F11", fullscreenItem.ShortcutKeyDisplayString);

            var processCmdKey = typeof(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Form1.ProcessCmdKey was not found.");
            object?[] invokeArgs = [Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.F11];
            Assert.True((bool)processCmdKey.Invoke(form, invokeArgs)!);
            Assert.True(Field<bool>(form, "_isFullScreen"));

            invokeArgs = [Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.F11];
            Assert.True((bool)processCmdKey.Invoke(form, invokeArgs)!);
            Assert.False(Field<bool>(form, "_isFullScreen"));

            invokeArgs = [Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Alt | Keys.Enter];
            Assert.True((bool)processCmdKey.Invoke(form, invokeArgs)!);
            Assert.True(Field<bool>(form, "_isFullScreen"));
            invokeArgs = [Message.Create(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero), Keys.Alt | Keys.Enter];
            Assert.True((bool)processCmdKey.Invoke(form, invokeArgs)!);
            Assert.False(Field<bool>(form, "_isFullScreen"));
        });
    }

    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(192)]
    public void EveryIconSupportsStatesAndDpiWithoutClipping(int dpi)
    {
        Sta(() =>
        {
            foreach (var icon in Enum.GetValues<EditorToolbarIcon>())
            {
                using var button = new EditorToolbarButton("test", "test", icon);
                button.ApplyDpi(dpi);
                Assert.Equal(new Size(PlaybackToolbarIcons.Scale(29, dpi), PlaybackToolbarIcons.Scale(29, dpi)), button.Size);
                foreach (var (active, enabled) in new[] { (false, true), (true, true), (true, false) })
                {
                    button.Checked = active;
                    button.Enabled = enabled;
                    using var bitmap = Render(button);
                    Assert.Contains(Pixels(bitmap), c => c.A > 0);
                    if (!active)
                    {
                        for (var x = 0; x < bitmap.Width; x++) { Assert.Equal(0, bitmap.GetPixel(x, 0).A); Assert.Equal(0, bitmap.GetPixel(x, bitmap.Height - 1).A); }
                        for (var y = 0; y < bitmap.Height; y++) { Assert.Equal(0, bitmap.GetPixel(0, y).A); Assert.Equal(0, bitmap.GetPixel(bitmap.Width - 1, y).A); }
                    }
                    if (active && enabled) Assert.Contains(Pixels(bitmap), c => c.ToArgb() == PlaybackToolbarIcons.ActiveBackground.ToArgb());
                    if (!enabled) Assert.DoesNotContain(Pixels(bitmap), c => c.A > 0 && (c.B > c.R + 50 || c.G > c.R + 50));
                }
                button.ApplyDpi(96);
                Assert.Equal(new Size(29, 29), button.Size);
            }
            using var find = new EditorToolbarSplitButton("find", "find");
            find.ApplyDpi(dpi);
            Assert.Equal(PlaybackToolbarIcons.Scale(41, dpi), find.Width);
            using var image = Render(find);
            Assert.Contains(Pixels(image), c => c.A > 0);
        });
    }

    [Fact]
    public void ButtonsUseRealUndoRedoHistoryAndRefreshImmediately()
    {
        WithForm((form, directory) =>
        {
            var editor = Field<SubtitleTextEditor>(form, "_subtitleEditor");
            var toolbar = Field<ToolStrip>(form, "_mainToolbar");
            var undo = Assert.IsType<EditorToolbarButton>(toolbar.Items["toolbarUndo"]);
            var redo = Assert.IsType<EditorToolbarButton>(toolbar.Items["toolbarRedo"]);
            editor.EmptyUndoBuffer();
            Assert.False(undo.Enabled); Assert.False(redo.Enabled);
            editor.AppendText("first");
            Assert.True(undo.Enabled); Assert.False(redo.Enabled);
            undo.PerformClick();
            Assert.Equal(string.Empty, editor.Text);
            Assert.False(undo.Enabled); Assert.True(redo.Enabled);
            redo.PerformClick();
            Assert.Equal("first", editor.Text);
            Assert.True(undo.Enabled); Assert.False(redo.Enabled);
            undo.PerformClick();
            editor.AppendText("new branch");
            Assert.False(redo.Enabled);
            Assert.Equal(editor.CanUndo, undo.Enabled);
            editor.EmptyUndoBuffer();
            Assert.False(undo.Enabled); Assert.False(redo.Enabled);
            Assert.False(undo.Checked); Assert.False(redo.Checked);
            var unchanged = editor.Text;
            undo.PerformClick(); redo.PerformClick();
            Assert.Equal(unchanged, editor.Text);
        });
    }

    [Fact]
    public void ChangingDocumentResetsAvailabilityAndCannotRestorePreviousDocument()
    {
        WithForm((form, directory) =>
        {
            var editor = Field<SubtitleTextEditor>(form, "_subtitleEditor");
            var toolbar = Field<ToolStrip>(form, "_mainToolbar");
            editor.AppendText("old document");
            var path = Path.Combine(directory, "other.txt");
            File.WriteAllText(path, "00:00:01:new document");
            Assert.True((bool)typeof(Form1).GetMethod("LoadSubtitleFile", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, [path, false, false])!);
            Assert.False(toolbar.Items["toolbarUndo"]!.Enabled);
            Assert.False(toolbar.Items["toolbarRedo"]!.Enabled);
            toolbar.Items["toolbarUndo"]!.PerformClick();
            Assert.DoesNotContain("old document", editor.Text);
            editor.AppendText("!");
            Assert.True(toolbar.Items["toolbarUndo"]!.Enabled);
        });
    }

    [Fact]
    public void NoHistoricalToolbarResourcesOrLoaderRemainInAssembly()
    {
        var assembly = typeof(Form1).Assembly;
        Assert.DoesNotContain(assembly.GetManifestResourceNames(), n => n.Contains("ToolbarIconsOriginal") || n.Contains("ToolbarNavigationOriginal"));
        Assert.Null(assembly.GetType("SubEdit.Player.WinForms.Controls.OriginalToolbarIcons"));
    }

    [Theory]
    [InlineData(0)] // Visible editor.
    [InlineData(1)] // Hidden editor.
    [InlineData(2)] // Fullscreen.
    public void OnlyAltPeriodAndCommaNavigateFilesInEveryMode(int mode)
    {
        WithForm((form, directory) =>
        {
            foreach (var name in new[] { "A.mkv", "B.mkv", "C.mkv" })
                File.WriteAllBytes(Path.Combine(directory, name), []);
            var load = typeof(Form1).GetMethod("LoadVideoFile", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)load.Invoke(form, [Path.Combine(directory, "B.mkv"), null, null])!);
            var playback = (WindowTitleTests.TitlePlayback)(object)Field<IPlaybackEngine>(form, "_playback");
            var opens = 0;
            playback.BeforeOpen = () => opens++;
            var split = Field<SplitContainer>(form, "_splitContainer");
            split.Panel2Collapsed = mode == 1;
            if (mode == 2) Assert.True(DispatchKey(form, Keys.F11));
            try
            {
                Assert.False(DispatchKey(form, Keys.F));
                Assert.False(DispatchKey(form, Keys.Shift | Keys.F));
                Assert.Equal(0, opens);
                Assert.Equal("B.mkv", form.Text);
                Assert.Equal(mode == 2, Field<bool>(form, "_isFullScreen"));

                Assert.True(DispatchKey(form, Keys.Alt | Keys.OemPeriod));
                Assert.Equal("C.mkv", form.Text);
                Assert.Equal(1, opens);
                Assert.True(DispatchKey(form, Keys.Alt | Keys.Oemcomma));
                Assert.Equal("B.mkv", form.Text);
                Assert.Equal(2, opens);
                Assert.Equal(mode == 2, Field<bool>(form, "_isFullScreen"));
            }
            finally { if (Field<bool>(form, "_isFullScreen")) DispatchKey(form, Keys.F11); }
        });
    }

    [Fact]
    public void CtrlFStillOpensFindAndNativeEditorAcceptsLowerAndUpperF()
    {
        WithForm((form, _) =>
        {
            var observedFind = false;
            form.BeginInvoke((Action)(() =>
            {
                var dialog = Application.OpenForms.OfType<SubtitleFindDialog>().Single();
                observedFind = dialog.Visible;
                dialog.Close();
            }));
            Assert.True(DispatchKey(form, Keys.Control | Keys.F));
            Assert.True(observedFind);
            var editor = Field<SubtitleTextEditor>(form, "_subtitleEditor");
            editor.ReadOnly = false;
            editor.Text = string.Empty;
            Assert.False(DispatchKey(form, Keys.F));
            Assert.False(DispatchKey(form, Keys.Shift | Keys.F));
            SendKeyboardCharacter(editor.Handle, 0x0102, (IntPtr)'f', IntPtr.Zero);
            SendKeyboardCharacter(editor.Handle, 0x0102, (IntPtr)'F', IntPtr.Zero);
            Assert.Equal("fF", editor.Text);
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendKeyboardCharacter(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    private static bool DispatchKey(Form1 form, Keys key) =>
        (bool)typeof(Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [Message.Create(form.Handle, 0x0100, (IntPtr)(key & Keys.KeyCode), IntPtr.Zero), key])!;

    private static Bitmap Render(ToolStripItem item)
    {
        var bitmap = new Bitmap(item.Width, item.Height);
        using var g = Graphics.FromImage(bitmap);
        item.GetType().GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(item, [new PaintEventArgs(g, new Rectangle(Point.Empty, bitmap.Size))]);
        return bitmap;
    }
    private static IEnumerable<Color> Pixels(Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++) yield return bitmap.GetPixel(x, y);
    }
    private static T Field<T>(Form1 form, string name) => (T)typeof(Form1).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void WithForm(Action<Form1, string> action) => Sta(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "SubEditUpperToolbar-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var engine = DispatchProxy.Create<IPlaybackEngine, WindowTitleTests.TitlePlayback>();
            using var form = new Form1(new AppSettings(), new PortableSettingsStore(dir), new TestFactory(engine));
            _ = form.Handle;
            action(form, dir);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    });
    private static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(40)), "Upper toolbar STA test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    private sealed class TestFactory(IPlaybackEngine engine) : IPlaybackEngineFactory
    {
        public IPlaybackEngine CreateEngine(Control? dispatcher, int volume, Func<VideoDecodingMode> mode) => engine;
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
