using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using SubEdit.Player.WinForms.Playback;
using Xunit;

namespace SubEdit.Player.Tests;

[CollectionDefinition("Video context menu UI", DisableParallelization = true)]
public sealed class VideoContextMenuUiCollection;

[Collection("Video context menu UI")]
public class VideoContextMenuTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void Menu_GroupsPlaybackAndTransformationsAndExposesDirectZoomCommands()
    {
        WithForm((_, factory, _, _) =>
        {
            Assert.Equal(new[]
            {
                "Otwórz plik wideo", "Otwórz plik napisów", "-", "Pauza / Odtwarzanie", "Stop", "Prędkość odtwarzania",
                "-", "Przesunięcie obrazu", "Powiększenie", "Pomniejszenie", "Resetuj transformacje",
                "-", "Wyświetlaj napisy", "Wyświetlaj czas filmu", "Tryb pełnoekranowy", "-", "Opcje", "Zakończ program"
            }, factory.Menu.Items.Cast<ToolStripItem>().Select(item => item is ToolStripSeparator ? "-" : item.Text));
            Assert.Equal(new[] { "Przyspiesz", "Normalna prędkość", "Zwolnij" }, ChildLabels("Prędkość odtwarzania"));
            Assert.Equal(new[] { "W górę", "W dół", "W lewo", "W prawo" }, ChildLabels("Przesunięcie obrazu"));
            Assert.Empty(ChildLabels("Powiększenie"));
            Assert.Empty(ChildLabels("Pomniejszenie"));

            IEnumerable<string?> ChildLabels(string label) =>
                Item(factory.Menu.Items, label).DropDownItems.Cast<ToolStripItem>().Select(item => item.Text);
        });
    }

    [Fact]
    public void Menu_ZoomCommandsFollowMediaAvailabilityAndApplyBothDirections()
    {
        WithForm((form, factory, playback, _) =>
        {
            form.Show();
            var zoomIn = Item(factory.Menu.Items, "Powiększenie");
            var zoomOut = Item(factory.Menu.Items, "Pomniejszenie");
            OpenAndCloseMenu(form, factory);
            Assert.False(zoomIn.Enabled);
            Assert.False(zoomOut.Enabled);

            playback.IsMediaOpen = true;
            OpenAndCloseMenu(form, factory);
            Assert.True(zoomIn.Enabled);
            Assert.True(zoomOut.Enabled);
            zoomIn.PerformClick();
            Assert.Equal(110, factory.ZoomPercent);
            zoomOut.PerformClick();
            Assert.Equal(100, factory.ZoomPercent);
        });
    }

    [Fact]
    public void Menu_CheckmarksFollowSubtitleVisibilityAndFullscreen()
    {
        WithForm((form, factory, _, _) =>
        {
            form.Show();
            var view = Item(form.MainMenuStrip!.Items, "Widok");
            var mainSubtitles = Item(view.DropDownItems, "Wyświetlaj napisy");
            var subtitles = Item(factory.Menu.Items, "Wyświetlaj napisy");
            var fullscreen = Item(factory.Menu.Items, "Tryb pełnoekranowy");
            var toolbar = (ToolStrip)typeof(Form1).GetField("_mainToolbar", Hidden)!.GetValue(form)!;
            var toolbarSubtitles = Assert.IsAssignableFrom<ToolStripButton>(toolbar.Items["toolbarShowSubtitles"]);

            AssertChecks(true, false);
            subtitles.PerformClick();
            AssertChecks(false, false);
            mainSubtitles.PerformClick();
            AssertChecks(true, false);
            toolbarSubtitles.PerformClick();
            AssertChecks(false, false);
            Press(Keys.Control | Keys.Shift | Keys.N);
            AssertChecks(true, false);

            fullscreen.PerformClick();
            AssertChecks(true, true);
            Press(Keys.F11);
            AssertChecks(true, false);

            void AssertChecks(bool expectedSubtitles, bool expectedFullscreen)
            {
                OpenAndCloseMenu(form, factory);
                Assert.Equal(expectedSubtitles, subtitles.Checked);
                Assert.Equal(expectedSubtitles, mainSubtitles.Checked);
                Assert.Equal(expectedSubtitles, toolbarSubtitles.Checked);
                Assert.Equal(expectedFullscreen, fullscreen.Checked);
            }

            void Press(Keys keys)
            {
                var message = Message.Create(form.Handle, 0x100, (nint)keys, 0);
                Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, [message, keys])!);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimeCounter_CyclesAndSynchronizesBothMenusAndShortcut(bool fullscreen)
    {
        WithForm((form, factory, _, directory) =>
        {
            form.Show();
            var view = Item(form.MainMenuStrip!.Items, "Widok");
            var mainTime = Item(view.DropDownItems, "Wyświetlaj czas filmu");
            var videoTime = Item(factory.Menu.Items, "Wyświetlaj czas filmu");
            if (fullscreen)
                Item(factory.Menu.Items, "Tryb pełnoekranowy").PerformClick();

            AssertChecks(TimeDisplayMode.Elapsed);
            mainTime.PerformClick();
            AssertChecks(TimeDisplayMode.Remaining);
            videoTime.PerformClick();
            AssertChecks(TimeDisplayMode.Hidden);

            // T is a player shortcut, so hide the editor in windowed mode.
            if (!fullscreen)
                Item(view.DropDownItems, "Edytor napisów").PerformClick();
            var message = Message.Create(form.Handle, 0x100, (nint)Keys.T, 0);
            Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, [message, Keys.T])!);
            AssertChecks(TimeDisplayMode.Elapsed);

            void AssertChecks(TimeDisplayMode expected)
            {
                var visible = expected != TimeDisplayMode.Hidden;
                Assert.Equal(expected, new PortableSettingsStore(directory).Load().TimeDisplayMode);
                Assert.Equal(visible, mainTime.Checked);
                Assert.Equal(visible, videoTime.Checked);
                OpenAndCloseMenu(form, factory);
                Assert.Equal(visible, videoTime.Checked);
                if (!fullscreen)
                {
                    view.ShowDropDown();
                    Assert.True(view.DropDown.Visible);
                    Assert.Equal(visible, mainTime.Checked);
                    view.HideDropDown();
                }
            }
        });
    }

    private static ToolStripMenuItem Item(ToolStripItemCollection items, string label) =>
        items.OfType<ToolStripMenuItem>().Single(item => item.Text == label);

    private static void OpenAndCloseMenu(Form form, TestFactory factory)
    {
        factory.Menu.Show(form, new System.Drawing.Point(20, 20));
        Assert.True(factory.Menu.Visible);
        factory.Menu.Close();
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

    [Theory]
    [InlineData("late-handler")]
    [InlineData("aborted-session")]
    [InlineData("unsaved-subtitles")]
    public void CancelledClose_KeepsWindowAndResourcesUsable(string scenario)
    {
        WithForm((form, factory, playback, directory) =>
        {
            form.Show();
            var overlay = ShowShutdownTestOverlay(form);
            var handle = form.Handle;
            var overlayHandle = overlay.Handle;
            var bounds = form.Bounds;
            var settingsPath = Path.Combine(directory, "SubEdit.Player.Next.json");
            var editor = FormField<SubtitleTextEditor>(form, "_subtitleEditor");
            var timers = new[] { "_playbackTimer", "_seekRepeatTimer", "_autoSaveTimer", "_fullScreenControlsTimer" }
                .Select(name => FormField<System.Windows.Forms.Timer>(form, name)).ToArray();
            var enabledTimers = timers.Select(timer => timer.Enabled).ToArray();
            Assert.True(enabledTimers[0]);
            var closed = 0;
            form.FormClosed += (_, _) => closed++;

            if (scenario == "late-handler")
            {
                FormClosingEventHandler cancel = (_, e) => { Assert.False(e.Cancel); e.Cancel = true; };
                form.FormClosing += cancel;
                try { form.Close(); }
                finally { form.FormClosing -= cancel; }
            }
            else if (scenario == "aborted-session")
            {
                Assert.Equal((nint)1, SendMessage(handle, WmQueryEndSession, 0, 0));
                AssertStillOpen();
                SendMessage(handle, WmEndSession, 0, 0);
            }
            else
            {
                const string draft = "1\r\n00:00:01,000 --> 00:00:02,000\r\nNiezapisane";
                editor.AppendText(draft);
                Assert.True(editor.Modified);
                var cancelledQuestion = false;
                form.BeginInvoke(() =>
                {
                    if (Application.OpenForms.OfType<QuestionDialog>().SingleOrDefault() is { CancelButton: { } cancel })
                    {
                        cancelledQuestion = true;
                        cancel.PerformClick();
                    }
                });
                try
                {
                    form.Close();
                    Assert.True(cancelledQuestion);
                    Assert.Equal(draft, editor.Text);
                    Assert.True(editor.Modified);
                }
                finally
                {
                    if (!form.IsDisposed) editor.Undo();
                }
            }

            AssertStillOpen();
            ShowModal(() => Assert.Equal(1, ModalDepth(form)));
            Assert.Equal(0, ModalDepth(form));
            var f11 = Message.Create(handle, 0x0100, (nint)Keys.F11, 0);
            Assert.True(Application.FilterMessage(ref f11));
            Assert.True(FormField<bool>(form, "_isFullScreen"));
            Assert.True(Application.FilterMessage(ref f11));
            Assert.False(FormField<bool>(form, "_isFullScreen"));

            form.Close();
            form.Dispose();
            Assert.Equal(1, closed);
            Assert.Equal(1, playback.DisposeCount);
            Assert.Equal(1, factory.HostDisposeCount);

            void AssertStillOpen()
            {
                Assert.Equal(0, closed);
                Assert.False(form.IsDisposed);
                Assert.True(form.Visible);
                Assert.Equal(bounds, form.Bounds);
                Assert.True(IsWindow(handle));
                Assert.False(overlay.IsDisposed);
                Assert.True(overlay.Visible);
                Assert.True(IsWindow(overlayHandle));
                Assert.Equal(enabledTimers, timers.Select(timer => timer.Enabled));
                Assert.Equal(0, playback.DisposeCount);
                Assert.Equal(0, factory.HostDisposeCount);
                Assert.False(File.Exists(settingsPath));
            }
        });
    }

    [Fact]
    public void ConfirmedEndSession_SavesAndReleasesBeforeFormClosedObservers()
    {
        WithForm((form, factory, playback, directory) =>
        {
            form.Show();
            var overlay = ShowShutdownTestOverlay(form);
            var handle = form.Handle;
            var overlayHandle = overlay.Handle;
            var bounds = form.Bounds;
            var settingsPath = Path.Combine(directory, "SubEdit.Player.Next.json");
            var closed = 0;
            form.FormClosed += (_, e) =>
            {
                closed++;
                Assert.Equal(CloseReason.WindowsShutDown, e.CloseReason);
                Assert.False(form.Visible);
                Assert.True(overlay.IsDisposed);
                Assert.Equal(1, factory.HostDisposeCount);
                Assert.Equal(1, playback.DisposeCount);
                Assert.True(File.Exists(settingsPath));
            };

            Assert.Equal((nint)1, SendMessage(handle, WmQueryEndSession, 0, 0));
            Assert.Equal(1, form.CloseRequests);
            Assert.Equal(0, closed);
            Assert.True(form.Visible);
            Assert.True(overlay.Visible);
            Assert.Equal(0, playback.DisposeCount);
            Assert.False(File.Exists(settingsPath));

            SendMessage(handle, WmEndSession, 1, 0);
            form.Dispose();
            Assert.Equal(1, form.CloseRequests);
            Assert.Equal(1, closed);
            Assert.True(form.IsDisposed);
            Assert.False(IsWindow(handle));
            Assert.False(IsWindow(overlayHandle));
            Assert.Equal(1, factory.HostDisposeCount);
            Assert.Equal(1, playback.DisposeCount);
            var settings = new PortableSettingsStore(directory).Load();
            Assert.Equal(bounds, new Rectangle(settings.Window.Left, settings.Window.Top, settings.Window.Width, settings.Window.Height));
            Assert.Equal(27, settings.Volume);
            var f11 = Message.Create(0, 0x0100, (nint)Keys.F11, 0);
            Assert.False(Application.FilterMessage(ref f11));
            ShowModal(() => Assert.Equal(0, ModalDepth(form)));
        });
    }

    private const uint WmQueryEndSession = 0x0011;
    private const uint WmEndSession = 0x0016;

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint window);

    private static T FormField<T>(Form1 form, string name) => (T)typeof(Form1).GetField(name, Hidden)!.GetValue(form)!;

    private static VideoOverlayForm ShowShutdownTestOverlay(Form1 form)
    {
        var overlay = FormField<VideoOverlayForm>(form, "_videoOverlay");
        if (!overlay.Visible)
        {
            overlay.OsdText = "0:00:00";
            overlay.ShowForVideo(form, new Rectangle(form.Location, new Size(320, 180)));
        }
        Assert.True(overlay.Visible);
        return overlay;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CloseAndDispose_ReleaseResourcesOnceAndDetachThreadHandlers(bool close)
    {
        WithForm((form, factory, playback, directory) =>
        {
            var closed = 0;
            form.FormClosed += (_, _) => closed++;

            if (close)
                form.Close();
            else
                form.Dispose();

            form.Dispose();
            Assert.True(form.IsDisposed);
            Assert.Equal(close ? 1 : 0, form.CloseRequests);
            Assert.Equal(close ? 1 : 0, closed);
            Assert.Equal(close, File.Exists(Path.Combine(directory, "SubEdit.Player.Next.json")));

            var message = Message.Create(IntPtr.Zero, 0x0100, (nint)Keys.F11, 0);
            Assert.False(Application.FilterMessage(ref message));
            Assert.Equal(1, playback.DisposeCount);
            Assert.Equal(1, factory.HostDisposeCount);

            var nextEngine = DispatchProxy.Create<IPlaybackEngine, TestPlayback>();
            var nextFactory = new TestFactory(nextEngine);
            using var next = new TestForm(new AppSettings(), new PortableSettingsStore(directory), nextFactory);
            int oldDepth = -1, nextDepth = -1;
            ShowModal(() =>
            {
                oldDepth = ModalDepth(form);
                nextDepth = ModalDepth(next);
            });

            Assert.Equal(0, oldDepth);
            Assert.Equal(1, nextDepth);
            Assert.Equal(0, ModalDepth(next));
            Assert.False(next.IsHandleCreated);
            next.Close();
            Assert.True(next.IsDisposed);
            Assert.Equal(0, next.CloseRequests);
            Assert.Equal(1, nextFactory.HostDisposeCount);
            Assert.Equal(1, ((TestPlayback)(object)nextEngine).DisposeCount);
            Assert.False(Application.FilterMessage(ref message));
        });
    }

    [Fact]
    public void DisposeDuringModalDialog_DetachesBeforeLeaveThreadModal()
    {
        WithForm((form, _, _, _) =>
        {
            var depthBeforeDispose = -1;
            ShowModal(() =>
            {
                depthBeforeDispose = ModalDepth(form);
                form.Dispose();
            });

            Assert.Equal(1, depthBeforeDispose);
            Assert.True(form.IsDisposed);
            Assert.Equal(depthBeforeDispose, ModalDepth(form));
        });
    }

    [Theory]
    [InlineData("split")]
    [InlineData("merge-active")]
    [InlineData("merge-imported")]
    public void InvalidSrtOperation_PreservesEditorAndFiles(string operation)
    {
        WithForm((form, _, playback, directory) =>
        {
            const string valid = "1\r\n00:00:06,000 --> 00:00:07,000\r\nPoprawny";
            const string draft = "\r\n\r\n2\r\nNiezapisany szkic";
            var activePath = Path.Combine(directory, "Movie_CD1.srt");
            var importedPath = Path.Combine(directory, "Import.srt");
            var importedError = operation == "merge-imported";
            File.WriteAllText(activePath, valid);
            File.WriteAllText(importedPath, valid + (importedError ? draft : ""));
            var beforeFiles = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllBytes);
            typeof(Form1).GetField("_currentVideoPath", Hidden)!.SetValue(form, Path.Combine(directory, "Movie_CD1.mkv"));
            playback.IsMediaOpen = true;
            Assert.True((bool)Invoke("LoadSubtitleFile", activePath, false, false)!);
            var editor = (SubtitleTextEditor)typeof(Form1).GetField("_subtitleEditor", Hidden)!.GetValue(form)!;
            if (!importedError)
                editor.AppendText(draft);
            var beforeText = editor.Text;
            var beforeModified = editor.Modified;

            try
            {
                var warning = CaptureNativeWarning(form, () =>
                {
                    if (operation == "split")
                        Invoke("SplitSubtitles");
                    else
                        Invoke("MergeSubtitleFile", importedPath, 8000L);
                });

                Assert.Contains(importedError ? "dołączanym pliku" : "edytowanych napisach", warning);
                Assert.Contains("linia 6", warning);
                Assert.Contains("Operacja została anulowana, aby zapobiec utracie tekstu.", warning);
                Assert.Equal(beforeText, editor.Text);
                Assert.Equal(beforeModified, editor.Modified);
                Assert.Equal(activePath, typeof(Form1).GetField("_currentSubtitlePath", Hidden)!.GetValue(form));
                Assert.Equal(beforeFiles.Keys.Order(), Directory.GetFiles(directory).Order());
                foreach (var (path, bytes) in beforeFiles)
                    Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                form.Dispose();
            }

            object? Invoke(string name, params object?[] args) =>
                typeof(Form1).GetMethod(name, Hidden)!.Invoke(form, args);
        });
    }

    private static string CaptureNativeWarning(Form owner, Action action)
    {
        var warnings = new List<string>();
        var ownerHandle = owner.Handle;
        DialogHook callback = (code, window, data) =>
        {
            if (code == 5 && window != ownerHandle) // HCBT_ACTIVATE on this STA thread.
            {
                var title = new StringBuilder(256);
                GetWindowText(window, title, title.Capacity);
                if (title.ToString() == "Nieprawidłowy blok SRT")
                {
                    var text = new StringBuilder(512);
                    for (nint child = 0; (child = FindWindowEx(window, child, "Static", null)) != 0;)
                    {
                        if (GetWindowText(child, text, text.Capacity) > 0)
                            warnings.Add(text.ToString());
                    }
                    PostMessage(GetDlgItem(window, 1), 0x00F5, 0, 0); // BM_CLICK on IDOK.
                }
                else
                {
                    PostMessage(window, 0x0010, 0, 0); // Close an unexpected dialog and let the assertion fail.
                }
            }
            return CallNextHookEx(0, code, window, data);
        };

        var hook = SetWindowsHookEx(5, callback, 0, GetCurrentThreadId());
        Assert.NotEqual(IntPtr.Zero, hook);
        try { action(); }
        finally { UnhookWindowsHookEx(hook); GC.KeepAlive(callback); }
        return Assert.Single(warnings);
    }

    private delegate nint DialogHook(int code, nint window, nint data);
    [DllImport("user32.dll")] private static extern nint SetWindowsHookEx(int id, DialogHook callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint window, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint child, string className, string? title);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint window, int id);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [Fact]
    public void EditorOptions_ResetTemporaryZoomOnlyWhenBaseFontSizeChanges()
    {
        WithForm((form, _, _, directory) =>
        {
            form.Show();
            var editor = FormField<SubtitleTextEditor>(form, "_subtitleEditor");
            var store = new PortableSettingsStore(directory);
            const int defaultStyle = ScintillaNET.Style.Default;
            var unzoomedHeight = editor.Lines[0].Height;
            var originalDpi = editor.DeviceDpi;
            AssertEditor(10, 0);

            for (var notch = 0; notch < 4; notch++)
            {
                var point = editor.PointToScreen(new Point(10, 10));
                var coordinates = (point.Y & 0xffff) << 16 | (point.X & 0xffff);
                SendMessage(editor.Handle, 0x020A, (120 << 16) | 0x0008, coordinates); // WM_MOUSEWHEEL + MK_CONTROL
            }
            AssertEditor(10, 4);
            var zoomedHeight = editor.Lines[0].Height;
            Assert.True(zoomedHeight > unzoomedHeight);

            Options(10, dialog => FontSize(dialog).Value = 16, DialogActionRole.Cancel);
            AssertEditor(10, 4);
            Assert.Equal(10, store.Load().Editor.FontSize);
            Options(10, _ => { }, DialogActionRole.Ok);
            AssertEditor(10, 4);
            Assert.Equal(10, store.Load().Editor.FontSize);
            var autoSave = store.Load().Editor.AutoSaveEnabled;
            Options(10, dialog => dialog.AutoSave.Checked = !autoSave, DialogActionRole.Ok);
            AssertEditor(10, 4);
            Assert.Equal(!autoSave, store.Load().Editor.AutoSaveEnabled);

            Options(10, dialog =>
            {
                FontSize(dialog).Value = 12;
                dialog.LayoutView.Actions.Button(DialogActionRole.Apply).PerformClick();
                AssertEditor(12, 0);
                Assert.InRange(editor.Lines[0].Height, unzoomedHeight + 1, zoomedHeight - 1);
                Assert.Equal(12, store.Load().Editor.FontSize);
                editor.Zoom = -2;
            }, DialogActionRole.Ok);
            AssertEditor(12, -2);

            Options(12, dialog => FontSize(dialog).Value = 14, DialogActionRole.Ok);
            AssertEditor(14, 0);
            Assert.Equal(originalDpi, editor.DeviceDpi);
            Assert.Equal(zoomedHeight, editor.Lines[0].Height);

            var initialBounds = form.Bounds;
            editor.Zoom = 3;
            foreach (var screen in Screen.AllScreens)
            {
                form.Location = new Point(screen.WorkingArea.Left + 40, screen.WorkingArea.Top + 40);
                SubtitleDialogLayoutTests.Pump();
                Assert.Equal(screen.DeviceName, Screen.FromControl(editor).DeviceName);
                Assert.Equal(form.DeviceDpi, editor.DeviceDpi);
                Options(14, dialog => Assert.Equal(form.DeviceDpi, dialog.DeviceDpi), DialogActionRole.Ok);
                AssertEditor(14, 3);
            }
            form.Bounds = initialBounds;
            SubtitleDialogLayoutTests.Pump();
            Assert.Equal(originalDpi, editor.DeviceDpi);
            Options(14, dialog => FontSize(dialog).Value = 10, DialogActionRole.Ok);
            AssertEditor(10, 0);
            Assert.Equal(unzoomedHeight, editor.Lines[0].Height);

            Options(10, dialog => FontSize(dialog).Value = 12, DialogActionRole.Ok);
            editor.Zoom = 4;
            form.Close();
            var saved = store.Load();
            Assert.Equal(12, saved.Editor.FontSize);
            var engine = DispatchProxy.Create<IPlaybackEngine, TestPlayback>();
            using var restarted = new TestForm(saved, store, new TestFactory(engine));
            var restartedEditor = FormField<SubtitleTextEditor>(restarted, "_subtitleEditor");
            Assert.Equal(12, restartedEditor.Styles[defaultStyle].Size);
            Assert.Equal(0, restartedEditor.Zoom);

            void AssertEditor(int size, int zoom)
            {
                Assert.Equal(size, editor.Styles[defaultStyle].Size);
                Assert.Equal(zoom, editor.Zoom);
            }

            void Options(int expectedSize, Action<EditorOptionsDialog> edit, DialogActionRole close)
            {
                ExceptionDispatchInfo? failure = null;
                form.BeginInvoke(() =>
                {
                    var dialog = Application.OpenForms.OfType<EditorOptionsDialog>().Single();
                    try
                    {
                        Assert.Equal(expectedSize, FontSize(dialog).Value);
                        edit(dialog);
                        dialog.LayoutView.Actions.Button(close).PerformClick();
                    }
                    catch (Exception exception)
                    {
                        failure = ExceptionDispatchInfo.Capture(exception);
                        dialog.DialogResult = DialogResult.Cancel;
                    }
                });
                typeof(Form1).GetMethod("ShowEditorOptionsDialog", Hidden)!.Invoke(form, null);
                failure?.Throw();
            }

            static NumericUpDown FontSize(EditorOptionsDialog dialog) =>
                SubtitleDialogLayoutTests.Descendants(dialog.LayoutView.Pages[0].Page).OfType<NumericUpDown>()
                    .Single(control => control.Minimum == 6 && control.Maximum == 22);
        });
    }

    private static int ModalDepth(Form1 form) => (int)typeof(Form1).GetField("_modalDialogDepth", Hidden)!.GetValue(form)!;

    private static void ShowModal(Action whileModal)
    {
        using var dialog = new Form();
        ExceptionDispatchInfo? failure = null;
        dialog.Shown += (_, _) =>
        {
            try { whileModal(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
            finally { dialog.Close(); }
        };
        dialog.ShowDialog();
        failure?.Throw();
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
            finally
            {
                Application.ExitThread();
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
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
        public int DisposeCount;
        public bool Disposed => DisposeCount != 0;
        public bool IsMediaOpen;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case "Dispose": DisposeCount++; return null;
                case "get_IsMediaOpen": return IsMediaOpen;
                case "get_DurationMilliseconds": return 5000L;
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
        internal int HostDisposeCount;
        internal bool HostDisposed => HostDisposeCount != 0;
        internal int ZoomPercent;
        public IPlaybackEngine CreateEngine(Control? dispatcher, int initialVolume, Func<VideoDecodingMode> mode) => engine;
        public IVideoHost CreateVideoHost(Panel container, IPlaybackEngine playback, ContextMenuStrip context)
        {
            Menu = context;
            return new TestHost(container, value => ZoomPercent = value, () => HostDisposeCount++);
        }
    }

    private sealed class TestHost(Control control, Action<int> zoom, Action dispose) : IVideoHost
    {
        public event EventHandler? VideoResized { add { } remove { } }
        public Control HostControl => control;
        public void ApplyVerticalOffset(int value) { }
        public void ApplyHorizontalOffset(int value) { }
        public void ApplyZoom(int value) => zoom(value);
        public void ShowContextMenuCentered() { }
        public void Focus() { }
        public void RegisterFileDrop(DragEventHandler enter, DragEventHandler drop) { }
        public void Dispose() => dispose();
    }
}
