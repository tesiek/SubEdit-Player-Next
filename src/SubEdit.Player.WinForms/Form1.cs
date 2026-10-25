using SubEdit.Player.Core.Configuration;
using SubEdit.Player.Core.Playback;
using SubEdit.Player.WinForms.Controls;
using SubEdit.Player.WinForms.Playback;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace SubEdit.Player.WinForms;

public partial class Form1 : Form
{
    private const int SeekSliderMaximum = 1000;
    private const int RecentFilesLimit = 5;
    private const int WmMouseActivate = 0x0021;
    private const int MaActivate = 1;
    private static readonly string[] SupportedSubtitleExtensions = [".txt", ".srt", ".sub", ".mpl"];
    private static readonly string[] SubtitleMenuExtensions = SupportedSubtitleExtensions;
    private readonly AppSettings _settings;
    private readonly PortableSettingsStore _settingsStore;
    private readonly string? _startupVideoPath;
    private readonly CleanSplitContainer _splitContainer;
    private readonly Panel _videoPanel;
    private readonly Panel _editorPanel;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _fileStatus;
    private readonly ToolStripStatusLabel _mDVDStatus;
    private readonly ToolStripStatusLabel _mpl2Status;
    private readonly ToolStripStatusLabel _fpsStatus;
    private readonly ToolStripStatusLabel _positionStatus;
    private readonly ToolStripStatusLabel _lengthStatus;
    private readonly ToolStripStatusLabel _decoderStatus;
    private readonly ToolStripStatusLabel _sizeStatus;
    private string _decoderModeStatusText = string.Empty;
    private string _videoCodecStatusText = string.Empty;
    private string _resolutionStatusText = string.Empty;
    private string _audioTrackStatusText = string.Empty;
    private readonly MenuStrip _menu;
    private readonly ToolStrip _mainToolbar;
    private readonly ToolStrip _transportToolbar;
    private readonly Panel _fullScreenControlsPanel;
    private readonly StatusStrip _fullScreenStatusStrip;
    private readonly ToolStripStatusLabel _fullScreenPlaybackStatus;
    private readonly ToolStripStatusLabel _fullScreenFileNameStatus;
    private readonly ToolStripStatusLabel _fullScreenTimeStatus;
    private readonly ToolStripButton _playButton;
    private readonly ToolStripButton _pauseButton;
    private ToolStripMenuItem _loopMenuItem = null!;
    private readonly ToolStripButton _playDirectoryButton;
    private readonly ToolStripButton _subtitleTracksButton;
    private readonly ToolStripButton _audioTracksButton;
    private readonly ToolStripButton _videoTracksButton;
    private readonly ToolStripButton _chaptersButton;
    private readonly ToolStripButton _volumeButton;
    private readonly VolumeTrackBar _volumeSlider;
    private readonly ChapterSeekBar _seekSlider;
    private readonly SubtitleTextEditor _subtitleEditor;
    private readonly Label _editorHeader;
    private readonly SubtitleInfoBar _subtitleInfoBar;
    private readonly IPlaybackEngine _playback;
    private readonly IVideoHost _videoHost;
    private readonly VideoOverlayForm _videoOverlay;
    private readonly System.Windows.Forms.Timer _playbackTimer;
    private readonly System.Windows.Forms.Timer _seekRepeatTimer;
    private readonly System.Windows.Forms.Timer _autoSaveTimer;
    private readonly System.Windows.Forms.Timer _fullScreenControlsTimer;
    private readonly VideoInputMessageFilter _videoInputFilter;
    private readonly FullScreenWheelHook _fullScreenWheelHook;
    private readonly Random _directoryShuffleRandom = new();
    private string? _currentVideoPath;
    private string? _currentSubtitlePath;
    private string? _suggestedSubtitleSavePath;
    private Encoding? _subtitleEncoding;
    private SubtitleTimeScaleTransform? _rememberedSubtitleTimeScaleTransform;
    private SubtitleDocument _subtitleDocument = SubtitleDocument.Empty;
    private long? _pendingResumePosition;
    private bool _pendingResumePrecise;
    private bool _updatingSeekSlider;
    private bool _isStopped = true;
    private int _volumeBeforeMute;
    private int _movieHorizontalOffset;
    private int _movieElevation;
    private int _movieZoomPercent = 100;
    private int _playbackSpeedMultiplier = 100;
    private DateTime _volumeOsdExpiresAt;
    private DateTime _playbackOsdExpiresAt;
    private DateTime _subtitleFontOsdExpiresAt;
    private DateTime _fileStatusExpiresAt;
    private DateTime _lastTimeCounterOsdUpdateAt;
    private long _smoothedPositionMilliseconds;
    private long _lastKnownMediaLengthMilliseconds;
    private long _stoppedSeekPosition;
    private long? _pendingPausedSeekPosition;
    private bool _pendingPausedSeekPrecise;
    private bool _showVideoEndPosition;
    private readonly RelativeSeekQueue _relativeSeekQueue = new();
    private readonly WheelSeekDeltaAccumulator _wheelSeekDeltaAccumulator = new();
    private long _lastPlaybackClockMilliseconds;
    private long _seekOsdOffsetMilliseconds;
    private bool _seekOsdVisible;
    private bool _mediaDetailsAvailable;
    private bool _isFullScreen;
    private bool _editorWasVisible;
    private int _editorCaretPositionBeforeFullScreen;
    private int? _frameInsertionUndoCaretPosition;
    private int? _frameInsertionRedoCaretPosition;
    private bool _frameInsertionUndoApplied;
    private bool _restoringFrameInsertionCaretPosition;
    private bool _resumePlaybackAfterRestore;
    private bool _resumePlaybackAfterApplicationActivation;
    private int _applicationActivationGeneration;
    private bool _resumePlaybackAfterModalDialog;
    private int _modalDialogDepth;
    private bool _isLoadingSubtitle;
    private bool _isSubtitleDirty;
    private bool _subtitleTextDirty;
    private bool _subtitleStateDirty;
    private Encoding? _savedSubtitleEncoding;
    private string? _lastFindQuery;
    private bool _lastFindMatchCase;
    private bool _lastFindWholeWord;
    private bool _repeatLongSubtitleLineSearch;
    private bool _repeatShortSubtitleSearch;
    private bool _repeatSubtitleGapSearch;
    private bool _repeatUnsortedSubtitleSearch;
    private SubtitleReplaceDialog? _replaceDialog;
    private bool _externalSubtitleOverlayEnabled;
    private bool _automaticTrackSelectionPending;
    private ContextMenuStrip? _fullScreenActiveMenu;
    private Point _lastFullScreenCursorPosition;
    private DateTime _fullScreenCursorLastMovedAt;
    private bool _isFullScreenCursorHidden;
    private bool _subtitlesVisible = true;
    private bool _transparentSubtitles = true;
    private bool _subtitleTraceEnabled;
    private readonly UnsortedSubtitleWarningTracker _unsortedSubtitleWarningTracker = new();
    private readonly SubtitlePlaybackTracker _subtitlePlaybackTracker = new();
    private int _tracedSubtitleLineIndex = -1;
    private DirectoryVideoSortOrder _directoryVideoSortOrder = DirectoryVideoSortOrder.Name;
    private string? _randomDirectoryPath;
    private List<string>? _randomDirectoryVideoOrder;
    private ToolStripMenuItem? _closeVideoMenuItem;
    private ToolStripMenuItem? _goToVideoPositionMenuItem;
    private ToolStripMenuItem? _measureSubtitleDelayMenuItem;
    private ToolStripMenuItem? _measureInitialSubtitleDelayMenuItem;
    private ToolStripMenuItem? _measureFinalSubtitleDelayMenuItem;
    private ToolStripMenuItem? _geometricSubtitleShiftMenuItem;
    private ToolStripMenuItem? _convertSubtitleFormatMenuItem;
    private ToolStripMenuItem? _correctSubtitlesMenuItem;
    private ToolStripMenuItem? _goToSubtitleMenuItem;
    private ToolStripMenuItem? _shiftSubtitleTimeMenuItem;
    private ToolStripMenuItem? _changeSubtitleDisplayTimeMenuItem;
    private ToolStripMenuItem? _rapidSubtitleMergeMenuItem;
    private ToolStripMenuItem? _splitSubtitlesMenuItem;
    private ToolStripMenuItem? _mergeSubtitlesMenuItem;
    private ToolStripMenuItem? _splitLongSubtitleLinesMenuItem;
    private ToolStripMenuItem? _limitSubtitleLinesMenuItem;
    private ToolStripMenuItem? _changeSubtitleFrameRateMenuItem;
    private ToolStripMenuItem? _sortSubtitlesMenuItem;
    private ToolStripMenuItem? _addHalfSecondMenuItem;
    private ToolStripMenuItem? _subtractHalfSecondMenuItem;
    private ToolStripMenuItem? _jumpToCurrentTimeSubtitleMenuItem;
    private ToolStripMenuItem? _subtitleTraceMenuItem;
    private ToolStripMenuItem? _defaultSubtitleFormatMenuItem;
    private ToolStripMenuItem? _subtitleVisibilityMenuItem;
    private ToolStripMenuItem? _transparentSubtitlesMenuItem;
    private ToolStripMenuItem? _timeCounterMenuItem;
    private ToolStripButton? _activeSubtitleFormatButton;
    private FormBorderStyle _restoreBorderStyle;
    private FormWindowState _restoreWindowState;
    private Rectangle _restoreBounds;
    private VideoDecoderMode _videoDecoderMode;
    private double _sessionFrameRate = 25d;
    private bool _sessionFrameRateOverridden;

    private enum DirectoryVideoSortOrder
    {
        Name,
        Extension,
        Random
    }

    private enum VideoDecoderMode
    {
        Unknown,
        Software,
        Hardware
    }

    internal Form1(
        AppSettings settings,
        PortableSettingsStore settingsStore,
        IPlaybackEngineFactory engineFactory,
        string? startupVideoPath = null)
    {
        ArgumentNullException.ThrowIfNull(engineFactory);

        _settings = settings;
        _settingsStore = settingsStore;
        _startupVideoPath = startupVideoPath;
        _transparentSubtitles = _settings.TransparentSubtitles;

        InitializeComponent();
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        _playback = engineFactory.CreateEngine(
            this,
            _settings.Volume,
            () => _settings.Playback.VideoDecodingMode);
        ApplyWindowSettings();
        FormClosing += OnFormClosing;
        Deactivate += OnApplicationDeactivated;
        Activated += OnApplicationActivated;
        Application.EnterThreadModal += OnApplicationEnterThreadModal;
        Application.LeaveThreadModal += OnApplicationLeaveThreadModal;

        _menu = CreateMenu();
        _mainToolbar = CreateMainToolbar();
        _statusStrip = CreateStatusStrip(out _fileStatus);
        _fileStatus.AutoToolTip = true;
        _mDVDStatus = (ToolStripStatusLabel)_statusStrip.Items[0];
        _mpl2Status = (ToolStripStatusLabel)_statusStrip.Items[1];
        _positionStatus = (ToolStripStatusLabel)_statusStrip.Items[2];
        _lengthStatus = (ToolStripStatusLabel)_statusStrip.Items[3];
        _decoderStatus = (ToolStripStatusLabel)_statusStrip.Items[5];
        _decoderStatus.TextAlign = ContentAlignment.MiddleCenter;
        _sizeStatus = (ToolStripStatusLabel)_statusStrip.Items[6];
        _sizeStatus.TextAlign = ContentAlignment.MiddleCenter;
        _fpsStatus = (ToolStripStatusLabel)_statusStrip.Items[7];
        _fpsStatus.TextAlign = ContentAlignment.MiddleCenter;
        _statusStrip.SizeChanged += (_, _) =>
        {
            LayoutEditorStatusDetails();
            RefreshFilePathStatus();
        };
        _splitContainer = CreateSplitContainer(
            out _videoPanel,
            out _editorPanel,
            out _transportToolbar,
            out _volumeSlider,
            out _seekSlider,
            out var subtitleEditor,
            out var editorHeader,
            out var subtitleInfoBar);
        _fullScreenControlsPanel = CreateFullScreenControlsPanel(
            out _fullScreenStatusStrip,
            out _fullScreenPlaybackStatus,
            out _fullScreenFileNameStatus,
            out _fullScreenTimeStatus);
        _subtitleEditor = subtitleEditor;
        _subtitleEditor.ApplyEditorSettings(_settings.Editor);
        _subtitleEditor.NativeUndoRedoPerformed += (_, _) => ClearRememberedSubtitleTimeScaleTransform();
        _editorHeader = editorHeader;
        _editorPanel.Resize += (_, _) => UpdateSubtitleHeader();
        _subtitleInfoBar = subtitleInfoBar;
        UpdateResponsiveLayout();
        _playButton = TransportButton(PlaybackToolbarCommand.Play);
        _pauseButton = TransportButton(PlaybackToolbarCommand.Pause);
        _playDirectoryButton = TransportButton(PlaybackToolbarCommand.PlayDirectory);
        _subtitleTracksButton = TransportButton(PlaybackToolbarCommand.Subtitles);
        _audioTracksButton = TransportButton(PlaybackToolbarCommand.Audio);
        _videoTracksButton = TransportButton(PlaybackToolbarCommand.Video);
        _chaptersButton = TransportButton(PlaybackToolbarCommand.Chapters);
        _volumeButton = TransportButton(PlaybackToolbarCommand.Volume);
        _volumeBeforeMute = Math.Clamp(_settings.LastAudibleVolume, 1, 100);
        _volumeSlider.Value = Math.Clamp(_settings.Volume, _volumeSlider.Minimum, _volumeSlider.Maximum);
        _volumeSlider.ValueChanged += (_, _) => SetVolume(_volumeSlider.Value);
        _volumeSlider.Scroll += (_, _) => ShowVolumeOsd();
        SetVolume(_volumeSlider.Value);
        _volumeSlider.WheelStep = AdjustVolume;
        _seekSlider.WheelStep = AdjustSeek;
        _seekSlider.InteractionStarted += (_, _) =>
        {
            InvalidatePendingRelativeSeek();
            _pendingResumePosition = null;
            _pendingResumePrecise = false;
        };
        _seekSlider.PreviewSeekRequested += (_, _) =>
        {
            if (!_isStopped)
            {
                SeekToSliderPosition(precise: false);
            }
        };
        _seekSlider.SeekCommitted += (_, _) =>
        {
            SeekToSliderPosition(precise: true);
        };
        _seekSlider.MouseEnter += (_, _) => ShowFullScreenControls();
        UpdateTransportButtons();

        Controls.Add(_splitContainer);
        Controls.Add(_statusStrip);
        Controls.Add(_mainToolbar);
        Controls.Add(_menu);

        MainMenuStrip = _menu;
        KeyPreview = true;
        WirePlayerCommands();
        _videoHost = engineFactory.CreateVideoHost(_videoPanel, _playback, CreateVideoMenu());
        _videoPanel.Controls.Add(_fullScreenControlsPanel);
        _fullScreenControlsPanel.BringToFront();
        ConfigureFileDropTargets(this);
        _videoPanel.Resize += (_, _) =>
        {
            ApplyMovieTransformations();
            LayoutFullScreenControls();
        };
        _videoOverlay = new VideoOverlayForm();
        ApplyOverlaySettings();
        ApplyMovieTransformations();
        _subtitleEditor.SavePointLeft += (_, _) =>
        {
            if (_isLoadingSubtitle)
            {
                return;
            }

            _subtitleTextDirty = true;
            UpdateSubtitleDirtyState();
        };
        _subtitleEditor.SavePointReached += (_, _) =>
        {
            if (_isLoadingSubtitle)
            {
                return;
            }

            _subtitleTextDirty = false;
            UpdateSubtitleDirtyState();
        };
        _subtitleEditor.TextChanged += (_, _) =>
        {
            UpdateUndoRedoAvailability();
            if (_isLoadingSubtitle)
            {
                return;
            }

            if (!_restoringFrameInsertionCaretPosition)
            {
                _frameInsertionUndoCaretPosition = null;
                _frameInsertionRedoCaretPosition = null;
                _frameInsertionUndoApplied = false;
            }

            _unsortedSubtitleWarningTracker.Reset();
            RefreshSubtitleDocument();
            RefreshSubtitleInfoBar();
            UpdateSubtitleConversionAvailability();
        };
        _subtitleEditor.UpdateUI += (_, _) =>
        {
            RefreshSubtitleInfoBar();
            UpdateUndoRedoAvailability();
        };
        _subtitleEditor.UndoHistoryCleared += (_, _) => UpdateUndoRedoAvailability();
        UpdateUndoRedoAvailability();
        RefreshSubtitleInfoBar();
        UpdateSubtitleConversionAvailability();
        _videoInputFilter = new VideoInputMessageFilter(
            () => !OwnedForms.Any(form => form.Visible && form != _videoOverlay),
            IsVideoSurfacePoint,
            ToggleFullScreenFromVideoSurface,
            ShowEditorOptionsDialog);
        Application.AddMessageFilter(_videoInputFilter);
        _fullScreenWheelHook = new FullScreenWheelHook(
            () => Visible && ContainsFocus,
            TryHandlePlayerWheel);
        _playbackTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _playbackTimer.Tick += (_, _) => UpdatePlaybackUi();
        _playbackTimer.Start();
        _seekRepeatTimer = new System.Windows.Forms.Timer { Interval = RelativeSeekQueue.TimerIntervalMilliseconds };
        _seekRepeatTimer.Tick += (_, _) => ProcessQueuedSeek();
        _autoSaveTimer = new System.Windows.Forms.Timer();
        _autoSaveTimer.Tick += (_, _) => TryAutoSaveSubtitle();
        ConfigureAutoSaveTimer();
        _fullScreenControlsTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _fullScreenControlsTimer.Tick += (_, _) =>
        {
            UpdateFullScreenMouseCursor();
            UpdateFullScreenControlsFromPointer();
        };
        _playback.PlaybackEnded += (_, e) =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                var eventSessionId = e.SessionId;
                BeginInvoke(() => HandlePlaybackEnded(eventSessionId));
            }
        };
        _playback.MediaInfoChanged += (_, _) =>
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            _mediaDetailsAvailable = false;
            ApplyInitialTrackSelection();
        };
        _playback.TracksReady += (_, _) =>
        {
            if (!IsDisposed && IsHandleCreated)
            {
                ApplyInitialTrackSelection();
                RefreshAudioTrackStatus();
                UpdateSeekBarChapters();
            }
        };
        _playback.StateChanged += (_, _) =>
        {
            if (_playback.State == PlaybackState.Paused && !IsDisposed && IsHandleCreated)
            {
                BeginInvoke(ApplyPendingSeekAfterPause);
            }
        };
        if (_playback is IEmbeddedTextSubtitleSource embeddedSubtitleSource)
        {
            embeddedSubtitleSource.EmbeddedSubtitleTextChanged += OnPlaybackEmbeddedSubtitleTextChanged;
        }
        Shown += (_, _) =>
        {
            SetInitialSplitterDistance();
            if (!_fullScreenWheelHook.TryEnable())
            {
                ShowTemporaryFileStatus("Obsługa kółka myszy jest niedostępna");
            }
            _videoOverlay.Show(this);
            UpdateTimeCounterOsd(_playback.PositionMilliseconds, _playback.DurationMilliseconds, force: true);
            SyncVideoOverlay();
            BeginInvoke(OpenStartupVideo);
            BeginInvoke(RefreshInitialLastVideoStatus);
            BeginInvoke(UpdateSubtitleHeader);
            BeginInvoke(() =>
            {
                BringApplicationToFront();
                _subtitleEditor.Focus();
            });
        };
        Move += (_, _) => SyncVideoOverlay();
        Resize += (_, _) =>
        {
            UpdatePlaybackForWindowState();
            SyncVideoOverlay();
            if (_currentVideoPath is null)
            {
                RefreshInitialLastVideoStatus();
            }
            else
            {
                RefreshFilePathStatus();
            }

            UpdateSubtitleHeader();
        };
        DpiChanged += (_, _) => BeginInvoke(UpdateResponsiveLayout);
        _videoHost.VideoResized += (_, _) => SyncVideoOverlay();
    }

    private void UpdatePlaybackForWindowState()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            if (_settings.Playback.PauseOnMinimize && _playback.State == PlaybackState.Playing)
            {
                _playback.Pause();
                _isStopped = false;
                _resumePlaybackAfterRestore = true;
                UpdateTransportButtons();
            }

            return;
        }

        if (_resumePlaybackAfterRestore)
        {
            _resumePlaybackAfterRestore = false;
            Play();
        }
    }

    private void OnApplicationDeactivated(object? sender, EventArgs e)
    {
        _seekSlider.CancelInteraction();
        _wheelSeekDeltaAccumulator.Reset();
        var activationGeneration = ++_applicationActivationGeneration;
        BeginInvoke(() => PauseForExternalApplicationDeactivation(activationGeneration));
    }

    private void PauseForExternalApplicationDeactivation(int activationGeneration)
    {
        if (activationGeneration != _applicationActivationGeneration ||
            _modalDialogDepth > 0 ||
            Form.ActiveForm is not null)
        {
            return;
        }

        if (_settings.Playback.PauseOnDeactivate && _playback.State == PlaybackState.Playing)
        {
            _playback.Pause();
            _isStopped = false;
            _resumePlaybackAfterApplicationActivation = true;
            UpdateTransportButtons();
        }
    }

    private void OnApplicationActivated(object? sender, EventArgs e)
    {
        _applicationActivationGeneration++;

        if (!_resumePlaybackAfterApplicationActivation)
            return;

        _resumePlaybackAfterApplicationActivation = false;

        if (_playback.IsMediaOpen && !_isStopped)
        {
            _playback.Play();
            UpdateTransportButtons();
        }
    }

    private void OnApplicationEnterThreadModal(object? sender, EventArgs e)
    {
        _modalDialogDepth++;
        if (_modalDialogDepth != 1 ||
            !_settings.Playback.PauseWhenDialogDisplayed ||
            _playback.State != PlaybackState.Playing)
        {
            return;
        }

        _playback.Pause();
        _isStopped = false;
        _resumePlaybackAfterModalDialog = true;
        UpdateTransportButtons();
    }

    private void OnApplicationLeaveThreadModal(object? sender, EventArgs e)
    {
        _modalDialogDepth = Math.Max(0, _modalDialogDepth - 1);
        if (_modalDialogDepth != 0 || !_resumePlaybackAfterModalDialog)
        {
            return;
        }

        _resumePlaybackAfterModalDialog = false;
        if (_playback.IsMediaOpen && !_isStopped && _playback.State == PlaybackState.Paused)
        {
            _playback.Play();
            UpdateTransportButtons();
        }
    }

    private void UpdateResponsiveLayout()
    {
        if (IsDisposed)
        {
            return;
        }

        _statusStrip.AutoSize = false;
        _statusStrip.Height = Math.Max(
            ScaleLogicalPixels(22),
            TextRenderer.MeasureText("Hg", _statusStrip.Font, Size.Empty, TextFormatFlags.NoPadding).Height + ScaleLogicalPixels(6));
        _mDVDStatus.Width = ScaleLogicalPixels(102);
        _mpl2Status.Width = ScaleLogicalPixels(92);
        _positionStatus.Width = ScaleLogicalPixels(96);
        _lengthStatus.Width = ScaleLogicalPixels(96);
        _sizeStatus.Width = ScaleLogicalPixels(78);
        _fpsStatus.Width = ScaleLogicalPixels(68);
        LayoutEditorStatusDetails();

        _editorHeader.Height = Math.Max(
            ScaleLogicalPixels(20),
            TextRenderer.MeasureText("Hg", _editorHeader.Font, Size.Empty, TextFormatFlags.NoPadding).Height + ScaleLogicalPixels(6));
        AlignEditorHeader(_editorHeader, _subtitleEditor);
        _subtitleInfoBar.UpdateDpiLayout();
        RefreshFilePathStatus();
        UpdateSubtitleHeader();
        LayoutFullScreenControls();
    }

    private int ScaleLogicalPixels(int value) =>
        Math.Max(1, (int)Math.Round(value * DeviceDpi / 96d));

    private static string FormatFrameRate(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private void WirePlayerCommands()
    {
        ToolbarButton("toolbarOpenVideo").Click += (_, _) => OpenVideo();
        ToolbarButton("toolbarOpenSubtitles").Click += (_, _) => OpenSubtitleFile();
        ToolbarButton("toolbarNew").Click += (_, _) => NewSubtitle();
        ToolbarButton("toolbarSave").Click += (_, _) => SaveSubtitle();
        ToolbarButton("toolbarUndo").Click += (_, _) => UndoSubtitleEdit();
        ToolbarButton("toolbarRedo").Click += (_, _) => RedoSubtitleEdit();
        ToolbarButton("toolbarEditor").Click += (_, _) => ToggleEditor();
        ToolbarButton("toolbarOptions").Click += (_, _) => ShowEditorOptionsDialog();
        ToolbarButton("toolbarSubtitleTrace").Click += (_, _) => ToggleSubtitleTrace();
        ToolbarButton("toolbarConvert").Click += (_, _) => ShowSubtitleConversionDialog();
        ToolbarButton("toolbarAddTime").Click += (_, _) => ShowSubtitleTimeShiftDialog();
        ToolbarButton("toolbarShowTime").Click += (_, _) => ShowSubtitleDisplayTimeDialog();
        ToolbarButton("toolbarTransparentSubtitles").Click += (_, _) => ToggleSubtitleTransparency();
        ToolbarButton("toolbarShowSubtitles").Click += (_, _) => ToggleSubtitleVisibility();
        ToolbarButton("toolbarAlwaysOnTop").Click += (_, _) => ToggleAlwaysOnTop();
        ToolbarButton("toolbarFullscreen").Click += (_, _) => ToggleFullScreen();
        ToolbarButton("toolbarClose").Click += (_, _) => Close();
        TransportButton(PlaybackToolbarCommand.Play).Click += (_, _) => Play();
        TransportButton(PlaybackToolbarCommand.Pause).Click += (_, _) => Pause();
        TransportButton(PlaybackToolbarCommand.Stop).Click += (_, _) => StopPlayback();
        TransportButton(PlaybackToolbarCommand.SeekBackward).Click += (_, _) => QueuePreciseSeekWithAccumulatedOsd(-5_000);
        TransportButton(PlaybackToolbarCommand.SeekForward).Click += (_, _) => QueuePreciseSeekWithAccumulatedOsd(5_000);
        TransportButton(PlaybackToolbarCommand.PreviousFile).Click += (_, _) => PlayAdjacentVideo(-1);
        TransportButton(PlaybackToolbarCommand.NextFile).Click += (_, _) => PlayAdjacentVideo(1);
        _playDirectoryButton.Click += (_, _) => ToggleDirectoryPlayback();
        _subtitleTracksButton.Click += (_, _) => ShowSubtitleTracksMenu();
        _audioTracksButton.Click += (_, _) => ShowAudioTracksMenu();
        _videoTracksButton.Click += (_, _) => ShowVideoTracksMenu();
        _chaptersButton.Click += (_, _) => ShowChaptersMenu();
        _volumeButton.Click += (_, _) => ToggleMute();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaActivate;
            return;
        }

        base.WndProc(ref m);
    }

    private static bool IsScintillaControlNavigationShortcut(Keys keyData)
    {
        var modifiers = keyData & Keys.Modifiers;
        if (modifiers is not Keys.Control and not (Keys.Control | Keys.Shift))
        {
            return false;
        }

        return (keyData & Keys.KeyCode) is
            Keys.Back or Keys.Delete or Keys.Insert or
            Keys.Left or Keys.Right or Keys.Up or Keys.Down or
            Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown;
    }

    private void AdjustSeek(int wheelDelta)
    {
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (length <= 0)
        {
            _wheelSeekDeltaAccumulator.Reset();
            return;
        }

        var offsetMilliseconds = _wheelSeekDeltaAccumulator.ProcessDelta(wheelDelta);
        if (offsetMilliseconds != 0)
        {
            QueuePreciseSeekWithAccumulatedOsd(offsetMilliseconds);
        }
    }

    private void SeekUserBackward() => SeekByUserPreference(-1, _settings.Playback.UserSeekBackwardSeconds);

    private void SeekUserForward() => SeekByUserPreference(1, _settings.Playback.UserSeekForwardSeconds);

    private void SeekByUserPreference(int direction, int seconds)
    {
        var offsetMilliseconds = direction * Math.Max(1L, seconds) * 1_000L;
        QueuePreciseSeekWithAccumulatedOsd(offsetMilliseconds);
    }

    private bool TryHandlePlayerWheel(Point pointer, int wheelDelta)
    {
        if (wheelDelta == 0)
        {
            return false;
        }

        var controlsAreVisible = !_isFullScreen || _fullScreenControlsPanel.Visible;
        if (controlsAreVisible &&
            _volumeSlider.RectangleToScreen(_volumeSlider.ClientRectangle).Contains(pointer))
        {
            AdjustVolume(wheelDelta);
            return true;
        }

        if (controlsAreVisible &&
            _seekSlider.RectangleToScreen(_seekSlider.ClientRectangle).Contains(pointer))
        {
            AdjustSeek(wheelDelta);
            return true;
        }

        if (controlsAreVisible &&
            (_transportToolbar.RectangleToScreen(_transportToolbar.ClientRectangle).Contains(pointer) ||
            (_isFullScreen && _fullScreenControlsPanel.Visible &&
                _fullScreenControlsPanel.RectangleToScreen(_fullScreenControlsPanel.ClientRectangle).Contains(pointer))))
        {
            return true;
        }

        if (_videoPanel.RectangleToScreen(_videoPanel.ClientRectangle).Contains(pointer))
        {
            AdjustVolume(wheelDelta);
            return true;
        }

        return false;
    }

    // Purposeful M2TS EOF safety guard:
    // In M2TS / MPEG-TS containers, nominal container Duration reported by the demuxer
    // can extend past the presentation timestamp (PTS) of the last available video frame.
    // Seeking too close to nominal EOF (target >= length - 1000 ms) causes the demuxer/decoder
    // to reach end-of-stream without acquiring the target video frame, leaving Flyleaf playback
    // in an unstable or frozen state (reproduced also in upstream FlyleafPlayer).
    // The 1-second margin (length - 1000 ms) is SubEdit's preserved, empirically verified safety
    // threshold and MUST NOT be simplified to the standard (length - 100 ms) non-M2TS clamp.
    // Manual and programmatic seeks are blocked within this 1000 ms window for .m2ts only;
    // natural playback is permitted to reach EOF normally.
    private bool IsUnsafeManualSeekNearEnd(long targetPosition, long length) =>
        IsUnsafeManualSeekNearEnd(_currentVideoPath, targetPosition, length);

    internal static bool IsUnsafeManualSeekNearEnd(string? videoPath, long targetPosition, long length)
    {
        if (length <= 0)
        {
            return false;
        }

        if (string.Equals(Path.GetExtension(videoPath), ".m2ts", StringComparison.OrdinalIgnoreCase))
        {
            return targetPosition >= Math.Max(0, length - 1000);
        }

        return false;
    }

    // Flyleaf EOF workaround for non-M2TS media:
    // Seeking exactly to or past Duration can leave playback in an unstable EOF state.
    // For non-M2TS media, manual targets that reach or exceed Duration are capped 100 ms
    // before EOF (length - 100 ms). Targets below Duration remain untouched.
    // For .m2ts, targets are returned as-is because IsUnsafeManualSeekNearEnd handles the 1000 ms guard.
    // The 100 ms margin is empirically validated in SubEdit tests and is not a Flyleaf API guarantee.
    private long NormalizeManualSeekTarget(long targetPosition, long length) =>
        NormalizeManualSeekTarget(_currentVideoPath, targetPosition, length);

    internal static long NormalizeManualSeekTarget(string? videoPath, long targetPosition, long length)
    {
        if (length <= 0)
        {
            return Math.Max(0, targetPosition);
        }

        if (string.Equals(Path.GetExtension(videoPath), ".m2ts", StringComparison.OrdinalIgnoreCase))
        {
            return targetPosition;
        }

        if (targetPosition >= length)
        {
            return Math.Max(0, length - 100);
        }

        return targetPosition;
    }

    private void InvalidatePendingRelativeSeek()
    {
        _seekRepeatTimer.Stop();
        _relativeSeekQueue.Reset();
        _wheelSeekDeltaAccumulator.Reset();
    }

    private void SeekToPosition(long targetPosition, long length, bool showVideoEndPosition = false, bool forward = false, bool precise = false)
    {
        if (IsUnsafeManualSeekNearEnd(targetPosition, length))
        {
            return;
        }

        targetPosition = NormalizeManualSeekTarget(targetPosition, length);
        targetPosition = Math.Clamp(targetPosition, 0, length);
        MarkPastUnsortedSubtitleWarnings(targetPosition);
        _showVideoEndPosition = showVideoEndPosition;
        var presentationPosition = showVideoEndPosition ? length : targetPosition;

        if (_isStopped && _playback.IsMediaOpen)
        {
            _stoppedSeekPosition = targetPosition;
            UpdateSeekPresentation(presentationPosition, length);
            if (precise)
            {
                _playback.SeekPrecise(targetPosition);
            }
            else
            {
                _playback.Seek(targetPosition, forward);
            }
            return;
        }

        if (precise)
        {
            _playback.SeekPrecise(targetPosition);
        }
        else
        {
            _playback.Seek(targetPosition, forward);
        }
        UpdateSeekPresentation(presentationPosition, length);
    }

    private bool CanUsePlayerOnlyShortcut() => _isFullScreen || _splitContainer.Panel2Collapsed;

    private bool CanUseFrameStepShortcut() => CanUsePlayerOnlyShortcut();

    private void SeekToBoundary(bool toEnd)
    {
        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (length <= 0)
        {
            return;
        }

        if (toEnd)
        {
            var endPosition = NormalizeManualSeekTarget(length, length);
            if (IsUnsafeManualSeekNearEnd(endPosition, length))
            {
                if (_playback.State == PlaybackState.Playing)
                {
                    _playback.Pause();
                    _isStopped = false;
                    _subtitleEditor.ReadOnly = false;
                    ClearSubtitleTraceSelection();
                    UpdateSeekPresentation(length, length);
                    UpdateTransportButtons();
                }
                ShowPlaybackOsd("Koniec filmu");
                return;
            }

            if (_playback.State == PlaybackState.Playing)
            {
                _pendingPausedSeekPosition = endPosition;
                _pendingPausedSeekPrecise = true;
                _showVideoEndPosition = true;
                _playback.Pause();
                _isStopped = false;
                _subtitleEditor.ReadOnly = false;
                ClearSubtitleTraceSelection();
                UpdateSeekPresentation(length, length);
                UpdateTransportButtons();
            }
            else
            {
                SeekToPosition(endPosition, length, showVideoEndPosition: true, precise: true);
            }
        }
        else
        {
            SeekToPosition(0, length, precise: true);
        }
        ShowPlaybackOsd(toEnd ? "Koniec filmu" : "Początek filmu");
    }

    private void StepFrame(bool forward)
    {
        if (!_playback.IsMediaOpen)
        {
            return;
        }

        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        _showVideoEndPosition = false;

        if (forward)
        {
            _playback.StepFrameForward();
        }
        else
        {
            _playback.StepFrameBackward();
        }

        _isStopped = false;
        _subtitleEditor.ReadOnly = false;
        ClearSubtitleTraceSelection();

        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        var actualPosition = Math.Clamp(_playback.PositionMilliseconds, 0, length);

        MarkPastUnsortedSubtitleWarnings(actualPosition);
        UpdateSeekPresentation(actualPosition, length);
        PositionSubtitleTraceAfterTimeChange(actualPosition, playbackIsStarting: false);
        ShowPlaybackOsd(forward ? "Klatka w przód" : "Klatka wstecz");
        UpdateTransportButtons();
    }

    private long QueueSeekByMilliseconds(long offset, string? osdText = null, bool precise = false)
    {
        _seekSlider.CancelInteraction();
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (length <= 0)
        {
            return 0;
        }

        var currentPosition = _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds;
        var mode = precise ? RelativeSeekMode.Precise : RelativeSeekMode.Fast;
        var appliedOffset = _relativeSeekQueue.Enqueue(offset, currentPosition, length, mode, Environment.TickCount64);

        ProcessQueuedSeek();
        _seekRepeatTimer.Start();
        if (osdText is not null)
        {
            ShowPlaybackOsd(osdText);
        }

        return appliedOffset;
    }

    private void QueuePreciseSeekWithAccumulatedOsd(long offset)
    {
        var appliedOffset = QueueSeekByMilliseconds(offset, precise: true);
        ShowAccumulatedSeekOsd(appliedOffset);
    }

    private void ProcessQueuedSeek()
    {
        var now = Environment.TickCount64;
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);

        if (_relativeSeekQueue.TryGetDispatchAction(now, out var targetPosition, out var forward, out var mode))
        {
            if (!IsUnsafeManualSeekNearEnd(targetPosition, length))
            {
                SeekToPosition(targetPosition, length, forward: forward, precise: mode == RelativeSeekMode.Precise);
            }
            else
            {
                var actualPosition = _isStopped
                    ? Math.Clamp(_stoppedSeekPosition, 0, length)
                    : Math.Clamp(_playback.PositionMilliseconds, 0, length);
                _relativeSeekQueue.SyncCurrentPosition(actualPosition);
            }
        }

        _relativeSeekQueue.CheckExpiration(now);
        if (!_relativeSeekQueue.HasPendingInput)
        {
            _seekRepeatTimer.Stop();
        }
    }

    private void ApplyPendingSeekAfterPause()
    {
        if (_pendingPausedSeekPosition is { } targetPosition)
        {
            var isPrecise = _pendingPausedSeekPrecise;
            _pendingPausedSeekPosition = null;
            _pendingPausedSeekPrecise = false;
            SeekToPosition(
                targetPosition,
                Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds),
                showVideoEndPosition: true,
                precise: isPrecise);
            _isStopped = false;
            UpdateTransportButtons();
        }
    }

    private void AdjustSubtitleFontSize(int change)
    {
        var fontSize = Math.Clamp(_settings.SubtitleFontSize + change, 8, 72);
        _settings.SubtitleFontSize = fontSize;
        _videoOverlay.SubtitleFontSize = fontSize;
        ShowSubtitlePreviewOsd($"Przykładowy tekst napisu — {fontSize} pkt");
    }

    private void AdjustMovieElevation(int change)
    {
        var elevation = Math.Clamp(_movieElevation + change, -250, 250);
        _movieElevation = elevation;
        ApplyMovieElevation();
        ShowPlaybackOsd($"Podwyższenie filmu: {elevation} px");
    }

    private void AdjustMovieHorizontalOffset(int change)
    {
        var offset = Math.Clamp(_movieHorizontalOffset + change, -250, 250);
        _movieHorizontalOffset = offset;
        ApplyMovieHorizontalOffset();
        ShowPlaybackOsd($"Przesunięcie poziome filmu: {offset} px");
    }

    private void AdjustMovieZoom(int change)
    {
        var zoom = Math.Clamp(_movieZoomPercent + change, 10, 500);
        _movieZoomPercent = zoom;
        ApplyMovieZoom();
        ShowPlaybackOsd($"Powiększenie filmu: {zoom}%");
    }

    private void ResetMovieTransformations()
    {
        _movieHorizontalOffset = 0;
        _movieElevation = 0;
        _movieZoomPercent = 100;
        ApplyMovieTransformations();
        ShowPlaybackOsd("Transformacje obrazu: zresetowane");
    }

    private void AdjustPlaybackSpeed(int change)
    {
        var newMultiplier = Math.Clamp(_playbackSpeedMultiplier + change, 25, 400);
        _playbackSpeedMultiplier = newMultiplier;
        ApplyPlaybackSpeed();
        ShowPlaybackSpeedOsd();
        SynchronizeSmoothedPosition(_playback.PositionMilliseconds);
    }

    private void DecreasePlaybackSpeed() => AdjustPlaybackSpeed(-25);

    private void IncreasePlaybackSpeed() => AdjustPlaybackSpeed(25);

    private void ResetPlaybackSpeed()
    {
        _playbackSpeedMultiplier = 100;
        ApplyPlaybackSpeed();
        ShowPlaybackSpeedOsd();
        SynchronizeSmoothedPosition(_playback.PositionMilliseconds);
    }

    private void ApplyPlaybackSpeed()
    {
        _playback.Speed = _playbackSpeedMultiplier / 100.0;
    }

    private void ShowPlaybackSpeedOsd()
    {
        var speedFormatted = (_playbackSpeedMultiplier / 100.0).ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pl-PL"));
        ShowPlaybackOsd($"Prędkość odtwarzania: {speedFormatted}×");
    }

    private void ApplyMovieElevation()
    {
        if (_videoPanel.ClientSize.Width <= 0 || _videoPanel.ClientSize.Height <= 0)
        {
            return;
        }

        _videoHost.ApplyVerticalOffset(_movieElevation);
    }

    private void ApplyMovieHorizontalOffset()
    {
        if (_videoPanel.ClientSize.Width <= 0 || _videoPanel.ClientSize.Height <= 0)
        {
            return;
        }

        _videoHost.ApplyHorizontalOffset(_movieHorizontalOffset);
    }

    private void ApplyMovieZoom()
    {
        if (_videoPanel.ClientSize.Width <= 0 || _videoPanel.ClientSize.Height <= 0)
        {
            return;
        }

        _videoHost.ApplyZoom(_movieZoomPercent);
    }

    private void ApplyMovieTransformations()
    {
        ApplyMovieHorizontalOffset();
        ApplyMovieElevation();
        ApplyMovieZoom();
    }

    private void SeekToSliderPosition(int? sliderValue = null, bool precise = false)
    {
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (_updatingSeekSlider || length <= 0)
        {
            return;
        }

        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        var valueToUse = sliderValue ?? _seekSlider.IntendedValue ?? _seekSlider.Value;
        var targetPosition = (long)Math.Round((valueToUse / (double)SeekSliderMaximum) * length);
        if (IsUnsafeManualSeekNearEnd(targetPosition, length))
        {
            var currentPosition = _isStopped
                ? Math.Clamp(_stoppedSeekPosition, 0, length)
                : Math.Clamp(_playback.PositionMilliseconds, 0, length);
            _updatingSeekSlider = true;
            _seekSlider.Value = Math.Clamp(
                (int)Math.Round((currentPosition / (double)length) * SeekSliderMaximum),
                _seekSlider.Minimum,
                _seekSlider.Maximum);
            _updatingSeekSlider = false;
            return;
        }

        targetPosition = NormalizeManualSeekTarget(targetPosition, length);
        SeekToPosition(targetPosition, length, precise: precise);
        PositionSubtitleTraceAfterTimeChange(targetPosition, playbackIsStarting: false);
    }

    private static bool EndsWithLineBreak(string text) =>
        text.EndsWith('\n') || text.EndsWith('\r');

    private static string GetEditorLineBreak(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private void ClearRecentFiles()
    {
        using var dialog = new QuestionDialog("Czy wyczyścić historię ostatnio otwieranych plików?", includeCancel: false);
        if (dialog.ShowDialog(this) != DialogResult.Yes)
        {
            return;
        }

        _settings.Playback.RecentVideoPaths.Clear();
        _settings.Playback.RecentSubtitlePaths.Clear();
    }

    private void UpdateSeekBarChapters()
    {
        if (!_playback.IsMediaOpen)
        {
            _seekSlider.DurationMilliseconds = 0;
            _seekSlider.ChapterMarkers = Array.Empty<long>();
            return;
        }

        _seekSlider.DurationMilliseconds = Math.Max(0, _playback.DurationMilliseconds);
        _seekSlider.ChapterMarkers = _playback.Chapters
            .Select(chapter => chapter.PositionMilliseconds)
            .ToArray();
    }

    private void SeekToChapter(int chapterIndex, long chapterPosition)
    {
        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (IsUnsafeManualSeekNearEnd(chapterPosition, length))
        {
            return;
        }

        var targetPosition = NormalizeManualSeekTarget(chapterPosition, length);
        if (targetPosition != chapterPosition)
        {
            SeekToPosition(targetPosition, length, precise: true);
            PositionSubtitleTraceAfterTimeChange(targetPosition, playbackIsStarting: false);
            return;
        }

        if (_isStopped && _playback.IsMediaOpen)
        {
            _stoppedSeekPosition = targetPosition;
            UpdateSeekPresentation(targetPosition, length);
            _playback.SelectChapter(chapterIndex);
        }
        else
        {
            _playback.SelectChapter(chapterIndex);
            UpdateSeekPresentation(targetPosition, length);
        }

        MarkPastUnsortedSubtitleWarnings(targetPosition);
        PositionSubtitleTraceAfterTimeChange(targetPosition, playbackIsStarting: false);
    }

    private void ShowGoToVideoPositionDialog()
    {
        if (!_playback.IsMediaOpen)
        {
            return;
        }

        var currentPosition = _isStopped
            ? _stoppedSeekPosition
            : Math.Max(0, _playback.PositionMilliseconds);
        using var dialog = new GoToVideoPositionDialog(GetVideoFrameRate(), currentPosition);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        InvalidatePendingRelativeSeek();

        var length = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
        if (length <= 0)
        {
            ShowTemporaryFileStatus("Nie można ustalić długości filmu");
            return;
        }

        var targetPosition = dialog.TargetSeconds >= long.MaxValue / 1000d
            ? length
            : Math.Clamp((long)Math.Round(dialog.TargetSeconds * 1000d), 0, length);
        if (IsUnsafeManualSeekNearEnd(targetPosition, length))
        {
            return;
        }

        targetPosition = NormalizeManualSeekTarget(targetPosition, length);
        SeekToPosition(targetPosition, length, precise: true);
        PositionSubtitleTraceAfterTimeChange(targetPosition, playbackIsStarting: false);
    }

    private void SetSubtitleEditorText(string text)
    {
        ResetSubtitleGeometryMeasurements();
        _isLoadingSubtitle = true;
        try
        {
            _subtitleEditor.ExecuteWritableMutation(() => _subtitleEditor.Text = text);
        }
        finally
        {
            _isLoadingSubtitle = false;
        }

        RefreshSubtitleDocument();
        RefreshSubtitleInfoBar();
        UpdateSubtitleConversionAvailability();
    }

    private static void RememberRecentFile(List<string> paths, string path)
    {
        paths.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        paths.Insert(0, path);
        if (paths.Count > RecentFilesLimit)
        {
            paths.RemoveRange(RecentFilesLimit, paths.Count - RecentFilesLimit);
        }
    }

    private void RefreshSubtitleDocument()
    {
        _subtitleDocument = ParseSubtitleDocument();
        MarkPastUnsortedSubtitleWarnings(_playback.PositionMilliseconds);
        UpdateSubtitleOverlay(_playback.PositionMilliseconds);
    }

    private void MarkPastUnsortedSubtitleWarnings(long position)
    {
        _unsortedSubtitleWarningTracker.Synchronize(_subtitleDocument, position);
        _subtitlePlaybackTracker.Synchronize(_subtitleDocument, position);
    }

    private SubtitleDocument ParseSubtitleDocument() => SubtitleDocument.Parse(
        _subtitleEditor.Text,
        GetVideoFrameRate(),
        _settings.Editing.DefaultFiftyCharacterDisplaySeconds,
        _settings.Editing.AdditionalDefaultDisplaySeconds,
        new SubtitleRenderOptions(
            _settings.SubtitlePlayback.InterpretMicroDvdControlCodes,
            _settings.SubtitlePlayback.ItalicLinesStartingWithSlash,
            _settings.SubtitlePlayback.ItalicLinesStartingWithAsterisk,
            _settings.SubtitlePlayback.ItalicLinesStartingWithHash),
        _settings.SubtitlePlayback.MinimumGapFrames);

    private void Play()
    {
        if (!_playback.IsMediaOpen)
        {
            RestoreLastPlayback();
            return;
        }

        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        var startPosition = _isStopped
            ? _stoppedSeekPosition
            : Math.Max(0, _playback.PositionMilliseconds);
        _isStopped = false;
        _playback.Play();
        _subtitleEditor.ReadOnly = _subtitleTraceEnabled;
        PositionSubtitleTraceAfterTimeChange(startPosition, playbackIsStarting: true);
        if (!_subtitleTraceEnabled && startPosition == 0)
        {
            PositionCaretAtFirstSubtitle();
        }
        ShowPlaybackOsd("Odtwarzanie");
        RefreshFilePathStatus();
        UpdateTransportButtons();
    }

    private void RestoreLastPlayback()
    {
        var playback = _settings.Playback;
        if (string.IsNullOrWhiteSpace(playback.LastVideoPath) || !File.Exists(playback.LastVideoPath))
        {
            ShowTemporaryFileStatus("Brak ostatnio odtwarzanego filmu");
            return;
        }

        LoadVideoFile(playback.LastVideoPath, playback.LastPositionMilliseconds);
        ShowPlaybackOsd("Odtwarzanie");
        PositionCaretAtFirstSubtitle();
    }

    private void Pause()
    {
        if (_playback.State == PlaybackState.Playing)
        {
            _playback.Pause();
            _isStopped = false;
            _subtitleEditor.ReadOnly = false;
            ClearSubtitleTraceSelection();
            ShowPlaybackOsd("Pauza");
            UpdateTransportButtons();
            return;
        }

        Play();
    }

    private void TogglePlayback()
    {
        if (_playback.State == PlaybackState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void StopPlayback()
    {
        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        _lastKnownMediaLengthMilliseconds = Math.Max(
            _lastKnownMediaLengthMilliseconds,
            _playback.DurationMilliseconds);
        _playback.Stop();
        _isStopped = true;
        _subtitleEditor.ReadOnly = false;
        ClearSubtitleTraceSelection();
        _stoppedSeekPosition = 0;
        ResetSeekSliderToStart();
        ShowPlaybackOsd("Stop");
        _settings.Playback.LastPositionMilliseconds = 0;
        _fileStatusExpiresAt = default;
        RefreshFilePathStatus();
        UpdateTransportButtons();
    }

    private void ToggleDirectoryPlayback()
    {
        var osdMessage = _playDirectoryButton.Checked
            ? "Odtwarzaj wszystkie filmy z katalogu"
            : "Nie odtwarzaj wszystkich filmów z katalogu";
        ShowTemporaryFileStatus(_playDirectoryButton.Checked
            ? "Włączono odtwarzanie katalogu"
            : "Wyłączono odtwarzanie katalogu");
        ShowPlaybackOsd(osdMessage);
    }

    private void ToggleDirectoryPlaybackFromShortcut()
    {
        _playDirectoryButton.Checked = !_playDirectoryButton.Checked;
        ToggleDirectoryPlayback();
    }

    private void ToggleLoopPlayback()
    {
        var osdMessage = _loopMenuItem.Checked
            ? "Powtarzaj odtwarzanie"
            : "Nie powtarzaj odtwarzania";
        ShowTemporaryFileStatus(_loopMenuItem.Checked
            ? "Włączono zapętlanie filmu"
            : "Wyłączono zapętlanie filmu");
        ShowPlaybackOsd(osdMessage);
    }

    private void ToggleLoopPlaybackFromShortcut()
    {
        _loopMenuItem.Checked = !_loopMenuItem.Checked;
        ToggleLoopPlayback();
    }

    private void HandlePlaybackEnded(long eventSessionId)
    {
        if (eventSessionId <= 0 || eventSessionId != _playback.SessionId || !_playback.IsMediaOpen)
        {
            return;
        }

        if (_pendingPausedSeekPosition is { } pendingEndFramePosition)
        {
            var isPrecise = _pendingPausedSeekPrecise;
            _pendingPausedSeekPosition = null;
            _pendingPausedSeekPrecise = false;
            _isStopped = true;
            SeekToPosition(
                pendingEndFramePosition,
                Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds),
                showVideoEndPosition: true,
                precise: isPrecise);
            return;
        }

        if (TryLoopCurrentVideo())
        {
            return;
        }

        if (TryAdvanceDirectoryPlayback())
        {
            return;
        }

        if (TryAdvanceNumberedPartPlayback())
        {
            return;
        }

        StopPlaybackAfterEnd(!_playback.IsMediaOpen ? "Brak wczytanego filmu" : "Zatrzymano film");
    }

    private bool TryLoopCurrentVideo()
    {
        if (!_loopMenuItem.Checked || !_playback.IsMediaOpen)
        {
            return false;
        }

        InvalidatePendingRelativeSeek();
        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        _isStopped = false;
        MarkPastUnsortedSubtitleWarnings(0);
        _playback.Stop();
        _playback.Play();
        UpdateTransportButtons();
        return true;
    }

    private bool TryAdvanceDirectoryPlayback()
    {
        if (!_playDirectoryButton.Checked || string.IsNullOrWhiteSpace(_currentVideoPath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(_currentVideoPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        var videos = TryGetVideosInDirectory(directory);
        if (videos is null)
        {
            StopPlaybackAfterEnd("Nie można odczytać katalogu z filmami");
            return true;
        }

        var currentIndex = videos.FindIndex(path => string.Equals(path, _currentVideoPath, StringComparison.OrdinalIgnoreCase));
        if (currentIndex < 0 || currentIndex + 1 >= videos.Count)
        {
            _fileStatus.Text = "Odtworzono ostatni film katalogu";
            return false;
        }

        return LoadVideoFile(videos[currentIndex + 1]);
    }

    private bool TryAdvanceNumberedPartPlayback()
    {
        var nextVideoPath = TryGetNextNumberedPartPath(_currentVideoPath);
        if (nextVideoPath is null)
        {
            return false;
        }

        var nextSubtitlePath = TryGetNextNumberedPartPath(_currentSubtitlePath);
        return LoadVideoFile(nextVideoPath, preferredSubtitlePath: nextSubtitlePath);
    }

    internal static string? GenerateNextNumberedPartPath(string? currentPath) =>
        SubtitleSplitProcessor.GenerateNextNumberedPartPath(currentPath);

    private static string? TryGetNextNumberedPartPath(string? currentPath)
    {
        var nextPath = GenerateNextNumberedPartPath(currentPath);
        return nextPath is not null && File.Exists(nextPath) ? nextPath : null;
    }

    private void PlayAdjacentVideo(int direction)
    {
        if (string.IsNullOrWhiteSpace(_currentVideoPath))
        {
            ShowTemporaryFileStatus("Brak wczytanego filmu");
            return;
        }

        var directory = Path.GetDirectoryName(_currentVideoPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var videos = TryGetVideosInDirectory(directory);
        if (videos is null)
        {
            ShowTemporaryFileStatus("Nie można odczytać katalogu z filmami");
            return;
        }

        var currentIndex = videos.FindIndex(path => string.Equals(path, _currentVideoPath, StringComparison.OrdinalIgnoreCase));
        var targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= videos.Count)
        {
            ShowTemporaryFileStatus(direction < 0
                ? "Brak poprzedniego filmu"
                : "Brak następnego filmu");
            return;
        }

        LoadVideoFile(videos[targetIndex]);
        ShowPlaybackOsd(direction < 0
            ? "Poprzedni plik z aktualnego katalogu"
            : "Następny plik z aktualnego katalogu");
    }

    private static bool IsVideoFile(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".mkv" or ".mp4" or ".avi" or ".mov" or ".wmv" or ".mpeg" or ".mpg" or ".m2ts" or ".ts" or ".mts" or ".vob";

    private List<string>? TryGetVideosInDirectory(string directory)
    {
        try
        {
            var videos = Directory.EnumerateFiles(directory)
                .Where(IsVideoFile)
                .ToList();
            return GetDirectoryVideoSortOrder() switch
            {
                DirectoryVideoSortOrder.Name => videos
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DirectoryVideoSortOrder.Random => GetRandomDirectoryVideoOrder(directory, videos),
                _ => videos
                    .OrderBy(Path.GetExtension, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private List<string> GetRandomDirectoryVideoOrder(string directory, List<string> videos)
    {
        if (string.Equals(_randomDirectoryPath, directory, StringComparison.OrdinalIgnoreCase) &&
            _randomDirectoryVideoOrder is { } cachedOrder &&
            cachedOrder.Count == videos.Count &&
            cachedOrder.All(videos.Contains))
        {
            return [.. cachedOrder];
        }

        var shuffledVideos = new List<string>(videos);
        for (var index = shuffledVideos.Count - 1; index > 0; index--)
        {
            var swapIndex = _directoryShuffleRandom.Next(index + 1);
            (shuffledVideos[index], shuffledVideos[swapIndex]) = (shuffledVideos[swapIndex], shuffledVideos[index]);
        }

        _randomDirectoryPath = directory;
        _randomDirectoryVideoOrder = shuffledVideos;
        return [.. shuffledVideos];
    }

    private void StopPlaybackAfterEnd(string status)
    {
        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        _pendingPausedSeekPosition = null;
        _pendingPausedSeekPrecise = false;
        _showVideoEndPosition = false;
        _playDirectoryButton.Checked = false;
        _playback.Stop();
        _isStopped = true;
        _subtitleEditor.ReadOnly = false;
        ClearSubtitleTraceSelection();
        ResetSeekSliderToStart();
        _settings.Playback.LastPositionMilliseconds = 0;
        _fileStatus.Text = status;
        UpdateTransportButtons();
        if (_settings.Playback.ExitFullscreenWhenPlaybackEnds && _isFullScreen)
        {
            ToggleFullScreen();
        }
    }

    private void RememberPlaybackPosition()
    {
        if (_currentVideoPath is null)
        {
            return;
        }

        _settings.Playback.LastVideoPath = _currentVideoPath;
        _settings.Playback.LastSubtitlePath = _currentSubtitlePath;
        _settings.Playback.LastPositionMilliseconds = Math.Max(
            0,
            _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds);
    }

    private void UpdatePlaybackUi()
    {
        var length = Math.Max(0, Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds));
        _lastKnownMediaLengthMilliseconds = Math.Max(_lastKnownMediaLengthMilliseconds, length);
        _seekSlider.DurationMilliseconds = length;
        var position = _isStopped
            ? Math.Clamp(_stoppedSeekPosition, 0, length)
            : GetSmoothedPosition(Math.Clamp(_playback.PositionMilliseconds, 0, length), length);
        if (length > 0 && !_seekSlider.IsInteracting)
        {
            if (_pendingResumePosition is { } resumePosition)
            {
                var isPrecise = _pendingResumePrecise;
                _pendingResumePosition = null;
                _pendingResumePrecise = false;
                if (IsUnsafeManualSeekNearEnd(resumePosition, length))
                {
                    _settings.Playback.LastPositionMilliseconds = 0;
                }
                else
                {
                    var normalizedResumePosition = NormalizeManualSeekTarget(Math.Clamp(resumePosition, 0, length), length);
                    if (isPrecise)
                    {
                        _playback.SeekPrecise(normalizedResumePosition);
                    }
                    else
                    {
                        _playback.Seek(normalizedResumePosition);
                    }
                    MarkPastUnsortedSubtitleWarnings(_playback.PositionMilliseconds);
                    SynchronizeSmoothedPosition(_playback.PositionMilliseconds);
                    position = _smoothedPositionMilliseconds;
                }
            }
            var presentationPosition = _showVideoEndPosition ? length : position;
            _updatingSeekSlider = true;
            _seekSlider.Value = Math.Clamp(
                (int)Math.Round((presentationPosition / (double)length) * SeekSliderMaximum),
                _seekSlider.Minimum,
                _seekSlider.Maximum);
            _updatingSeekSlider = false;
        }
        RememberPlaybackPosition();
        var statusPosition = _showVideoEndPosition ? length : position;
        UpdateStatusBar(statusPosition, length);
        UpdateTransportButtons();
        if (_fileStatusExpiresAt != default && DateTime.UtcNow >= _fileStatusExpiresAt)
        {
            _fileStatusExpiresAt = default;
            RestoreFileStatus();
        }
        UpdateTimeCounterOsd(statusPosition, length);
        UpdateSubtitleOverlay(position);
        UpdateSubtitleTraceEditorReadOnly();
        UpdateSubtitleTrace(position);
        if (_volumeOsdExpiresAt != default && DateTime.UtcNow >= _volumeOsdExpiresAt)
        {
            _videoOverlay.VolumeText = string.Empty;
            _volumeOsdExpiresAt = default;
            UpdateOverlayVisibility();
        }
        if (_playbackOsdExpiresAt != default && DateTime.UtcNow >= _playbackOsdExpiresAt)
        {
            _videoOverlay.PlaybackText = string.Empty;
            _playbackOsdExpiresAt = default;
            UpdateOverlayVisibility();
        }
        if (_subtitleFontOsdExpiresAt != default && DateTime.UtcNow >= _subtitleFontOsdExpiresAt)
        {
            _videoOverlay.SubtitlePreviewCue = null;
            _subtitleFontOsdExpiresAt = default;
            UpdateOverlayVisibility();
        }
    }

    private void PlayFromEditorLine()
    {
        _seekSlider.CancelInteraction();
        InvalidatePendingRelativeSeek();
        if (!_playback.IsMediaOpen)
        {
            return;
        }

        var frameRate = GetVideoFrameRate();
        for (var lineIndex = _subtitleEditor.CurrentLine; lineIndex >= 0; lineIndex--)
        {
            var line = _subtitleEditor.Lines[lineIndex].Text;
            if (SubtitleDocument.TryGetCueStartMilliseconds(line, frameRate, out var startMilliseconds))
            {
                var knownLength = Math.Max(_playback.DurationMilliseconds, _lastKnownMediaLengthMilliseconds);
                if (IsUnsafeManualSeekNearEnd(startMilliseconds, knownLength))
                {
                    return;
                }

                var targetPosition = knownLength > 0
                    ? NormalizeManualSeekTarget(Math.Clamp(startMilliseconds, 0, knownLength), knownLength)
                    : Math.Max(0, startMilliseconds);
                MarkPastUnsortedSubtitleWarnings(targetPosition);
                SynchronizeSmoothedPosition(targetPosition);
                RememberSubtitleGeometryEditorSeek(targetPosition);
                if (_isStopped)
                {
                    _pendingResumePosition = targetPosition;
                    _pendingResumePrecise = true;
                }
                else if (_playback.State == PlaybackState.Paused)
                {
                    _playback.Play();
                    _playback.SeekPrecise(targetPosition);
                }
                else
                {
                    _playback.SeekPrecise(targetPosition);
                }

                _isStopped = false;
                _playback.Play();
                _subtitleEditor.ReadOnly = _subtitleTraceEnabled;
                if (_subtitleTraceEnabled &&
                    TryGetTraceSelectionForTimingLine(lineIndex, frameRate, out var traceSelection))
                {
                    ApplySubtitleTraceSelection(traceSelection);
                }
                return;
            }

            if (lineIndex != _subtitleEditor.CurrentLine && string.IsNullOrWhiteSpace(line))
            {
                break;
            }
        }

        ShowTemporaryFileStatus("Brak czasu napisu w bieżącej linii");
    }

    private double GetVideoFrameRate()
    {
        if (_sessionFrameRateOverridden)
        {
            return _sessionFrameRate;
        }

        return _playback.MediaFramesPerSecond ?? _sessionFrameRate;
    }

    private double? GetLoadedVideoFrameRate() => _playback.MediaFramesPerSecond;

    private void UpdateMediaDetails()
    {
        var videoPath = _currentVideoPath;
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            return;
        }

        var fileSize = MediaInspector.TryGetFileSize(videoPath);
        _sizeStatus.Text = string.IsNullOrWhiteSpace(fileSize) ? string.Empty : $"[{fileSize}]";
        var mediaInfo = _playback.MediaInfo;
        if (mediaInfo is null)
        {
            return;
        }

        if (mediaInfo.DisplayWidth > 0 && mediaInfo.DisplayHeight > 0)
        {
            _videoOverlay.VideoSourceSize = new Size(mediaInfo.DisplayWidth, mediaInfo.DisplayHeight);
        }
        _resolutionStatusText = mediaInfo.DisplayWidth > 0 && mediaInfo.DisplayHeight > 0
            ? $"{mediaInfo.DisplayWidth}x{mediaInfo.DisplayHeight}"
            : string.Empty;

        if (!mediaInfo.HasVideo)
        {
            return;
        }

        UpdateFrameRateStatus();
        _subtitleEditor.SetFramesPerSecond(GetVideoFrameRate());
        RefreshSubtitleDocument();
        _videoCodecStatusText = mediaInfo.VideoCodecName;

        if (mediaInfo.IsHardwareAccelerated.HasValue)
        {
            _videoDecoderMode = mediaInfo.IsHardwareAccelerated.Value
                ? VideoDecoderMode.Hardware
                : VideoDecoderMode.Software;
            _decoderModeStatusText = mediaInfo.IsHardwareAccelerated.Value ? "H/W" : "S/W";
            _decoderStatus.ToolTipText = mediaInfo.IsHardwareAccelerated.Value
                ? "Dekodowanie sprzętowe (H/W)"
                : "Dekodowanie programowe (S/W)";
        }

        UpdateEditorDecoderStatus();

        _mediaDetailsAvailable = true;
        RefreshFilePathStatus();
    }

    private void ResetSeekSliderToStart()
    {
        _pendingResumePosition = null;
        _pendingResumePrecise = false;
        MarkPastUnsortedSubtitleWarnings(0);
        _updatingSeekSlider = true;
        _seekSlider.Value = _seekSlider.Minimum;
        _updatingSeekSlider = false;
        SynchronizeSmoothedPosition(0);
        UpdateTimeCounterOsd(0, Math.Max(0, _playback.DurationMilliseconds));
    }

    private long GetSmoothedPosition(long enginePosition, long length)
    {
        var now = Environment.TickCount64;
        if (_lastPlaybackClockMilliseconds == 0 || _playback.State != PlaybackState.Playing)
        {
            _smoothedPositionMilliseconds = enginePosition;
            _lastPlaybackClockMilliseconds = now;
            return enginePosition;
        }

        var speedFactor = _playbackSpeedMultiplier / 100.0;
        var elapsed = Math.Max(0, (long)Math.Round((now - _lastPlaybackClockMilliseconds) * speedFactor));
        var estimatedPosition = Math.Clamp(_smoothedPositionMilliseconds + elapsed, 0, length);
        if (enginePosition > estimatedPosition + 250 || enginePosition < estimatedPosition - 1_000)
        {
            estimatedPosition = enginePosition;
        }

        _smoothedPositionMilliseconds = estimatedPosition;
        _lastPlaybackClockMilliseconds = now;
        return estimatedPosition;
    }

    private void SynchronizeSmoothedPosition(long position)
    {
        _smoothedPositionMilliseconds = Math.Max(0, position);
        _lastPlaybackClockMilliseconds = Environment.TickCount64;
    }

    private void UpdateSeekPresentation(long position, long length)
    {
        var boundedLength = Math.Max(0, length);
        var boundedPosition = Math.Clamp(position, 0, boundedLength);
        _seekSlider.DurationMilliseconds = boundedLength;
        SynchronizeSmoothedPosition(boundedPosition);
        if (boundedLength > 0)
        {
            _updatingSeekSlider = true;
            _seekSlider.Value = Math.Clamp(
                (int)Math.Round((boundedPosition / (double)boundedLength) * SeekSliderMaximum),
                _seekSlider.Minimum,
                _seekSlider.Maximum);
            _updatingSeekSlider = false;
        }

        UpdateStatusBar(boundedPosition, boundedLength);
        UpdateTimeCounterOsd(boundedPosition, boundedLength, force: true);
        UpdateSubtitleOverlay(boundedPosition);
    }

    private void UpdateOverlayVisibility()
    {
        var shouldShow = !string.IsNullOrEmpty(_videoOverlay.OsdText) ||
            !string.IsNullOrEmpty(_videoOverlay.VolumeText) ||
            !string.IsNullOrEmpty(_videoOverlay.PlaybackText) ||
            _videoOverlay.SubtitlePreviewCue is not null ||
            _videoOverlay.SubtitleCue is not null;
        if (shouldShow && !_videoOverlay.Visible)
        {
            _videoOverlay.Show(this);
            SyncVideoOverlay();
        }
        else if (!shouldShow && _videoOverlay.Visible)
        {
            _videoOverlay.Hide();
        }
    }

    private void OnPlaybackEmbeddedSubtitleTextChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => OnPlaybackEmbeddedSubtitleTextChanged(sender, e));
            return;
        }

        UpdateSubtitleOverlay(_isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds);
    }

    private void UpdateSubtitleOverlay(long position)
    {
        SubtitleCue? cue = null;
        if (_subtitlesVisible)
        {
            if (_externalSubtitleOverlayEnabled)
            {
                cue = _subtitlePlaybackTracker.GetCueAt(_subtitleDocument, position);
                WarnAboutUnsortedSubtitles(position);
            }
            else if (_playback.ActiveEmbeddedSubtitleTrackId is not null &&
                     _playback is IEmbeddedTextSubtitleSource embeddedSubtitleSource)
            {
                var text = embeddedSubtitleSource.CurrentEmbeddedSubtitleText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var renderOptions = new SubtitleRenderOptions(
                        _settings.SubtitlePlayback.InterpretMicroDvdControlCodes,
                        _settings.SubtitlePlayback.ItalicLinesStartingWithSlash,
                        _settings.SubtitlePlayback.ItalicLinesStartingWithAsterisk,
                        _settings.SubtitlePlayback.ItalicLinesStartingWithHash);
                    var parsed = SubtitleMarkup.ParseCue(text, default, renderOptions);
                    cue = new SubtitleCue(position, position, parsed.Lines, parsed.Position);
                }
            }
        }

        _videoOverlay.SubtitleCue = cue;
        UpdateOverlayVisibility();
    }

    private void WarnAboutUnsortedSubtitles(long position)
    {
        if (_playback.State != PlaybackState.Playing ||
            !_settings.SubtitlePlayback.WarnOnUnsortedSubtitles ||
            !_subtitlesVisible ||
            !_externalSubtitleOverlayEnabled)
        {
            return;
        }

        var cue = _unsortedSubtitleWarningTracker.GetNextWarning(_subtitleDocument, position);
        if (cue is null)
        {
            return;
        }
        var wasPlaying = _playback.State == PlaybackState.Playing;
        if (wasPlaying)
        {
            _playback.Pause();
        }

        using var dialog = new UnsortedSubtitlesWarningDialog(cue.SourceLineNumber);
        dialog.ShowDialog(this);

        if (wasPlaying)
        {
            _playback.Play();
        }
    }

    private void UpdateTransportButtons()
    {
        _playButton.Checked = _playback.State == PlaybackState.Playing;
        _pauseButton.Checked = _playback.IsMediaOpen &&
            _playback.State != PlaybackState.Playing &&
            !_isStopped;
    }

    private static string FitPathToWidth(string fullPath, Font font, int availableWidth)
    {
        if (TextRenderer.MeasureText(fullPath, font).Width <= availableWidth)
        {
            return fullPath;
        }

        var displayPath = $"...{Path.DirectorySeparatorChar}{Path.GetFileName(fullPath)}";
        while (displayPath.Length > 4 && TextRenderer.MeasureText(displayPath, font).Width > availableWidth)
        {
            displayPath = $"...{displayPath[4..]}";
        }

        return displayPath;
    }

    private void SyncVideoOverlay()
    {
        if (IsDisposed || !_videoOverlay.Visible)
        {
            return;
        }

        _videoOverlay.Bounds = _videoPanel.RectangleToScreen(_videoPanel.ClientRectangle);
        _videoOverlay.BringToFrontWithoutActivation();
    }

    private void BringApplicationToFront()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        Activate();
        BringToFront();
        SetForegroundWindow(Handle);
    }

    private static string FormatTime(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return $"{(int)time.TotalHours}:{time:mm\\:ss}";
    }

    private static string FitTextToWidth(string text, Font font, int width)
    {
        if (string.IsNullOrEmpty(text) ||
            TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding).Width <= width)
        {
            return text;
        }

        const string ellipsis = "…";
        var length = text.Length;
        while (length > 0 &&
               TextRenderer.MeasureText(text[..length] + ellipsis, font, Size.Empty, TextFormatFlags.NoPadding).Width > width)
        {
            length--;
        }

        return length == 0 ? ellipsis : text[..length] + ellipsis;
    }

    private void ToggleEditor()
    {
        if (_isFullScreen)
        {
            return;
        }

        if (_subtitleTraceEnabled && !_splitContainer.Panel2Collapsed)
        {
            SetSubtitleTraceEnabled(false);
        }

        var wasHidden = _splitContainer.Panel2Collapsed;
        _splitContainer.Panel2Collapsed = !wasHidden;
        ToolbarButton("toolbarEditor").Checked = !_splitContainer.Panel2Collapsed;
        UpdateSubtitleConversionAvailability();
        _fileStatus.Text = _splitContainer.Panel2Collapsed ? "Edytor napis\u00f3w wy\u0142\u0105czony" : "Napisy.txt";
        if (_splitContainer.Panel2Collapsed)
        {
            _videoHost.Focus();
        }
        else if (wasHidden)
        {
            if (_settings.Editor.MoveCaretToCurrentSubtitleOnEditorShow)
            {
                MoveCaretToCurrentSubtitle();
            }
            else
            {
                _subtitleEditor.Focus();
            }
        }
    }

    private void MoveCaretToCurrentSubtitle()
    {
        var position = _isStopped ? _stoppedSeekPosition : _playback.PositionMilliseconds;
        position = Math.Max(0, position);
        if (!TryGetSubtitleTraceSelection(position, out var selection) &&
            !TryGetFirstTraceSelectionAtOrAfter(position, out selection))
        {
            _subtitleEditor.Focus();
            return;
        }

        _subtitleEditor.SetCaretPosition(selection.TextStart);
        ScrollTraceLineToFourthRow(selection.LineIndex);
        _subtitleEditor.Focus();
    }

    private void ToggleSubtitleVisibility()
    {
        _subtitlesVisible = !_subtitlesVisible;
        ToolbarButton("toolbarShowSubtitles").Checked = _subtitlesVisible;
        if (_subtitleVisibilityMenuItem is not null)
        {
            _subtitleVisibilityMenuItem.Checked = _subtitlesVisible;
        }

        UpdateSubtitleOverlay(_playback.PositionMilliseconds);
        ShowPlaybackOsd(_subtitlesVisible ? "Napisy włączone" : "Napisy wyłączone");
    }

    private void ToggleSubtitleTransparency()
    {
        _transparentSubtitles = !_transparentSubtitles;
        _settings.TransparentSubtitles = _transparentSubtitles;
        ToolbarButton("toolbarTransparentSubtitles").Checked = _transparentSubtitles;
        if (_transparentSubtitlesMenuItem is not null)
        {
            _transparentSubtitlesMenuItem.Checked = _transparentSubtitles;
        }

        _videoOverlay.TransparentSubtitles = _transparentSubtitles;
        ShowPlaybackOsd(_transparentSubtitles
            ? "Przezroczyste napisy włączone"
            : "Przezroczyste napisy wyłączone");
    }

    private void ToggleAlwaysOnTop()
    {
        TopMost = !TopMost;
        ToolbarButton("toolbarAlwaysOnTop").Checked = TopMost;
        ShowPlaybackOsd(TopMost ? "Okno zawsze na wierzchu" : "Okno nie zawsze na wierzchu");
    }

    private void ToggleMinimizeWindow()
    {
        WindowState = WindowState == FormWindowState.Minimized
            ? FormWindowState.Normal
            : FormWindowState.Minimized;
    }

    private bool IsVideoSurfacePoint(Point point)
    {
        if (!_videoPanel.RectangleToScreen(_videoPanel.ClientRectangle).Contains(point))
        {
            return false;
        }

        return !_isFullScreen ||
            !_fullScreenControlsPanel.Visible ||
            !_fullScreenControlsPanel.RectangleToScreen(_fullScreenControlsPanel.ClientRectangle).Contains(point);
    }

    private void RestoreWindowActivation()
    {
        SyncVideoOverlay();
        BringApplicationToFront();
        if (_editorWasVisible)
        {
            if (_settings.Editor.MoveCaretToCurrentSubtitleOnFullScreenExit)
            {
                MoveCaretToCurrentSubtitle();
                return;
            }

            _subtitleEditor.SetCaretPosition(
                Math.Clamp(_editorCaretPositionBeforeFullScreen, 0, _subtitleEditor.TextLength));
            _subtitleEditor.Focus();
            return;
        }

        _videoHost.Focus();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmSubtitleChanges())
        {
            e.Cancel = true;
            return;
        }

        SaveWindowSettings();
        _seekSlider.CancelInteraction();
        _playbackTimer.Stop();
        _seekRepeatTimer.Stop();
        _autoSaveTimer.Stop();
        _fullScreenControlsTimer.Stop();
        ShowFullScreenMouseCursor();
        Deactivate -= OnApplicationDeactivated;
        Activated -= OnApplicationActivated;
        Application.EnterThreadModal -= OnApplicationEnterThreadModal;
        Application.LeaveThreadModal -= OnApplicationLeaveThreadModal;
        Application.RemoveMessageFilter(_videoInputFilter);
        _fullScreenWheelHook.Dispose();
        _videoOverlay.Close();
        _videoHost.Dispose();
        if (_playback is IEmbeddedTextSubtitleSource embeddedSubtitleSource)
        {
            embeddedSubtitleSource.EmbeddedSubtitleTextChanged -= OnPlaybackEmbeddedSubtitleTextChanged;
        }
        _playback.Dispose();
        _playbackTimer.Dispose();
        _seekRepeatTimer.Dispose();
        _autoSaveTimer.Dispose();
        _fullScreenControlsTimer.Dispose();
    }

    private void SetInitialSplitterDistance()
    {
        var maximum = _splitContainer.Height - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
        if (maximum < _splitContainer.Panel1MinSize)
        {
            return;
        }

        _splitContainer.SplitterDistance = Math.Clamp(
            _settings.Window.SplitterDistance,
            _splitContainer.Panel1MinSize,
            maximum);
    }

    private void ApplyWindowSettings()
    {
        var window = _settings.Window;
        StartPosition = FormStartPosition.Manual;
        Bounds = GetVisibleWindowBounds(window);
        if (window.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    private Rectangle GetVisibleWindowBounds(WindowSettings window)
    {
        var requestedBounds = new Rectangle(
            window.Left,
            window.Top,
            Math.Max(MinimumSize.Width, window.Width),
            Math.Max(MinimumSize.Height, window.Height));
        if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(requestedBounds)))
        {
            return requestedBounds;
        }

        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        return new Rectangle(
            ClampWindowPosition(requestedBounds.Left, requestedBounds.Width, workingArea.Left, workingArea.Right),
            ClampWindowPosition(requestedBounds.Top, requestedBounds.Height, workingArea.Top, workingArea.Bottom),
            requestedBounds.Width,
            requestedBounds.Height);
    }

    private static int ClampWindowPosition(int position, int size, int start, int end)
    {
        var availableSize = end - start;
        return size >= availableSize
            ? start
            : Math.Clamp(position, start, end - size);
    }

    private void SaveWindowSettings()
    {
        RememberPlaybackPosition();
        var bounds = _isFullScreen
            ? _restoreBounds
            : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.Window.Left = bounds.Left;
        _settings.Window.Top = bounds.Top;
        _settings.Window.Width = bounds.Width;
        _settings.Window.Height = bounds.Height;
        _settings.Window.SplitterDistance = _splitContainer.SplitterDistance;
        _settings.Window.Maximized = !_isFullScreen && WindowState == FormWindowState.Maximized;
        _settings.Volume = _volumeSlider.Value;
        SaveSettingsOrShowWarning();
    }

}
