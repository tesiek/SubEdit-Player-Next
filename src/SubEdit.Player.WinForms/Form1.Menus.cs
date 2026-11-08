using System.Drawing;
using System.Text;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Controls;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private MenuStrip CreateMenu()
    {
        var openVideoItem = CommandItem("Otwórz plik wideo", "Ctrl+O", Keys.Control | Keys.O, OpenVideo);
        var openSubtitleItem = CommandItem("Otwórz plik napisów", "Ctrl+Shift+O", Keys.Control | Keys.Shift | Keys.O, OpenSubtitleFile);
        var newSubtitleItem = CommandItem("Nowe napisy", "Ctrl+N", Keys.Control | Keys.N, NewSubtitle);
        var saveSubtitleItem = CommandItem("Zapisz napisy", "Ctrl+S", Keys.Control | Keys.S, SaveSubtitle);
        var saveSubtitleAsItem = CommandItem("Zapisz napisy jako", "Ctrl+Shift+S", Keys.Control | Keys.Shift | Keys.S, SaveSubtitleAs);
        var saveSubtitleAsSrtItem = Item("Eksportuj napisy do SRT", SaveSubtitleAsSrt);
        var findTextItem = CommandItem("Znajdź tekst", "Ctrl+F", Keys.Control | Keys.F, ShowFindDialog);
        var findShortSubtitleItem = CommandItem(
            "Znajdź krótko wyświetlany napis",
            "Ctrl+Shift+D",
            Keys.Control | Keys.Shift | Keys.D,
            ShowFindShortSubtitleDialog);
        var findLongLineItem = CommandItem(
            "Znajdź długi wiersz",
            "Ctrl+Alt+D",
            Keys.Control | Keys.Alt | Keys.D,
            ShowFindLongSubtitleLineDialog);
        var findSubtitleGapItem = CommandItem(
            "Znajdź przerwę w wyświetlaniu napisów",
            "Ctrl+Shift+F",
            Keys.Control | Keys.Shift | Keys.F,
            ShowFindSubtitleGapDialog);
        var findUnsortedSubtitleItem = CommandItem(
            "Znajdź nieposortowany napis",
            "Ctrl+Alt+F",
            Keys.Control | Keys.Alt | Keys.F,
            FindUnsortedSubtitle);
        var findNextItem = CommandItem("Znajdź następny", "F3", Keys.F3, () => { FindNextSubtitleText(); });
        var findPreviousItem = CommandItem("Znajdź poprzedni", "Shift+F3", Keys.Shift | Keys.F3, () => { FindPreviousSubtitleText(); });
        var replaceTextItem = CommandItem("Znajdź i zamień", "Ctrl+H", Keys.Control | Keys.H, ShowReplaceDialog);
        var editorItem = CommandItem("Edytor napisów", "Ctrl+E", Keys.Control | Keys.E, ToggleEditor);
        _editorMenuItem = editorItem;
        var alwaysOnTopItem = CommandItem("Zawsze na wierzchu", "F6", Keys.F6, ToggleAlwaysOnTop);
        alwaysOnTopItem.Checked = TopMost;
        _alwaysOnTopMenuItem = alwaysOnTopItem;
        var fullScreenItem = CommandItem("Tryb pełnoekranowy", "F11", Keys.F11, ToggleFullScreen);
        _fullScreenMenuItem = fullScreenItem;
        var videoPropertiesItem = CommandItem("Właściwości pliku wideo", "F9", Keys.F9, ShowVideoProperties);
        videoPropertiesItem.Enabled = false;
        _videoPropertiesMenuItem = videoPropertiesItem;
        var goToVideoPositionItem = CommandItem("Przejdź do czasu filmu", "Ctrl+K", Keys.Control | Keys.K, ShowGoToVideoPositionDialog);
        goToVideoPositionItem.Enabled = false;
        _goToVideoPositionMenuItem = goToVideoPositionItem;
        var insertStartItem = CommandItem("Wstaw czas początkowy napisu", "Insert", Keys.Insert, () => InsertVideoPosition(insertStart: true));
        var insertEndItem = CommandItem("Wstaw czas końcowy napisu", "Alt+Insert", Keys.Alt | Keys.Insert, () => InsertVideoPosition(insertStart: false));
        var defaultFormatItem = CommandItem(
            GetDefaultSubtitleFormatMenuText(),
            "Ctrl+`",
            Keys.Control | Keys.Oemtilde,
            ShowSubtitleFormatDialog);
        _defaultSubtitleFormatMenuItem = defaultFormatItem;
        var convertSubtitleFormatItem = CommandItem("Konwertuj format napisów", "Ctrl+T", Keys.Control | Keys.T, ShowSubtitleConversionDialog);
        convertSubtitleFormatItem.Enabled = false;
        _convertSubtitleFormatMenuItem = convertSubtitleFormatItem;
        var correctSubtitlesItem = CommandItem("Popraw napisy", "Ctrl+R", Keys.Control | Keys.R, ShowSubtitleCorrectionDialog);
        correctSubtitlesItem.Enabled = false;
        _correctSubtitlesMenuItem = correctSubtitlesItem;
        var goToSubtitleItem = CommandItem(
            "Przejdź do napisu",
            "Ctrl+G",
            Keys.Control | Keys.G,
            ShowGoToSubtitleDialog);
        goToSubtitleItem.Enabled = false;
        _goToSubtitleMenuItem = goToSubtitleItem;
        var shiftSubtitleTimeItem = CommandItem(
            "Przesuń czas napisów",
            "Ctrl+D",
            Keys.Control | Keys.D,
            ShowSubtitleTimeShiftDialog);
        shiftSubtitleTimeItem.Enabled = false;
        _shiftSubtitleTimeMenuItem = shiftSubtitleTimeItem;
        var changeSubtitleDisplayTimeItem = CommandItem(
            "Zmień czas wyświetlania napisów",
            "Ctrl+W",
            Keys.Control | Keys.W,
            ShowSubtitleDisplayTimeDialog);
        changeSubtitleDisplayTimeItem.Enabled = false;
        _changeSubtitleDisplayTimeMenuItem = changeSubtitleDisplayTimeItem;
        var rapidSubtitleMergeItem = CommandItem(
            "Połącz szybko znikające napisy",
            "Ctrl+Alt+G",
            Keys.Control | Keys.Alt | Keys.G,
            ShowRapidSubtitleMergeDialog);
        rapidSubtitleMergeItem.Enabled = false;
        _rapidSubtitleMergeMenuItem = rapidSubtitleMergeItem;
        var splitLongSubtitleLinesItem = CommandItem(
            "Podziel długie wiersze",
            "Ctrl+Shift+Y",
            Keys.Control | Keys.Shift | Keys.Y,
            ShowLongSubtitleLineSplitDialog);
        splitLongSubtitleLinesItem.Enabled = false;
        _splitLongSubtitleLinesMenuItem = splitLongSubtitleLinesItem;
        var limitSubtitleLinesItem = CommandItem(
            "Ogranicz ilość wierszy w linii",
            "Ctrl+Shift+G",
            Keys.Control | Keys.Shift | Keys.G,
            ShowSubtitleLineLimitDialog);
        limitSubtitleLinesItem.Enabled = false;
        _limitSubtitleLinesMenuItem = limitSubtitleLinesItem;
        var changeSubtitleFrameRateItem = CommandItem(
            "Zmień współczynnik FPS napisów",
            "Ctrl+Q",
            Keys.Control | Keys.Q,
            ShowSubtitleFrameRateDialog);
        changeSubtitleFrameRateItem.Enabled = false;
        _changeSubtitleFrameRateMenuItem = changeSubtitleFrameRateItem;
        var sortSubtitlesItem = CommandItem(
            "Sortuj napisy",
            "Ctrl+Shift+R",
            Keys.Control | Keys.Shift | Keys.R,
            SortSubtitles);
        sortSubtitlesItem.Enabled = false;
        _sortSubtitlesMenuItem = sortSubtitlesItem;
        var addHalfSecondItem = CommandItem(
            "Dodaj pół sekundy do czasu napisu",
            "Ctrl+.",
            Keys.Control | Keys.OemPeriod,
            () => ShiftCurrentSubtitleByHalfSecond(0.5d));
        addHalfSecondItem.Enabled = false;
        _addHalfSecondMenuItem = addHalfSecondItem;
        var subtractHalfSecondItem = CommandItem(
            "Odejmij pół sekundy od czasu napisu",
            "Ctrl+,",
            Keys.Control | Keys.Oemcomma,
            () => ShiftCurrentSubtitleByHalfSecond(-0.5d));
        subtractHalfSecondItem.Enabled = false;
        _subtractHalfSecondMenuItem = subtractHalfSecondItem;
        var jumpToCurrentTimeSubtitleItem = CommandItem(
            "Przejdź do napisu w bieżącym czasie filmu",
            "Ctrl+Shift+L",
            Keys.Control | Keys.Shift | Keys.L,
            JumpToSubtitleAtCurrentTime);
        jumpToCurrentTimeSubtitleItem.Enabled = false;
        _jumpToCurrentTimeSubtitleMenuItem = jumpToCurrentTimeSubtitleItem;
        var measureSubtitleDelayItem = CommandItem(
            "Zmierz opóźnienie napisu względem dźwięku",
            "Ctrl+/",
            Keys.Control | Keys.OemQuestion,
            ShowMeasuredSubtitleDelayDialog);
        measureSubtitleDelayItem.Enabled = false;
        _measureSubtitleDelayMenuItem = measureSubtitleDelayItem;
        var measureInitialSubtitleDelayItem = CommandItem(
            "Zmierz początkowe opóźnienie napisu",
            "Ctrl+F1",
            Keys.Control | Keys.F1,
            () => MeasureSubtitleGeometryPoint(isInitial: true));
        measureInitialSubtitleDelayItem.Enabled = false;
        _measureInitialSubtitleDelayMenuItem = measureInitialSubtitleDelayItem;
        var measureFinalSubtitleDelayItem = CommandItem(
            "Zmierz końcowe opóźnienie napisu",
            "Ctrl+F2",
            Keys.Control | Keys.F2,
            () => MeasureSubtitleGeometryPoint(isInitial: false));
        measureFinalSubtitleDelayItem.Enabled = false;
        _measureFinalSubtitleDelayMenuItem = measureFinalSubtitleDelayItem;
        var geometricSubtitleShiftItem = CommandItem(
            "Geometryczne przesunięcie napisów",
            "Ctrl+F3",
            Keys.Control | Keys.F3,
            ShowGeometricSubtitleShiftDialog);
        geometricSubtitleShiftItem.Enabled = false;
        _geometricSubtitleShiftMenuItem = geometricSubtitleShiftItem;
        var subtitleTraceItem = CommandItem(
            "Śledzenie napisów w edytorze",
            "Ctrl+J",
            Keys.Control | Keys.J,
            ToggleSubtitleTrace);
        subtitleTraceItem.Enabled = false;
        _subtitleTraceMenuItem = subtitleTraceItem;
        var subtitleVisibilityItem = Item("Wyświetlaj napisy", ToggleSubtitleVisibility);
        subtitleVisibilityItem.ShortcutKeyDisplayString = "Ctrl+Shift+N";
        subtitleVisibilityItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
        subtitleVisibilityItem.Checked = _subtitlesVisible;
        _subtitleVisibilityMenuItem = subtitleVisibilityItem;
        var transparentSubtitlesItem = Item("Przezroczyste napisy", ToggleSubtitleTransparency);
        transparentSubtitlesItem.ShortcutKeyDisplayString = "M";
        transparentSubtitlesItem.Checked = _transparentSubtitles;
        _transparentSubtitlesMenuItem = transparentSubtitlesItem;
        var timeCounterItem = _timeCounterMenuItem = CreateTimeCounterMenuItem();
        UpdateTimeCounterMenuChecks();
        var aboutItem = Item("O programie", ShowAboutDialog);
        var recentFilesMenu = CreateRecentFilesMenu();
        var subtitleEncodingMenu = CreateSubtitleEncodingMenu();
        var menu = new ActivatingMenuStrip
        {
            Dock = DockStyle.Top,
            Padding = new Padding(3, 2, 0, 2)
        };
        var closeVideoItem = CommandItem("Zamknij plik wideo", "Ctrl+Alt+F4", Keys.Control | Keys.Alt | Keys.F4, CloseVideoFile);
        closeVideoItem.Enabled = false;
        _closeVideoMenuItem = closeVideoItem;
        var splitSubtitlesItem = Item("Podziel plik napisów (film w kilku częściach)", SplitSubtitles);
        splitSubtitlesItem.Enabled = false;
        _splitSubtitlesMenuItem = splitSubtitlesItem;
        var mergeSubtitlesItem = Item("Dołącz plik napisów (film w jednej części)", MergeSubtitles);
        mergeSubtitlesItem.Enabled = false;
        _mergeSubtitlesMenuItem = mergeSubtitlesItem;
        var fileMenu = Submenu("Plik",
            openVideoItem,
            closeVideoItem,
            videoPropertiesItem,
            Separator(),
            newSubtitleItem,
            openSubtitleItem,
            Item("Dopisz napisy z pliku", AppendSubtitleFile),
            CommandItem("Zamknij plik napisów", "Ctrl+F4", Keys.Control | Keys.F4, CloseSubtitles),
            Separator(),
            saveSubtitleItem,
            saveSubtitleAsItem,
            saveSubtitleAsSrtItem,
            Separator(),
            splitSubtitlesItem,
            mergeSubtitlesItem,
            Separator(),
            recentFilesMenu,
            Separator(),
            CommandItem("Zakończ", "Alt+F4", Keys.Alt | Keys.F4, Close));
        fileMenu.DropDownOpening += (_, _) =>
        {
            UpdateCloseVideoMenuState();
            UpdateSplitSubtitleAvailability();
            UpdateMergeSubtitleAvailability();
        };
        menu.Items.Add(fileMenu);

        _undoMenuItem = CommandItem("Cofnij", "Ctrl+Z", Keys.Control | Keys.Z, UndoSubtitleEdit);
        _redoMenuItem = CommandItem("Ponów", "Ctrl+Y", Keys.Control | Keys.Y, RedoSubtitleEdit);
        var editMenu = Submenu("Edycja",
            _undoMenuItem,
            _redoMenuItem,
            Separator(),
            CommandItem("Wytnij", "Ctrl+X", Keys.Control | Keys.X, () => _subtitleEditor.Cut()),
            CommandItem("Kopiuj", "Ctrl+C", Keys.Control | Keys.C, () => _subtitleEditor.Copy()),
            CommandItem("Wklej", "Ctrl+V", Keys.Control | Keys.V, () => _subtitleEditor.Paste()),
            Separator(),
            findTextItem,
            replaceTextItem,
            findNextItem,
            findPreviousItem,
            Submenu("Wyszukiwanie specjalne",
                findShortSubtitleItem,
                findLongLineItem,
                findSubtitleGapItem,
                findUnsortedSubtitleItem),
            Separator(),
            CommandItem("Kursywa", "Ctrl+I", Keys.Control | Keys.I, ToggleCurrentSubtitleLineItalic),
            CommandItem("Podkreślenie", "Ctrl+U", Keys.Control | Keys.U, ToggleCurrentSubtitleLineUnderline),
            CommandItem("Pogrubienie", "Ctrl+B", Keys.Control | Keys.B, ToggleCurrentSubtitleLineBold),
            Separator(),
            CommandItem("Konwertuj znaczniki kursywy", "Ctrl+Shift+I", Keys.Control | Keys.Shift | Keys.I, ShowItalicMarkerConversionDialog));
        editMenu.DropDownOpening += (_, _) =>
        {
            var hasText = _subtitleEditor.TextLength > 0;
            findShortSubtitleItem.Enabled = hasText;
            findSubtitleGapItem.Enabled = hasText;
            findUnsortedSubtitleItem.Enabled = hasText;
            var canRepeatSearch = !string.IsNullOrEmpty(_lastFindQuery) || _repeatLongSubtitleLineSearch ||
                _repeatShortSubtitleSearch || _repeatSubtitleGapSearch || _repeatUnsortedSubtitleSearch;
            findNextItem.Enabled = canRepeatSearch;
            findPreviousItem.Enabled = canRepeatSearch;
        };
        menu.Items.Add(editMenu);

        var viewPanItem = Submenu("Przesunięcie obrazu",
            CommandItem("W górę", "Ctrl+Alt+Up", Keys.Control | Keys.Alt | Keys.Up, () => AdjustMovieElevation(5)),
            CommandItem("W dół", "Ctrl+Alt+Down", Keys.Control | Keys.Alt | Keys.Down, () => AdjustMovieElevation(-5)),
            CommandItem("W lewo", "Ctrl+Alt+Left", Keys.Control | Keys.Alt | Keys.Left, () => AdjustMovieHorizontalOffset(-5)),
            CommandItem("W prawo", "Ctrl+Alt+Right", Keys.Control | Keys.Alt | Keys.Right, () => AdjustMovieHorizontalOffset(5)));
        var viewZoomInItem = CommandItem("Powiększenie", "Ctrl+Alt+=", Keys.Control | Keys.Alt | Keys.Oemplus, () => AdjustMovieZoom(10));
        var viewZoomOutItem = CommandItem("Pomniejszenie", "Ctrl+Alt+-", Keys.Control | Keys.Alt | Keys.OemMinus, () => AdjustMovieZoom(-10));
        var viewResetTransformationsItem = CommandItem("Resetuj transformacje", "Ctrl+Alt+0", Keys.Control | Keys.Alt | Keys.D0, ResetMovieTransformations);
        var viewMenu = Submenu("Widok",
            editorItem,
            subtitleVisibilityItem,
            transparentSubtitlesItem,
            timeCounterItem,
            Separator(),
            viewPanItem,
            viewZoomInItem,
            viewZoomOutItem,
            viewResetTransformationsItem,
            Separator(),
            alwaysOnTopItem,
            fullScreenItem);
        viewMenu.DropDownOpening += (_, _) =>
        {
            UpdateEditorMenuCheck();
            UpdateTimeCounterMenuChecks();
            alwaysOnTopItem.Checked = TopMost;
            var hasVideo = HasOpenVideo();
            viewPanItem.Enabled = hasVideo;
            viewZoomInItem.Enabled = hasVideo;
            viewZoomOutItem.Enabled = hasVideo;
            viewResetTransformationsItem.Enabled = hasVideo;
        };
        menu.Items.Add(viewMenu);

        menu.Items.Add(Submenu("Napisy",
            goToSubtitleItem,
            Separator(),
            shiftSubtitleTimeItem,
            Submenu("Synchronizacja napisów",
                measureSubtitleDelayItem,
                Separator(),
                measureInitialSubtitleDelayItem,
                measureFinalSubtitleDelayItem,
                geometricSubtitleShiftItem),
            changeSubtitleDisplayTimeItem,
            changeSubtitleFrameRateItem,
            addHalfSecondItem,
            subtractHalfSecondItem,
            Separator(),
            rapidSubtitleMergeItem,
            splitLongSubtitleLinesItem,
            limitSubtitleLinesItem,
            correctSubtitlesItem,
            sortSubtitlesItem,
            Separator(),
            convertSubtitleFormatItem,
            subtitleEncodingMenu));

        _loopMenuItem = new ToolStripMenuItem("Powtarzaj odtwarzanie")
        {
            Name = "movieRepeat",
            CheckOnClick = true,
            ShortcutKeyDisplayString = "R"
        };
        _loopMenuItem.Click += (_, _) => ToggleLoopPlayback();
        var movieSpeedItem = Submenu("Prędkość odtwarzania",
            CommandItem("Przyspiesz", "Alt+Up", Keys.Alt | Keys.Up, IncreasePlaybackSpeed),
            Item("Normalna prędkość", ResetPlaybackSpeed),
            CommandItem("Zwolnij", "Alt+Down", Keys.Alt | Keys.Down, DecreasePlaybackSpeed));
        var seekMenu = Submenu("Przewijanie",
            CommandItem("Przewiń 5 sekund w przód", "Ctrl+]", Keys.Control | Keys.OemCloseBrackets, () => QueuePreciseSeekWithAccumulatedOsd(5_000)),
            CommandItem("Przewiń 5 sekund wstecz", "Ctrl+[", Keys.Control | Keys.OemOpenBrackets, () => QueuePreciseSeekWithAccumulatedOsd(-5_000)),
            CommandItem("Przewiń w przód o własny krok", "Alt+Right", Keys.Alt | Keys.Right, SeekUserForward),
            CommandItem("Przewiń wstecz o własny krok", "Alt+Left", Keys.Alt | Keys.Left, SeekUserBackward),
            CommandItem("Przejdź na początek filmu", "Ctrl+Shift+[", Keys.Control | Keys.Shift | Keys.OemOpenBrackets, () => SeekToBoundary(toEnd: false)),
            CommandItem("Przejdź na koniec filmu", "Ctrl+Shift+]", Keys.Control | Keys.Shift | Keys.OemCloseBrackets, () => SeekToBoundary(toEnd: true)));
        var playFromSubtitleItem = CommandItem("Odtwarzaj film od bieżącego napisu", "Ctrl+P", Keys.Control | Keys.P, PlayFromEditorLine);
        var movieMenu = Submenu("Film",
            goToVideoPositionItem,
            seekMenu,
            Separator(),
            CommandItem("Pauza / Odtwarzanie", "Ctrl+L", Keys.Control | Keys.L, TogglePlayback),
            CommandItem("Stop", "Ctrl+M", Keys.Control | Keys.M, StopPlayback),
            playFromSubtitleItem,
            _loopMenuItem,
            movieSpeedItem,
            Separator(),
            insertStartItem,
            insertEndItem,
            jumpToCurrentTimeSubtitleItem,
            subtitleTraceItem,
            Separator(),
            CommandItem("Wczytaj poprzedni plik", "Alt+,", Keys.Alt | Keys.Oemcomma, () => PlayAdjacentVideo(-1)),
            CommandItem("Wczytaj następny plik", "Alt+.", Keys.Alt | Keys.OemPeriod, () => PlayAdjacentVideo(1)),
            CreateDirectoryVideoMenu());
        movieMenu.DropDownOpening += (_, _) =>
        {
            var hasVideo = HasOpenVideo();
            movieSpeedItem.Enabled = hasVideo;
            seekMenu.Enabled = hasVideo && Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds) > 0;
            playFromSubtitleItem.Enabled = hasVideo;
            insertStartItem.Enabled = insertEndItem.Enabled = _currentVideoPath is not null && !_splitContainer.Panel2Collapsed;
        };
        menu.Items.Add(movieMenu);

        menu.Items.Add(Submenu("Ustawienia",
            defaultFormatItem,
            CommandItem(
                "Aktywny współczynnik FPS",
                "Ctrl+Shift+Q",
                Keys.Control | Keys.Shift | Keys.Q,
                ShowDefaultFrameRateDialog),
            Separator(),
            CommandItem("Opcje", "F12", Keys.F12, ShowEditorOptionsDialog)));

        menu.Items.Add(Submenu("Pomoc",
            CommandItem("Tematy pomocy", "F1", Keys.F1, () => OpenHelpDocument("SubEditHelp.html")),
            Item("Skróty klawiszowe", () => OpenHelpDocument("SkrotyKlawiszowe.html")),
            Item("FAQ: edycja napisów", () => OpenHelpDocument("faq_edycja.html")),
            Item("SubEdit-Player Next — GitHub", () => OpenWebsite("https://github.com/tesiek/SubEdit-Player-Next")),
            Separator(),
            Item("Diagnostyka programu", ShowProgramDiagnostics),
            aboutItem));

        return menu;
    }

    private ToolStripButton ToolbarButton(string name) =>
        _mainToolbar.Items.Find(name, searchAllChildren: false).OfType<ToolStripButton>().Single();

    private bool HasOpenVideo() => _playback.IsMediaOpen;

    private ContextMenuStrip CreateVideoMenu()
    {
        var menu = new ContextMenuStrip();
        var speedItem = Submenu("Prędkość odtwarzania",
            Item("Przyspiesz", IncreasePlaybackSpeed),
            Item("Normalna prędkość", ResetPlaybackSpeed),
            Item("Zwolnij", DecreasePlaybackSpeed));
        var panItem = Submenu("Przesunięcie obrazu",
            Item("W górę", () => AdjustMovieElevation(5)),
            Item("W dół", () => AdjustMovieElevation(-5)),
            Item("W lewo", () => AdjustMovieHorizontalOffset(-5)),
            Item("W prawo", () => AdjustMovieHorizontalOffset(5)));
        var zoomInItem = Item("Powiększenie", () => AdjustMovieZoom(10));
        var zoomOutItem = Item("Pomniejszenie", () => AdjustMovieZoom(-10));
        var resetItem = Item("Resetuj transformacje", ResetMovieTransformations);
        var subtitleVisibilityItem = Item("Wyświetlaj napisy", ToggleSubtitleVisibility);
        var timeCounterItem = _videoTimeCounterMenuItem = CreateTimeCounterMenuItem();
        UpdateTimeCounterMenuChecks();
        var fullScreenItem = Item("Tryb pełnoekranowy", ToggleFullScreen);
        menu.Items.AddRange([
            Item("Otwórz plik wideo", OpenVideo),
            Item("Otwórz plik napisów", OpenSubtitleFile),
            Separator(),
            Item("Pauza / Odtwarzanie", TogglePlayback),
            Item("Stop", StopPlayback),
            speedItem,
            Separator(),
            panItem,
            zoomInItem,
            zoomOutItem,
            resetItem,
            Separator(),
            subtitleVisibilityItem,
            timeCounterItem,
            fullScreenItem,
            Separator(),
            Item("Opcje", ShowEditorOptionsDialog),
            Item("Zakończ program", Close)
        ]);
        menu.Opening += (_, _) =>
        {
            var hasVideo = HasOpenVideo();
            panItem.Enabled = hasVideo;
            zoomInItem.Enabled = hasVideo;
            zoomOutItem.Enabled = hasVideo;
            resetItem.Enabled = hasVideo;
            speedItem.Enabled = hasVideo;
            subtitleVisibilityItem.Checked = _subtitlesVisible;
            UpdateTimeCounterMenuChecks();
            fullScreenItem.Checked = _isFullScreen;
        };
        return menu;
    }

    private ToolStripMenuItem CreateTimeCounterMenuItem()
    {
        var item = Item("Wyświetlaj czas filmu", ToggleTimeCounterVisibility);
        item.ShortcutKeyDisplayString = "T";
        item.ToolTipText = "Przełącza czas od początku filmu, czas pozostały i ukrycie licznika.";
        return item;
    }

    private static ToolStripMenuItem Submenu(string title, params ToolStripItem[] items)
    {
        var menu = new ToolStripMenuItem(title);
        menu.DropDownItems.AddRange(items);
        return menu;
    }

    private ToolStripMenuItem CreateSubtitleEncodingMenu()
    {
        var currentEncoding = DisabledItem(string.Empty);
        var menu = Submenu("Kodowanie napisów", currentEncoding, Separator());
        var encodings = new (SubtitleEncodingKind Kind, string Name)[]
        {
            (SubtitleEncodingKind.Ansi, "ANSI"),
            (SubtitleEncodingKind.Utf8, "UTF-8"),
            (SubtitleEncodingKind.Utf8Bom, "UTF-8-BOM"),
            (SubtitleEncodingKind.Utf16BigEndianBom, "UTF-16 BE BOM"),
            (SubtitleEncodingKind.Utf16LittleEndianBom, "UTF-16 LE BOM")
        };
        var conversions = encodings.Select(encoding =>
        {
            var item = Item($"Konwertuj na {encoding.Name}", () => ConvertSubtitleEncoding(encoding.Kind));
            item.ToolTipText = "Ustawia kodowanie następnego zapisu napisów.";
            menu.DropDownItems.Add(item);
            return (encoding.Kind, encoding.Name, Item: item);
        }).ToArray();
        menu.DropDown.ShowItemToolTips = true;
        menu.DropDownOpening += (_, _) => RefreshEncodingMenu();
        RefreshEncodingMenu();
        return menu;

        void RefreshEncodingMenu()
        {
            var currentKind = SubtitleFileReader.GetEncodingKind(_subtitleEncoding);
            foreach (var (kind, name, item) in conversions)
            {
                item.Enabled = kind != currentKind;
                if (kind == currentKind)
                {
                    currentEncoding.Text = $"Bieżące kodowanie: {name}";
                }
            }
        }
    }

    private ToolStripMenuItem CreateDirectoryVideoMenu()
    {
        var playAllVideos = CommandItem(
            "Odtwarzaj wszystkie filmy w katalogu",
            "Ctrl+Alt+.",
            Keys.Control | Keys.Alt | Keys.OemPeriod,
            ToggleDirectoryPlaybackFromShortcut);
        var sortByName = Item("Sortowanie plików: Nazwa", () => SetDirectoryVideoSortOrder(DirectoryVideoSortOrder.Name));
        var sortByExtension = Item("Sortowanie plików: Rozszerzenie", () => SetDirectoryVideoSortOrder(DirectoryVideoSortOrder.Extension));
        var sortRandomly = Item("Sortowanie plików: Losowo", () => SetDirectoryVideoSortOrder(DirectoryVideoSortOrder.Random));
        var directoryMenu = Submenu(
            "Filmy w aktualnym katalogu",
            playAllVideos,
            Separator(),
            sortByName,
            sortByExtension,
            sortRandomly);
        directoryMenu.DropDownOpening += (_, _) =>
        {
            var sortOrder = GetDirectoryVideoSortOrder();
            playAllVideos.Checked = _playDirectoryButton.Checked;
            sortByExtension.Checked = sortOrder == DirectoryVideoSortOrder.Extension;
            sortByName.Checked = sortOrder == DirectoryVideoSortOrder.Name;
            sortRandomly.Checked = sortOrder == DirectoryVideoSortOrder.Random;
        };
        return directoryMenu;
    }

    private DirectoryVideoSortOrder GetDirectoryVideoSortOrder() => _directoryVideoSortOrder;

    private void SetDirectoryVideoSortOrder(DirectoryVideoSortOrder sortOrder)
    {
        _directoryVideoSortOrder = sortOrder;
        _randomDirectoryPath = null;
        _randomDirectoryVideoOrder = null;
    }

    private void ConvertSubtitleEncoding(SubtitleEncodingKind encodingKind)
    {
        if (SubtitleFileReader.GetEncodingKind(_subtitleEncoding) == encodingKind)
        {
            return;
        }

        var encoding = SubtitleFileReader.CreateEncoding(encodingKind);
        try
        {
            _ = encoding.GetByteCount(_subtitleEditor.Text);
        }
        catch (EncoderFallbackException)
        {
            MessageBox.Show(
                this,
                "Tekst zawiera znaki, których nie można zapisać w kodowaniu ANSI (Windows-1250).",
                "Nie można przekonwertować napisów",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _subtitleEncoding = encoding;
        UpdateSubtitleDirtyState();
        _subtitleEditor.Focus();
    }

    private void ShowVideoContextMenu()
    {
        _videoHost.ShowContextMenuCentered();
    }

    private ToolStripMenuItem CreateRecentFilesMenu()
    {
        var menu = Submenu("Ostatnio otwierane pliki");
        menu.DropDownOpening += (_, _) => PopulateRecentFilesMenu(menu);
        PopulateRecentFilesMenu(menu);
        return menu;
    }

    private void PopulateRecentFilesMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        var videos = _settings.Playback.RecentVideoPaths;
        var subtitles = _settings.Playback.RecentSubtitlePaths;

        if (videos.Count == 0 && subtitles.Count == 0)
        {
            menu.DropDownItems.Add(DisabledItem("Brak historii"));
        }
        else
        {
            for (var index = 0; index < videos.Count; index++)
            {
                var path = videos[index];
                menu.DropDownItems.Add(Item($"{index + 1} {path}", () => LoadVideoFile(path)));
            }

            if (videos.Count > 0 && subtitles.Count > 0)
            {
                menu.DropDownItems.Add(Separator());
            }

            for (var index = 0; index < subtitles.Count; index++)
            {
                var path = subtitles[index];
                menu.DropDownItems.Add(Item($"{(char)('a' + index)} {path}", () => SelectTextSubtitle(path)));
            }
        }

        menu.DropDownItems.Add(Separator());
        var clearItem = Item("Wyczyść historię", ClearRecentFiles);
        clearItem.Enabled = videos.Count > 0 || subtitles.Count > 0;
        menu.DropDownItems.Add(clearItem);
    }

    private ToolStripMenuItem Item(string title, Action action)
    {
        var item = new ToolStripMenuItem(title);
        item.Click += (_, _) => action();
        return item;
    }

    private ToolStripMenuItem CommandItem(string title, string shortcut, Keys shortcutKeys, Action action)
    {
        var item = Item(title, action);
        item.ShortcutKeys = shortcutKeys;
        item.ShortcutKeyDisplayString = FormatShortcutKeys(shortcutKeys);
        return item;
    }

    private static string FormatShortcutKeys(Keys shortcutKeys)
    {
        if (shortcutKeys == Keys.None)
        {
            return string.Empty;
        }

        var parts = new List<string>(4);
        if ((shortcutKeys & Keys.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((shortcutKeys & Keys.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((shortcutKeys & Keys.Shift) != 0)
        {
            parts.Add("Shift");
        }

        var key = shortcutKeys & Keys.KeyCode;
        var keyText = key switch
        {
            Keys.Oemtilde => "`",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.Oem5 => "\\",
            Keys.OemQuestion => "/",
            Keys.OemPeriod => ".",
            Keys.Oemcomma => ",",
            Keys.Oemplus => "=",
            Keys.OemMinus => "-",
            Keys.D0 => "0",
            _ => key.ToString()
        };

        parts.Add(keyText);
        return string.Join("+", parts);
    }

    private static ToolStripMenuItem DisabledItem(string title) => new(title) { Enabled = false };

    private static ToolStripSeparator Separator() => new();

    private ToolStrip CreateMainToolbar()
    {
        var activeSubtitleFormatButton = CreateToolbarButton("toolbarActiveSubtitleFormat", string.Empty, EditorToolbarIcon.ActiveFormat);
        activeSubtitleFormatButton.Click += (_, _) => ShowSubtitleFormatDialog();
        _activeSubtitleFormatButton = activeSubtitleFormatButton;
        UpdateActiveSubtitleFormatIndicator();
        var toolbar = new ActivatingToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new Size(22, 22),
            Padding = new Padding(3, 2, 3, 2),
            RenderMode = ToolStripRenderMode.System
        };

        toolbar.Items.AddRange([
            CreateToolbarButton("toolbarOpenVideo", "Otwórz plik wideo (Ctrl+O)", EditorToolbarIcon.OpenVideo),
            CreateToolbarButton("toolbarOpenSubtitles", "Otwórz plik napisów (Ctrl+Shift+O)", EditorToolbarIcon.OpenSubtitles),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarNew", "Nowe napisy (Ctrl+N)", EditorToolbarIcon.New),
            CreateToolbarButton("toolbarSave", "Zapisz tekst (Ctrl+S)", EditorToolbarIcon.Save, enabled: false),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarUndo", "Cofnij (Ctrl+Z)", EditorToolbarIcon.Undo, enabled: false),
            CreateToolbarButton("toolbarRedo", "Ponów (Ctrl+Y)", EditorToolbarIcon.Redo, enabled: false),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarEditor", "Edytor napisów (Ctrl+E)", EditorToolbarIcon.Editor, checkOnClick: true, isChecked: true),
            CreateFindToolbarButton(),
            activeSubtitleFormatButton,
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarSubtitleTrace", "Śledzenie napisów w edytorze (Ctrl+J)", EditorToolbarIcon.SubtitleTrace, enabled: false, checkOnClick: true),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarConvert", "Konwertuj format napisów (Ctrl+T)", EditorToolbarIcon.Convert, enabled: false),
            CreateToolbarButton("toolbarAddTime", "Przesuń czas napisów (Ctrl+D)", EditorToolbarIcon.ShiftTime, enabled: false),
            CreateToolbarButton("toolbarShowTime", "Zmień czas wyświetlania napisów (Ctrl+W)", EditorToolbarIcon.DisplayTime, enabled: false),
            CreateToolbarButton("toolbarTransparentSubtitles", "Przezroczyste napisy (M)", EditorToolbarIcon.TransparentSubtitles, checkOnClick: true, isChecked: _transparentSubtitles),
            CreateToolbarButton("toolbarShowSubtitles", "Wyświetlaj napisy (Ctrl+Shift+N)", EditorToolbarIcon.ShowSubtitles, checkOnClick: true, isChecked: true),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarAlwaysOnTop", "Zawsze na wierzchu (F6)", EditorToolbarIcon.AlwaysOnTop, checkOnClick: true),
            CreateToolbarButton("toolbarFullscreen", "Tryb pełnoekranowy (F11)", EditorToolbarIcon.Fullscreen, checkOnClick: true),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarOptions", "Opcje (F12)", EditorToolbarIcon.Options),
            new ToolStripSeparator(),
            CreateToolbarButton("toolbarClose", "Zakończ (Alt+F4)", EditorToolbarIcon.Close)
        ]);
        ConfigureToolbarToolTipBehavior(toolbar);
        return toolbar;
    }

    private static void ConfigureToolbarToolTipBehavior(ToolStrip toolbar)
    {
        toolbar.ShowItemToolTips = false;
        var toolTip = new ToolTip();
        var suppressedItems = new HashSet<ToolStripItem>();

        foreach (ToolStripItem item in toolbar.Items)
        {
            if (item is not ToolStripButton and not ToolStripSplitButton)
            {
                continue;
            }

            item.AutoToolTip = false;
            item.MouseHover += (_, _) =>
            {
                if (suppressedItems.Contains(item) || string.IsNullOrWhiteSpace(item.ToolTipText))
                {
                    return;
                }

                var position = toolbar.PointToClient(System.Windows.Forms.Cursor.Position);
                position.Offset(1, SystemInformation.CursorSize.Height);
                toolTip.Show(item.ToolTipText, toolbar, position, toolTip.AutoPopDelay);
            };
            item.MouseDown += (_, _) =>
            {
                suppressedItems.Add(item);
                toolTip.Hide(toolbar);
            };
            item.MouseLeave += (_, _) =>
            {
                suppressedItems.Remove(item);
                toolTip.Hide(toolbar);
            };
        }

        toolbar.VisibleChanged += (_, _) =>
        {
            if (!toolbar.Visible)
            {
                suppressedItems.Clear();
                toolTip.Hide(toolbar);
            }
        };
        toolbar.Disposed += (_, _) => toolTip.Dispose();
    }

    private static ToolStripButton CreateToolbarButton(
        string name,
        string toolTip,
        EditorToolbarIcon icon,
        bool enabled = true,
        bool checkOnClick = false,
        bool isChecked = false)
    {
        var button = new EditorToolbarButton(name, toolTip, icon)
        {
            CheckOnClick = checkOnClick,
            Checked = isChecked,
            Enabled = enabled
        };

        return button;
    }

    private ToolStripButton TransportButton(PlaybackToolbarCommand command) =>
        (ToolStripButton)_transportToolbar.Items[command.ToString()]!;

    private ToolStripSplitButton CreateFindToolbarButton()
    {
        const string toolTip = "Znajdź tekst (Ctrl+F)";
        var button = new EditorToolbarSplitButton("toolbarFind", toolTip);
        button.ButtonClick += (_, _) => ShowFindDialog();
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź tekst", "Ctrl+F", ShowFindDialog));
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź krótko wyświetlany napis", "Ctrl+Shift+D", ShowFindShortSubtitleDialog));
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź długi wiersz", "Ctrl+Alt+D", ShowFindLongSubtitleLineDialog));
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź przerwę w wyświetlaniu napisów", "Ctrl+Shift+F", ShowFindSubtitleGapDialog));
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź nieposortowany napis", "Ctrl+Alt+F", FindUnsortedSubtitle));
        button.DropDownItems.Add(CreateFindDropDownItem("Znajdź następny", "F3", () => FindNextSubtitleText()));
        return button;
    }

    private static ToolStripMenuItem CreateFindDropDownItem(string text, string shortcut, Action? action = null)
    {
        var item = new ToolStripMenuItem(text);
        if (action is null)
        {
            item.Enabled = false;
        }
        else
        {
            item.ShortcutKeyDisplayString = shortcut;
            item.Click += (_, _) => action();
        }

        return item;
    }

    private DpiAwareStatusStrip CreateStatusStrip(out ToolStripStatusLabel fileStatus)
    {
        var status = new DpiAwareStatusStrip { SizingGrip = false, Padding = new Padding(0) };
        void AddFixed(string text, int width, bool borderRight = true) =>
            status.AddFixed(CreateStatusCell(text, width, borderRight: borderRight), width);

        status.AddMeasured(CreateStatusCell("{0}{0}", 102), _mDvdCounter.Measure);
        status.AddMeasured(CreateStatusCell("[0][0]", 92), _mpl2Counter.Measure);
        AddFixed("0:00:00.000", 96);
        AddFixed("0:00:00.000", 96);
        fileStatus = CreateStatusCell(string.Empty, 0, spring: true);
        status.Items.Add(fileStatus);
        status.Items.Add(CreateStatusCell(string.Empty, 285));
        AddFixed("[0.0 GB]", 78);
        AddFixed(string.Empty, 68, borderRight: false);
        return status;
    }

    private static ToolStripStatusLabel CreateStatusCell(
        string text,
        int width,
        bool spring = false,
        bool borderRight = true) => new(text)
        {
            AutoSize = spring,
            Spring = spring,
            Width = width,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderSides = borderRight ? ToolStripStatusLabelBorderSides.Right : ToolStripStatusLabelBorderSides.None,
            BorderStyle = Border3DStyle.Etched
        };
}
