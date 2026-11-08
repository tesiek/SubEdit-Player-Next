using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using SubEdit.Player.WinForms.Playback;
using Xunit;
using Xunit.Abstractions;

namespace SubEdit.Player.Tests;

[Collection("Subtitle dialog layout")]
public class MenuDpiIntegrationTests(ITestOutputHelper output)
{
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Message Dispatch(Control control, int message, int value)
    {
        var args = new object[] { Message.Create(control.Handle, message, (IntPtr)value, IntPtr.Zero) };
        typeof(Control).GetMethod("WndProc", Hidden)!.Invoke(control, args);
        return (Message)args[0];
    }

    private static IEnumerable<ToolStripItem> Tree(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            yield return item;
            if (item is ToolStripMenuItem mi)
                foreach (var child in Tree(mi.DropDownItems)) yield return child;
        }
    }

    [Fact]
    public void ProductionMenu_RoundTripsDpiWithoutChangingCommandsOrAccumulatingSizes() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, settings) =>
    {
        var menu = Assert.IsType<ActivatingMenuStrip>(form.MainMenuStrip);
        _ = menu.Handle;
        var items = Tree(menu.Items).ToArray();
        // Native HWNDs stay at 96 DPI in this synthetic test. Create them first,
        // so a late native creation cannot overwrite the simulated managed DPI.
        foreach (var item in items.OfType<ToolStripMenuItem>())
            if (item.HasDropDownItems) _ = item.DropDown.Handle;
        var metadata = items.OfType<ToolStripMenuItem>().Select(i => (i.Text, i.ShortcutKeys, i.Checked, i.Enabled)).ToArray();
        var font = menu.Font;
        var size = menu.GetPreferredSize(Size.Empty);
        foreach (var dpi in new[] { 120, 96, 120, 96, 120, 96 })
        {
            // This exercises the real WM handler, not physical monitor transitions.
            Dispatch(menu, 0x02E2, dpi);
            Assert.True(dpi == menu.DeviceDpi, $"Menu: expected {dpi}, actual {menu.DeviceDpi}");
            foreach (var root in menu.Items.OfType<ToolStripMenuItem>())
            {
                Assert.True(dpi == root.DropDown.DeviceDpi, $"Dropdown {root.Text}: expected {dpi}, actual {root.DropDown.DeviceDpi}; menu={menu.DeviceDpi}");
                Assert.Equal(new Size(dpi == 96 ? 16 : 20, dpi == 96 ? 16 : 20), root.DropDown.ImageScalingSize);
            }
            var stableSize = menu.GetPreferredSize(Size.Empty);
            var stableFont = menu.Font;
            Dispatch(menu, 0x02E2, dpi);
            Assert.Equal(stableFont, menu.Font);
            Assert.Equal(stableSize, menu.GetPreferredSize(Size.Empty));
            Assert.Null(typeof(ActivatingMenuStrip).GetField("_messageOldDpi", Hidden)!.GetValue(menu));
        }
        Assert.Equal(font, menu.Font);
        Assert.Equal(size, menu.GetPreferredSize(Size.Empty));
        Assert.Equal(metadata, items.OfType<ToolStripMenuItem>().Select(i => (i.Text, i.ShortcutKeys, i.Checked, i.Enabled)).ToArray());
        Assert.Equal((IntPtr)1, Dispatch(menu, 0x0021, 0).Result);
    }));

    [Fact]
    public void ValidFrameworkArguments_AreUnchanged_AndMessageContextIsRestored() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var dpiScope = new DpiScope();
        using var actual = new ActivatingMenuStrip();
        using var native = new MenuStrip();
        actual.Items.Add(new ToolStripMenuItem("Plik")); native.Items.Add(new ToolStripMenuItem("Plik"));
        var context = typeof(ActivatingMenuStrip).GetField("_messageOldDpi", Hidden)!;
        context.SetValue(actual, 72);
        foreach (var pair in new[] { (96, 120), (120, 96) })
        {
            typeof(ActivatingMenuStrip).GetMethod("RescaleConstantsForDpi", Hidden)!.Invoke(actual, [pair.Item1, pair.Item2]);
            typeof(ToolStrip).GetMethod("RescaleConstantsForDpi", Hidden)!.Invoke(native, [pair.Item1, pair.Item2]);
            Assert.Equal(native.Font, actual.Font);
            Assert.Equal(native.ImageScalingSize, actual.ImageScalingSize);
            Assert.Equal(native.GetPreferredSize(Size.Empty), actual.GetPreferredSize(Size.Empty));
        }
        _ = actual.Handle;
        context.SetValue(actual, 72);
        Dispatch(actual, 0x02E2, 120);
        Assert.Equal(72, context.GetValue(actual));
    });

    [Fact]
    public void HistoryDropdownKeyboardAndRepeatedClose_RemainSafe() => SubtitleDialogLayoutTests.Sta(() =>
    {
        void Cycle()
        {
            WithForm((form, settings) =>
        {
            var menu = form.MainMenuStrip!;
            var history = Tree(menu.Items).OfType<ToolStripMenuItem>().Single(i => i.Text == "Ostatnio otwierane pliki");
            Assert.Collection(history.DropDownItems.Cast<ToolStripItem>(),
                item => { Assert.Equal("Brak historii", item.Text); Assert.False(item.Enabled); },
                item => Assert.IsType<ToolStripSeparator>(item),
                item => { Assert.Equal("Wyczyść historię", item.Text); Assert.False(item.Enabled); });
            settings.Playback.RecentVideoPaths.Add("menu-dpi-example.mkv");
            typeof(Form1).GetMethod("PopulateRecentFilesMenu", Hidden)!.Invoke(form, [history]);
            Assert.Contains(history.DropDownItems.OfType<ToolStripMenuItem>(), i => i.Text!.Contains("menu-dpi-example"));
            form.Show();
            var file = (ToolStripMenuItem)menu.Items[0];
            file.ShowDropDown();
            Assert.True(file.DropDown.Visible);
            file.HideDropDown();
            Assert.False(file.DropDown.Visible);
            Assert.Contains(Tree(menu.Items).OfType<ToolStripMenuItem>(), i => i.ShortcutKeys == (Keys.Control | Keys.O));
            Dispatch(menu, 0x02E2, 120);
            Dispatch(menu, 0x02E2, 96);
            });
            SubtitleDialogLayoutTests.Pump();
        }
        for (var i = 0; i < 12; i++) Cycle();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        GC.Collect(); GC.WaitForPendingFinalizers();
        var gdi = GetGuiResources(process.Handle, 0); var user = GetGuiResources(process.Handle, 1);
        for (var i = 0; i < 8; i++) Cycle();
        GC.Collect(); GC.WaitForPendingFinalizers();
        Assert.True(gdi > 0 && user > 0);
        Assert.InRange(GetGuiResources(process.Handle, 0), 1u, gdi + 2);
        Assert.InRange(GetGuiResources(process.Handle, 1), 1u, user + 2);
        output.WriteLine($"GDI {gdi}->{GetGuiResources(process.Handle, 0)}, USER {user}->{GetGuiResources(process.Handle, 1)}; 12 warmup + 8 measured Form1 cycles.");
    });

    [Fact]
    public void MainMenu_UsesApprovedSearchAndHelpGroupsWithoutEllipses() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, _) =>
    {
        var menu = form.MainMenuStrip!;
        var edit = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Edycja");
        Assert.Equal(
            new[] { "Znajdź tekst", "Znajdź i zamień", "Znajdź następny", "Znajdź poprzedni", "Wyszukiwanie specjalne" },
            edit.DropDownItems.Cast<ToolStripItem>().Skip(7).Take(5).Select(item => item.Text));
        var help = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Pomoc");
        Assert.Collection(help.DropDownItems.Cast<ToolStripItem>(),
            item => Assert.Equal("Tematy pomocy", item.Text),
            item => Assert.Equal("Skróty klawiszowe", item.Text),
            item => Assert.Equal("FAQ: edycja napisów", item.Text),
            item => Assert.Equal("SubEdit-Player Next — GitHub", item.Text),
            item => Assert.IsType<ToolStripSeparator>(item),
            item => Assert.Equal("Diagnostyka programu", item.Text),
            item => Assert.Equal("O programie", item.Text));

        Assert.All(Tree(menu.Items).OfType<ToolStripMenuItem>(), item =>
        {
            var label = item.Text?.TrimEnd() ?? string.Empty;
            Assert.False(label.EndsWith("...", StringComparison.Ordinal) || label.EndsWith('…'), label);
        });
    }));

    [Fact]
    public void MainMenu_UsesApprovedGroupsAndRetainsSubmenus() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, settings) =>
    {
        var items = form.MainMenuStrip!.Items;
        Assert.Equal(new[] { "Plik", "Edycja", "Widok", "Napisy", "Film", "Ustawienia", "Pomoc" },
            items.Cast<ToolStripItem>().Select(item => item.Text));

        static ToolStripMenuItem Submenu(ToolStripItemCollection parent, string text) =>
            parent.OfType<ToolStripMenuItem>().Single(item => item.Text == text);

        static void AssertItems(ToolStripMenuItem menu, params string[] labels) =>
            Assert.Equal(labels, menu.DropDownItems.Cast<ToolStripItem>()
                .Select(item => item is ToolStripSeparator ? "-" : item.Text));

        AssertItems(Submenu(items, "Plik"),
            "Otwórz plik wideo", "Zamknij plik wideo", "Właściwości pliku wideo", "-",
            "Nowe napisy", "Otwórz plik napisów", "Dopisz napisy z pliku", "Zamknij plik napisów", "-",
            "Zapisz napisy", "Zapisz napisy jako", "Eksportuj napisy do SRT", "-",
            "Podziel plik napisów (film w kilku częściach)", "Dołącz plik napisów (film w jednej części)", "-",
            "Ostatnio otwierane pliki", "-", "Zakończ");
        AssertItems(Submenu(items, "Edycja"),
            "Cofnij", "Ponów", "-", "Wytnij", "Kopiuj", "Wklej", "-",
            "Znajdź tekst", "Znajdź i zamień", "Znajdź następny", "Znajdź poprzedni", "Wyszukiwanie specjalne", "-",
            "Kursywa", "Podkreślenie", "Pogrubienie", "-", "Konwertuj znaczniki kursywy");

        var view = Submenu(items, "Widok");
        AssertItems(view, "Edytor napisów", "Wyświetlaj napisy", "Przezroczyste napisy", "Wyświetlaj czas filmu", "-",
            "Przesunięcie obrazu", "Powiększenie", "Pomniejszenie", "Resetuj transformacje", "-",
            "Zawsze na wierzchu", "Tryb pełnoekranowy");
        AssertItems(Submenu(view.DropDownItems, "Przesunięcie obrazu"), "W górę", "W dół", "W lewo", "W prawo");
        AssertItems(Submenu(view.DropDownItems, "Powiększenie"));
        AssertItems(Submenu(view.DropDownItems, "Pomniejszenie"));

        var subtitles = Submenu(items, "Napisy");
        AssertItems(subtitles, "Przejdź do napisu", "-", "Przesuń czas napisów", "Synchronizacja napisów",
            "Zmień czas wyświetlania napisów", "Zmień współczynnik FPS napisów",
            "Dodaj pół sekundy do czasu napisu", "Odejmij pół sekundy od czasu napisu", "-",
            "Połącz szybko znikające napisy", "Podziel długie wiersze", "Ogranicz ilość wierszy w linii",
            "Popraw napisy", "Sortuj napisy", "-", "Konwertuj format napisów", "Kodowanie napisów");
        AssertItems(Submenu(subtitles.DropDownItems, "Synchronizacja napisów"), "Zmierz opóźnienie napisu względem dźwięku", "-",
            "Zmierz początkowe opóźnienie napisu", "Zmierz końcowe opóźnienie napisu", "Geometryczne przesunięcie napisów");
        AssertItems(Submenu(subtitles.DropDownItems, "Kodowanie napisów"), "Bieżące kodowanie: UTF-8",
            "-", "Konwertuj na ANSI", "Konwertuj na UTF-8",
            "Konwertuj na UTF-8-BOM", "Konwertuj na UTF-16 BE BOM", "Konwertuj na UTF-16 LE BOM");

        var film = Submenu(items, "Film");
        AssertItems(film, "Przejdź do czasu filmu", "Przewijanie", "-", "Pauza / Odtwarzanie", "Stop",
            "Odtwarzaj film od bieżącego napisu", "Powtarzaj odtwarzanie", "Prędkość odtwarzania", "-",
            "Wstaw czas początkowy napisu", "Wstaw czas końcowy napisu", "Przejdź do napisu w bieżącym czasie filmu",
            "Śledzenie napisów w edytorze", "-", "Wczytaj poprzedni plik", "Wczytaj następny plik", "Filmy w aktualnym katalogu");
        AssertItems(Submenu(film.DropDownItems, "Przewijanie"), "Przewiń 5 sekund w przód", "Przewiń 5 sekund wstecz",
            "Przewiń w przód o własny krok", "Przewiń wstecz o własny krok", "Przejdź na początek filmu", "Przejdź na koniec filmu");
        AssertItems(Submenu(film.DropDownItems, "Prędkość odtwarzania"), "Przyspiesz", "Normalna prędkość", "Zwolnij");
        AssertItems(Submenu(film.DropDownItems, "Filmy w aktualnym katalogu"), "Odtwarzaj wszystkie filmy w katalogu", "-",
            "Sortowanie plików: Nazwa", "Sortowanie plików: Rozszerzenie", "Sortowanie plików: Losowo");

        var configuration = Submenu(items, "Ustawienia");
        settings.ActiveSubtitleFormat = "MicroDvd";
        typeof(Form1).GetMethod("UpdateActiveSubtitleFormatIndicator", Hidden)!.Invoke(form, null);
        AssertItems(configuration, "Aktywny format napisów: {000}{000}", "Aktywny współczynnik FPS", "-", "Opcje");
        Assert.Equal(Keys.Control | Keys.Oemtilde, Submenu(configuration.DropDownItems, "Aktywny format napisów: {000}{000}").ShortcutKeys);
        Assert.Equal(Keys.Control | Keys.Shift | Keys.Q, Submenu(configuration.DropDownItems, "Aktywny współczynnik FPS").ShortcutKeys);
        Assert.Equal(Keys.F12, Submenu(configuration.DropDownItems, "Opcje").ShortcutKeys);

        var commands = Tree(items).OfType<ToolStripMenuItem>().ToArray();
        Assert.DoesNotContain(commands.GroupBy(item => item.Text), group => group.Count() > 1);
        Assert.DoesNotContain(commands, item => item.ShortcutKeys == (Keys.Control | Keys.Oem5));
    }));

    [Fact]
    public void MenuAvailability_FollowsUndoHistoryAndMediaLifecycle() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, _) =>
    {
        form.Show();
        SubtitleDialogLayoutTests.Pump();
        var menu = form.MainMenuStrip!;
        var editor = (SubtitleTextEditor)typeof(Form1).GetField("_subtitleEditor", Hidden)!.GetValue(form)!;
        var edit = MenuItem(menu.Items, "Edycja");
        var undo = MenuItem(edit.DropDownItems, "Cofnij");
        var redo = MenuItem(edit.DropDownItems, "Ponów");
        var special = MenuItem(edit.DropDownItems, "Wyszukiwanie specjalne");
        var findShort = MenuItem(special.DropDownItems, "Znajdź krótko wyświetlany napis");
        var findNext = MenuItem(edit.DropDownItems, "Znajdź następny");
        var film = MenuItem(menu.Items, "Film");
        var seek = MenuItem(film.DropDownItems, "Przewijanie");
        var playFromSubtitle = MenuItem(film.DropDownItems, "Odtwarzaj film od bieżącego napisu");
        var insert = MenuItem(film.DropDownItems, "Wstaw czas początkowy napisu");
        var trace = MenuItem(film.DropDownItems, "Śledzenie napisów w edytorze");
        var file = MenuItem(menu.Items, "Plik");
        var closeVideo = MenuItem(file.DropDownItems, "Zamknij plik wideo");
        var properties = MenuItem(file.DropDownItems, "Właściwości pliku wideo");
        Assert.Equal(Keys.F9, properties.ShortcutKeys);
        var goToSubtitle = MenuItem(MenuItem(menu.Items, "Napisy").DropDownItems, "Przejdź do napisu");
        var toolbar = (ToolStrip)typeof(Form1).GetField("_mainToolbar", Hidden)!.GetValue(form)!;

        Assert.False(undo.Enabled);
        Assert.False(redo.Enabled);
        Refresh(edit);
        Assert.False(findShort.Enabled);
        Assert.False(findNext.Enabled);
        Assert.False(goToSubtitle.Enabled);
        AssertFilmAvailability(false);

        editor.AddText("{10}{20}Zażółć gęślą jaźń");
        Refresh(edit);
        Assert.True(undo.Enabled);
        Assert.True(findShort.Enabled);
        Assert.True(goToSubtitle.Enabled);
        undo.PerformClick();
        Assert.False(undo.Enabled);
        Assert.True(redo.Enabled);
        toolbar.Items["toolbarRedo"]!.PerformClick();
        Assert.True(undo.Enabled);
        Assert.False(redo.Enabled);
        editor.Focus();
        var undoMessage = Message.Create(form.Handle, 0x0100, (IntPtr)Keys.Z, IntPtr.Zero);
        Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, [undoMessage, Keys.Control | Keys.Z])!);
        Assert.False(undo.Enabled);
        redo.PerformClick();

        var path = Path.GetTempFileName();
        var subtitlePath = Path.GetTempFileName();
        try
        {
            Assert.True((bool)typeof(Form1).GetMethod("TrySaveSubtitleToPath", Hidden)!.Invoke(form, [subtitlePath])!);
            Assert.True((bool)typeof(Form1).GetMethod("LoadVideoFile", Hidden)!.Invoke(form, [path, null, subtitlePath])!);
            AssertFilmAvailability(true);
            Assert.True(trace.Enabled);
            trace.PerformClick();
            Assert.True(trace.Checked);
            Assert.True(((ToolStripButton)toolbar.Items["toolbarSubtitleTrace"]!).Checked);
            toolbar.Items["toolbarSubtitleTrace"]!.PerformClick();
            Assert.False(trace.Checked);
            closeVideo.PerformClick();
            AssertFilmAvailability(false);
            Assert.False(trace.Enabled);
            Assert.False(trace.Checked);
            Assert.True(goToSubtitle.Enabled);
            typeof(Form1).GetMethod("ClearSubtitles", Hidden)!.Invoke(form, null);
            Assert.False(goToSubtitle.Enabled);
            Assert.False(undo.Enabled);
        }
        finally
        {
            File.Delete(path);
            File.Delete(subtitlePath);
        }

        void AssertFilmAvailability(bool expected)
        {
            Assert.Equal(expected, properties.Enabled);
            Refresh(file);
            Assert.Equal(expected, properties.Enabled);
            Refresh(film);
            Assert.Equal(expected, closeVideo.Enabled);
            Assert.Equal(expected, seek.Enabled);
            Assert.Equal(expected, playFromSubtitle.Enabled);
            Assert.Equal(expected, insert.Enabled);
        }
    }));

    [Theory]
    [InlineData("Ansi", "ANSI")]
    [InlineData("Utf8", "UTF-8")]
    [InlineData("Utf8Bom", "UTF-8-BOM")]
    [InlineData("Utf16BigEndianBom", "UTF-16 BE BOM")]
    [InlineData("Utf16LittleEndianBom", "UTF-16 LE BOM")]
    public void EncodingMenu_ReportsDocumentEncodingAndRoundTripsConversion(string kind, string label) =>
        SubtitleDialogLayoutTests.Sta(() => WithForm((form, _) =>
        {
            form.Show();
            SubtitleDialogLayoutTests.Pump();
            var subtitles = MenuItem(form.MainMenuStrip!.Items, "Napisy");
            var encoding = MenuItem(subtitles.DropDownItems, "Kodowanie napisów");
            var indicator = (ToolStripMenuItem)encoding.DropDownItems[0];
            var target = Enum.Parse<SubtitleEncodingKind>(kind);
            var source = target == SubtitleEncodingKind.Ansi ? SubtitleEncodingKind.Utf8 : SubtitleEncodingKind.Ansi;
            const string content = "{10}{20}Zażółć gęślą jaźń";
            var path = Path.GetTempFileName();
            try
            {
                Refresh(encoding);
                Assert.Equal("Bieżące kodowanie: UTF-8", indicator.Text);
                Assert.False(indicator.Enabled);
                Assert.False(indicator.Checked);
                Assert.IsType<ToolStripSeparator>(encoding.DropDownItems[1]);

                SubtitleFileWriter.Write(path, content, SubtitleFileReader.CreateEncoding(source));
                var before = File.ReadAllBytes(path);
                Load();
                Refresh(encoding);
                Assert.Equal(source == SubtitleEncodingKind.Ansi ? "Bieżące kodowanie: ANSI" : "Bieżące kodowanie: UTF-8", indicator.Text);

                var conversion = MenuItem(encoding.DropDownItems, $"Konwertuj na {label}");
                Assert.True(conversion.Enabled);
                conversion.PerformClick();
                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.True((bool)typeof(Form1).GetField("_isSubtitleDirty", Hidden)!.GetValue(form)!);
                Refresh(encoding);
                Assert.Equal($"Bieżące kodowanie: {label}", indicator.Text);
                Assert.False(conversion.Enabled);

                Assert.True((bool)typeof(Form1).GetMethod("TrySaveSubtitle", Hidden)!.Invoke(form, null)!);
                var saved = SubtitleFileReader.Read(path);
                Assert.Equal(target, SubtitleFileReader.GetEncodingKind(saved.Encoding));
                Assert.Equal(content, saved.Text);
                Assert.False((bool)typeof(Form1).GetField("_isSubtitleDirty", Hidden)!.GetValue(form)!);

                typeof(Form1).GetMethod("CloseSubtitles", Hidden)!.Invoke(form, null);
                Refresh(encoding);
                Assert.Equal("Bieżące kodowanie: UTF-8", indicator.Text);
                Load();
                Refresh(encoding);
                Assert.Equal($"Bieżące kodowanie: {label}", indicator.Text);
            }
            finally
            {
                File.Delete(path);
            }

            void Load() => Assert.True((bool)typeof(Form1).GetMethod("LoadSubtitleFile", Hidden)!.Invoke(form, [path, false, false])!);
        }));

    private static ToolStripMenuItem MenuItem(ToolStripItemCollection items, string label) =>
        items.OfType<ToolStripMenuItem>().Single(item => item.Text == label);

    private static void Refresh(ToolStripMenuItem menu)
    {
        menu.ShowDropDown();
        menu.HideDropDown();
    }

    [Fact]
    public void ViewCommandsAndShortcuts_WorkInWindowAndFullScreen() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, settings) =>
    {
        form.Show();
        var menu = form.MainMenuStrip!;
        var view = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Widok");
        ToolStripMenuItem Item(string label) => view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == label);
        var zoomIn = Item("Powiększenie");
        var zoomOut = Item("Pomniejszenie");
        var subtitles = Item("Wyświetlaj napisy");
        var transparency = Item("Przezroczyste napisy");
        var time = Item("Wyświetlaj czas filmu");
        var zoom = typeof(Form1).GetField("_movieZoomPercent", Hidden)!;
        var modifiers = Keys.Control | Keys.Alt;
        Assert.Equal(modifiers | Keys.Oemplus, zoomIn.ShortcutKeys);
        Assert.Equal(modifiers | Keys.OemMinus, zoomOut.ShortcutKeys);
        Assert.Equal(Keys.Control | Keys.Shift | Keys.N, subtitles.ShortcutKeys);
        Assert.Equal("M", transparency.ShortcutKeyDisplayString);
        Assert.Equal("T", time.ShortcutKeyDisplayString);

        void Press(Keys key)
        {
            var message = Message.Create(form.Handle, 0x0100, (IntPtr)(key & Keys.KeyCode), IntPtr.Zero);
            Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, [message, key])!);
        }

        Press(Keys.Control | Keys.Oem5);
        Assert.Equal(FormWindowState.Minimized, form.WindowState);
        Press(Keys.Control | Keys.Oem5);
        Assert.Equal(FormWindowState.Normal, form.WindowState);

        view.ShowDropDown();
        Assert.False(zoomIn.Enabled);
        Assert.False(zoomOut.Enabled);
        view.HideDropDown();
        var engine = (IPlaybackEngine)typeof(Form1).GetField("_playback", Hidden)!.GetValue(form)!;
        ((WindowTitleTests.TitlePlayback)(object)engine).IsOpen = true;
        view.ShowDropDown();
        Assert.True(zoomIn.Enabled);
        Assert.True(zoomOut.Enabled);
        view.HideDropDown();

        zoomIn.PerformClick();
        Assert.Equal(110, zoom.GetValue(form));
        zoomOut.PerformClick();
        Assert.Equal(100, zoom.GetValue(form));
        time.PerformClick();
        Assert.Equal(TimeDisplayMode.Remaining, settings.TimeDisplayMode);

        Item("Tryb pełnoekranowy").PerformClick();
        try
        {
            Assert.False(menu.Visible);
            Press(modifiers | Keys.Add);
            Assert.Equal(110, zoom.GetValue(form));
            Press(modifiers | Keys.Subtract);
            Assert.Equal(100, zoom.GetValue(form));
            var visible = subtitles.Checked;
            var transparent = transparency.Checked;
            Press(Keys.Control | Keys.Shift | Keys.N);
            Assert.Equal(!visible, subtitles.Checked);
            Press(Keys.M);
            Assert.Equal(!transparent, transparency.Checked);
            Press(Keys.T);
            Assert.Equal(TimeDisplayMode.Hidden, settings.TimeDisplayMode);
            Assert.False(time.Checked);
            Press(Keys.T);
            Assert.Equal(TimeDisplayMode.Elapsed, settings.TimeDisplayMode);
            Assert.True(time.Checked);
        }
        finally
        {
            Press(Keys.F11);
        }
        Assert.True(menu.Visible);
    }));

    [Fact]
    public void ViewChecks_TrackMenuToolbarShortcutsAndFullScreen() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, _) =>
    {
        var menu = form.MainMenuStrip!;
        var view = menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Widok");
        var editor = view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Edytor napisów");
        var alwaysOnTop = view.DropDownItems.OfType<ToolStripMenuItem>().Single(item => item.Text == "Zawsze na wierzchu");
        var fullScreen = MenuItem(view.DropDownItems, "Tryb pełnoekranowy");
        var toolbar = (ToolStrip)typeof(Form1).GetField("_mainToolbar", Hidden)!.GetValue(form)!;
        var split = (SplitContainer)typeof(Form1).GetField("_splitContainer", Hidden)!.GetValue(form)!;

        Assert.True(editor.Checked);
        Assert.False(alwaysOnTop.Checked);
        form.Show();
        SubtitleDialogLayoutTests.Pump();

        AssertToggle(editor.PerformClick, true, () => editor.Checked, () => !split.Panel2Collapsed);
        AssertToggle(toolbar.Items["toolbarEditor"]!.PerformClick, true, () => editor.Checked, () => !split.Panel2Collapsed);
        AssertToggle(() => Press(Keys.Control | Keys.E), true, () => editor.Checked, () => !split.Panel2Collapsed);
        AssertToggle(alwaysOnTop.PerformClick, false, () => alwaysOnTop.Checked, () => form.TopMost);
        AssertToggle(toolbar.Items["toolbarAlwaysOnTop"]!.PerformClick, false, () => alwaysOnTop.Checked, () => form.TopMost);
        AssertToggle(() => Press(Keys.F6), false, () => alwaysOnTop.Checked, () => form.TopMost);

        // Other editor commands reveal Panel2 directly, without using ToggleEditor.
        split.Panel2Collapsed = true;
        Assert.False(editor.Checked);
        split.Panel2Collapsed = false;
        Assert.True(editor.Checked);

        Press(Keys.F6);
        Press(Keys.F11);
        try
        {
            Assert.False(editor.Checked);
            Assert.True(alwaysOnTop.Checked);
            Assert.True(fullScreen.Checked);
        }
        finally
        {
            Press(Keys.F11);
        }
        Assert.True(editor.Checked);
        Assert.True(alwaysOnTop.Checked);
        Assert.False(fullScreen.Checked);

        // Refresh from the actual state when the dropdown is reopened.
        form.TopMost = false;
        view.ShowDropDown();
        Assert.True(view.DropDown.Visible);
        Assert.True(editor.Checked);
        Assert.False(alwaysOnTop.Checked);
        view.HideDropDown();

        void Press(Keys key)
        {
            var message = Message.Create(form.Handle, 0x0100, (IntPtr)(key & Keys.KeyCode), IntPtr.Zero);
            Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, [message, key])!);
        }

        static void AssertToggle(Action toggle, bool initial, Func<bool> check, Func<bool> state)
        {
            foreach (var expected in new[] { !initial, initial })
            {
                toggle();
                Assert.Equal(expected, state());
                Assert.Equal(expected, check());
            }
        }
    }));

    [Fact]
    public void KeyboardCommandAndEscape_KeepProductionMenuUsable() => SubtitleDialogLayoutTests.Sta(() => WithForm((form, settings) =>
    {
        form.Show();
        var split = (SplitContainer)typeof(Form1).GetField("_splitContainer", Hidden)!.GetValue(form)!;
        var initial = split.Panel2Collapsed;
        for (var i = 0; i < 2; i++)
        {
            var args = new object[] { Message.Create(form.Handle, 0x0100, (IntPtr)Keys.E, IntPtr.Zero), Keys.Control | Keys.E };
            Assert.True((bool)typeof(Form1).GetMethod("ProcessCmdKey", Hidden)!.Invoke(form, args)!);
            Assert.Equal(i == 0 ? !initial : initial, split.Panel2Collapsed);
        }
        var menu = form.MainMenuStrip!;
        var first = (ToolStripMenuItem)menu.Items[0];
        first.ShowDropDown();
        Assert.True(first.DropDown.Visible);
        Assert.True((bool)typeof(ToolStrip).GetMethod("ProcessDialogKey", Hidden)!.Invoke(first.DropDown, [Keys.Escape])!);
        Assert.False(first.DropDown.Visible);
        Assert.False(form.IsDisposed);
    }));

    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    private sealed class DpiScope : IDisposable
    {
        private readonly IntPtr _previous = SetThreadDpiAwarenessContext((IntPtr)(-4));
        public void Dispose() => SetThreadDpiAwarenessContext(_previous);
    }
    private static void WithForm(Action<Form1, AppSettings> action)
    {
        using var dpiScope = new DpiScope();
        var directory = Path.Combine(Path.GetTempPath(), "MenuDpi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var engine = DispatchProxy.Create<IPlaybackEngine, WindowTitleTests.TitlePlayback>();
        var settings = new AppSettings();
        using var form = new Form1(settings, new PortableSettingsStore(directory), new Factory(engine));
        try { _ = form.Handle; action(form, settings); }
        finally { form.Close(); Directory.Delete(directory, true); }
    }
    private sealed class Factory(IPlaybackEngine engine) : IPlaybackEngineFactory
    {
        public IPlaybackEngine CreateEngine(Control? c, int v, Func<VideoDecodingMode> m) => engine;
        public IVideoHost CreateVideoHost(Panel c, IPlaybackEngine e, ContextMenuStrip m) => new Host(c);
    }
    private sealed class Host(Control control) : IVideoHost
    {
        public Control HostControl => control;
        public event EventHandler? VideoResized { add { } remove { } }
        public void ApplyVerticalOffset(int n) { } public void ApplyHorizontalOffset(int n) { } public void ApplyZoom(int n) { }
        public void ShowContextMenuCentered() { } public void Focus() { }
        public void RegisterFileDrop(DragEventHandler a, DragEventHandler b) { } public void Dispose() { }
    }
}
