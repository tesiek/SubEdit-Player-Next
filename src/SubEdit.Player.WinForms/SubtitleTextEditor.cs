using ScintillaNET;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms.Controls;

/// <summary>
/// Subtitle editor with line numbers based on document lines, not visual wraps.
/// </summary>
public sealed class SubtitleTextEditor : Scintilla
{
    private const string EditorFont = "Courier New";
    private const string LineNumberFont = "Consolas";
    private const int BoldStyle = 40;
    private const int ItalicStyle = 41;
    private const int UnderlineStyle = 42;
    private const int BoldItalicStyle = 43;
    private const int BoldUnderlineStyle = 44;
    private const int ItalicUnderlineStyle = 45;
    private const int BoldItalicUnderlineStyle = 46;
    private const int MarkupStyle = 47;
    private const int SemanticStyleStart = 48;
    internal const int GrayShadeCount = 16;
    internal const int InvalidFormatColorIndex = GrayShadeCount;
    internal const int VeryShortColorIndex = GrayShadeCount + 1;
    private const int UnsortedLineMarker = 0;
    private const int SelectionBackgroundMarkerStart = 1;
    private const int CurrentLineMarker = SelectionBackgroundMarkerStart + GrayShadeCount + 2;
    private const int SelectionTextIndicator = 0;
    private const int SelectionBackgroundIndicatorStart = 1;
    private const int SciSelectionIsRectangle = 2372;
    private const int SciSetUndoSelectionHistory = 2782;
    private const int ScUndoSelectionHistoryEnabled = 1;
    private const int ScUndoSelectionHistoryScroll = 2;

    private static readonly Keys[] BlockedControlShortcutKeys =
    [
        .. Enumerable.Range((int)Keys.A, (int)Keys.Z - (int)Keys.A + 1).Select(value => (Keys)value),
        .. Enumerable.Range((int)Keys.D0, (int)Keys.D9 - (int)Keys.D0 + 1).Select(value => (Keys)value),
        .. Enumerable.Range((int)Keys.NumPad0, (int)Keys.NumPad9 - (int)Keys.NumPad0 + 1).Select(value => (Keys)value),
        Keys.Tab, Keys.Enter, Keys.Space,
        Keys.Oem1, Keys.Oemplus, Keys.Oemcomma, Keys.OemMinus, Keys.OemPeriod,
        Keys.Oem2, Keys.Oem3, Keys.Oem4, Keys.Oem5, Keys.Oem6, Keys.Oem7, Keys.Oem8, Keys.Oem102
    ];

    private static readonly Color[] SemanticColors =
    [
        .. Enumerable.Range(0, GrayShadeCount)
            .Select(index => Color.FromArgb(index * 200 / (GrayShadeCount - 1), index * 200 / (GrayShadeCount - 1), index * 200 / (GrayShadeCount - 1))),
        Color.Red,
        Color.FromArgb(0, 200, 0)
    ];

    private double _framesPerSecond = 25d;
    private string _editorFontName = EditorFont;
    private int _editorFontSize = 10;
    private bool _highlightInvalidFormat = true;
    private bool _highlightUnsortedLines = true;
    private bool _highlightShortLines = true;
    private bool _highlightVeryShortLines = true;
    private bool _updatingSelectionHighlight;
    private bool _rectangularSelectionRendering;
    private bool _highlightMarkupPending;
    private readonly Color _defaultSelectionInactiveTextColor;
    private readonly Color _defaultSelectionInactiveBackColor;

    public event EventHandler? LineNumberMarginWidthChanged;
    public event EventHandler? NativeUndoRedoPerformed;
    public event EventHandler? UndoHistoryCleared;

    // Scintilla's EmptyUndoBuffer explicitly does not raise SavePointReached;
    // expose its completion so toolbar availability updates even without a text change.
    public new void EmptyUndoBuffer()
    {
        base.EmptyUndoBuffer();
        UndoHistoryCleared?.Invoke(this, EventArgs.Empty);
    }

    public int LineNumberMarginWidth => Margins[0].Width;

    public SubtitleTextEditor()
    {
        _defaultSelectionInactiveTextColor = SelectionInactiveTextColor;
        _defaultSelectionInactiveBackColor = SelectionInactiveBackColor;
        Dock = DockStyle.Fill;
        BorderStyle = ScintillaNET.BorderStyle.None;
        WrapMode = WrapMode.Word;
        UseTabs = false;
        TabWidth = 4;
        IndentWidth = 4;
        IndentationGuides = IndentView.None;
        ViewEol = false;
        EdgeMode = EdgeMode.Line;
        EdgeColumn = 80;
        EdgeColor = Color.FromArgb(192, 192, 192);

        StyleResetDefault();
        Styles[Style.Default].Font = EditorFont;
        Styles[Style.Default].Size = 10;
        Styles[Style.Default].BackColor = Color.White;
        Styles[Style.Default].ForeColor = Color.Black;
        Styles[Style.Default].Bold = false;
        Styles[Style.Default].Italic = false;
        Styles[Style.Default].Underline = false;
        StyleClearAll();
        ConfigureMarkupStyles();
        ConfigureSemanticStyles();

        Markers[UnsortedLineMarker].Symbol = MarkerSymbol.Background;
        Markers[UnsortedLineMarker].SetBackColor(Color.Red);
        Markers[CurrentLineMarker].Symbol = MarkerSymbol.Arrow;
        Markers[CurrentLineMarker].SetForeColor(SystemColors.ControlDarkDark);
        Markers[CurrentLineMarker].SetBackColor(SystemColors.ControlDarkDark);

        ConfigureSelectionHighlights();

        // Keep each style's foreground (black/gray/green/red) while selected.
#pragma warning disable CS0618 // This API is the Scintilla SCI_SETSELFORE(useSetting, color) message.
        SetSelectionForeColor(use: false, Color.Empty);
#pragma warning restore CS0618
        SelectionBackColor = Color.Transparent;
        SelectionInactiveTextColor = _defaultSelectionInactiveTextColor;
        SelectionInactiveBackColor = _defaultSelectionInactiveBackColor;
        SelectionLayer = Layer.UnderText;
        SelectionEolFilled = true;
        PhasesDraw = Phases.Multiple;
        MouseSelectionRectangularSwitch = true;
        VirtualSpaceOptions = VirtualSpace.RectangularSelection;

        Margins[0].Type = MarginType.Number;
        Margins[0].Width = 26;
        Margins[1].Type = MarginType.Symbol;
        Margins[1].Width = 10;
        Margins[1].Mask = 1 << CurrentLineMarker;
        Styles[Style.LineNumber].BackColor = SystemColors.Control;
        Styles[Style.LineNumber].ForeColor = SystemColors.ControlDarkDark;
        Styles[Style.LineNumber].Font = LineNumberFont;
        Styles[Style.LineNumber].Size = 10;
        Styles[Style.LineNumber].Bold = false;
        Styles[Style.LineNumber].Italic = false;
        Styles[Style.LineNumber].Underline = false;

        HandleCreated += (_, _) =>
        {
            ConfigureUndoSelectionHistory();
            DisableScintillaControlShortcuts();
            UpdateLineNumberMargin();
            HighlightMarkup();
        };

        if (IsHandleCreated)
        {
            ConfigureUndoSelectionHistory();
        }
        TextChanged += (_, _) =>
        {
            UpdateLineNumberMargin();
            ScheduleHighlightMarkup();
        };
        Insert += (_, e) =>
        {
            if (e.Source.HasFlag(ModificationFlags.Undo) || e.Source.HasFlag(ModificationFlags.Redo))
            {
                NativeUndoRedoPerformed?.Invoke(this, EventArgs.Empty);
            }
        };
        Delete += (_, e) =>
        {
            if (e.Source.HasFlag(ModificationFlags.Undo) || e.Source.HasFlag(ModificationFlags.Redo))
            {
                NativeUndoRedoPerformed?.Invoke(this, EventArgs.Empty);
            }
        };
        UpdateUI += (_, e) =>
        {
            if (e.Change.HasFlag(UpdateChange.Selection))
            {
                UpdateCurrentLineMarker();
                UpdateSelectionHighlight();
            }
        };
    }

    public void RefreshLineColors()
    {
        HighlightMarkup();
    }

    public void ApplyEditorSettings(EditorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _editorFontName = settings.FontName;
        _editorFontSize = Math.Clamp(settings.FontSize, 6, 22);
        _highlightInvalidFormat = settings.HighlightInvalidFormat;
        _highlightUnsortedLines = settings.HighlightUnsortedLines;
        _highlightShortLines = settings.HighlightShortLines;
        _highlightVeryShortLines = settings.HighlightVeryShortLines;

        Styles[Style.Default].Font = _editorFontName;
        Styles[Style.Default].Size = _editorFontSize;
        Styles[Style.Default].Bold = false;
        Styles[Style.Default].Italic = false;
        Styles[Style.Default].Underline = false;
        Styles[Style.LineNumber].Font = _editorFontName;
        Styles[Style.LineNumber].Size = _editorFontSize;
        Styles[Style.LineNumber].Bold = false;
        Styles[Style.LineNumber].Italic = false;
        Styles[Style.LineNumber].Underline = false;
        ConfigureMarkupStyles();
        ConfigureSemanticStyles();
        UpdateLineNumberMargin();
        HighlightMarkup();
    }

    internal void SetFramesPerSecond(double framesPerSecond)
    {
        _framesPerSecond = Math.Max(1d, framesPerSecond);
        HighlightMarkup();
    }

    internal void SetTraceSelection(int textStart, int textEnd)
    {
        _rectangularSelectionRendering = false;
#pragma warning disable CS0618 // This API maps to Scintilla's global selection foreground override.
        SetSelectionForeColor(use: true, Color.White);
#pragma warning restore CS0618
        SelectionInactiveTextColor = Color.White;
        SelectionInactiveBackColor = Color.Transparent;
        SelectionBackColor = Color.Transparent;
        SelectionLayer = Layer.UnderText;

        // Scintilla expects caret first and anchor second. Keeping the caret at
        // textStart places it directly between the timing prefix and subtitle text.
        SetSelection(textStart, textEnd);
        UpdateCurrentLineMarker();
        UpdateSelectionHighlight();
    }

    internal void SetCaretPosition(int position)
    {
        SetEmptySelection(position);
        UpdateCurrentLineMarker();
        UpdateSelectionHighlight();
    }

    internal SubtitleEditorViewState CaptureViewState()
    {
        var currentLine = Lines.Count > 0 ? Math.Clamp(CurrentLine, 0, Lines.Count - 1) : 0;
        var linePosition = Lines.Count > 0 ? Lines[currentLine].Position : 0;
        var currentColumn = Lines.Count > 0
            ? Math.Clamp(CurrentPosition - linePosition, 0, Lines[currentLine].Text.Length)
            : 0;
        var firstVisibleLine = FirstVisibleLine;
        return new SubtitleEditorViewState(currentLine, currentColumn, firstVisibleLine);
    }

    internal void RestoreViewState(SubtitleEditorViewState state)
    {
        if (Lines.Count == 0)
        {
            return;
        }

        var restoredLineIndex = Math.Clamp(state.Line, 0, Lines.Count - 1);
        var restoredLine = Lines[restoredLineIndex];
        var lineContentLength = restoredLine.Text.TrimEnd('\r', '\n').Length;
        var restoredPosition = restoredLine.Position + Math.Clamp(state.Column, 0, lineContentLength);
        SetCaretPosition(restoredPosition);
        FirstVisibleLine = state.FirstVisibleLine;
    }

    internal void ExecuteWritableMutation(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var wasReadOnly = ReadOnly;
        try
        {
            if (wasReadOnly)
            {
                ReadOnly = false;
            }

            action();
        }
        finally
        {
            if (wasReadOnly)
            {
                ReadOnly = true;
            }
        }
    }

    internal void ApplyFullDocumentEdit(string newText, SubtitleEditorViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(newText);

        ExecuteWritableMutation(() =>
        {
            BeginUndoAction();
            try
            {
                TargetStart = 0;
                TargetEnd = TextLength;
                ReplaceTarget(newText);
                RestoreViewState(viewState);
            }
            finally
            {
                EndUndoAction();
            }
        });
    }

    internal void ClearTraceSelection()
    {
        _rectangularSelectionRendering = false;
#pragma warning disable CS0618 // Restore the editor's semantic selection rendering.
        SetSelectionForeColor(use: false, Color.Empty);
#pragma warning restore CS0618
        SelectionBackColor = Color.Transparent;
        SelectionInactiveTextColor = _defaultSelectionInactiveTextColor;
        SelectionInactiveBackColor = _defaultSelectionInactiveBackColor;
        SelectionLayer = Layer.UnderText;
        SetEmptySelection(CurrentPosition);
        UpdateCurrentLineMarker();
        UpdateSelectionHighlight();
    }

    private void ConfigureUndoSelectionHistory()
    {
        DirectMessage(SciSetUndoSelectionHistory, (IntPtr)(ScUndoSelectionHistoryEnabled | ScUndoSelectionHistoryScroll));
    }

    private void DisableScintillaControlShortcuts()
    {
        foreach (var key in BlockedControlShortcutKeys)
        {
            ClearCmdKey(Keys.Control | key);
            ClearCmdKey(Keys.Control | Keys.Shift | key);
            ClearCmdKey(Keys.Control | Keys.Alt | key);
            ClearCmdKey(Keys.Control | Keys.Alt | Keys.Shift | key);
        }
    }

    private void ConfigureMarkupStyles()
    {
        // The editor always shows literal subtitle text in the Regular face.
        // Markup interpretation is reserved for subtitle rendering during playback.
        ConfigureRegularTextStyle(BoldStyle);
        ConfigureRegularTextStyle(ItalicStyle);
        ConfigureRegularTextStyle(UnderlineStyle);
        ConfigureRegularTextStyle(BoldItalicStyle);
        ConfigureRegularTextStyle(BoldUnderlineStyle);
        ConfigureRegularTextStyle(ItalicUnderlineStyle);
        ConfigureRegularTextStyle(BoldItalicUnderlineStyle);

        Styles[MarkupStyle].Font = _editorFontName;
        Styles[MarkupStyle].Size = _editorFontSize;
        Styles[MarkupStyle].ForeColor = Color.SeaGreen;
        Styles[MarkupStyle].BackColor = Color.White;
        Styles[MarkupStyle].Bold = false;
        Styles[MarkupStyle].Italic = false;
        Styles[MarkupStyle].Underline = false;
    }

    private void ConfigureSemanticStyles()
    {
        foreach (var colorIndex in Enumerable.Range(0, SemanticColors.Length))
        {
            ConfigureSemanticStyle((int)Style.Default, colorIndex);
            ConfigureSemanticStyle(MarkupStyle, colorIndex);
            ConfigureSemanticStyle(BoldStyle, colorIndex);
            ConfigureSemanticStyle(ItalicStyle, colorIndex);
            ConfigureSemanticStyle(UnderlineStyle, colorIndex);
            ConfigureSemanticStyle(BoldItalicStyle, colorIndex);
            ConfigureSemanticStyle(BoldUnderlineStyle, colorIndex);
            ConfigureSemanticStyle(ItalicUnderlineStyle, colorIndex);
            ConfigureSemanticStyle(BoldItalicUnderlineStyle, colorIndex);
        }
    }

    private void ConfigureSelectionHighlights()
    {
        Indicators[SelectionTextIndicator].Style = IndicatorStyle.TextFore;
        Indicators[SelectionTextIndicator].ForeColor = Color.White;

        for (var colorIndex = 0; colorIndex < SemanticColors.Length; colorIndex++)
        {
            var backgroundIndicator = Indicators[SelectionBackgroundIndicatorStart + colorIndex];
            backgroundIndicator.Style = IndicatorStyle.FullBox;
            backgroundIndicator.ForeColor = SemanticColors[colorIndex];
            backgroundIndicator.Alpha = 255;
            backgroundIndicator.OutlineAlpha = 255;
            backgroundIndicator.Under = true;

            var backgroundMarker = Markers[SelectionBackgroundMarkerStart + colorIndex];
            backgroundMarker.Symbol = MarkerSymbol.Background;
            backgroundMarker.SetBackColor(SemanticColors[colorIndex]);
        }
    }

    private void ConfigureSemanticStyle(int baseStyle, int colorIndex)
    {
        var styleIndex = GetSemanticStyleIndex(baseStyle, colorIndex);
        Styles[styleIndex].Font = _editorFontName;
        Styles[styleIndex].Size = _editorFontSize;
        Styles[styleIndex].ForeColor = SemanticColors[colorIndex];
        Styles[styleIndex].BackColor = Color.White;
        Styles[styleIndex].Bold = false;
        Styles[styleIndex].Italic = false;
        Styles[styleIndex].Underline = false;
    }

    private void ConfigureRegularTextStyle(int styleIndex)
    {
        Styles[styleIndex].Font = _editorFontName;
        Styles[styleIndex].Size = _editorFontSize;
        Styles[styleIndex].ForeColor = Color.Black;
        Styles[styleIndex].BackColor = Color.White;
        Styles[styleIndex].Bold = false;
        Styles[styleIndex].Italic = false;
        Styles[styleIndex].Underline = false;
    }

    private void ScheduleHighlightMarkup()
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return;
        }

        if (_highlightMarkupPending)
        {
            return;
        }

        _highlightMarkupPending = true;

        BeginInvoke(new Action(() =>
        {
            _highlightMarkupPending = false;
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            HighlightMarkup();
        }));
    }

    private void HighlightMarkup()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var text = Text;
        var unsortedLineIndices = GetUnsortedLineIndices().ToHashSet();
        var lineColors = GetLineColors();
        Markers[UnsortedLineMarker].DeleteAll();
        foreach (var lineIndex in unsortedLineIndices)
        {
            Lines[lineIndex].MarkerAdd(UnsortedLineMarker);
        }

        StartStyling(0);
        var textPosition = 0;
        foreach (var span in SubtitleMarkup.GetEditorSpans(text))
        {
            ApplyStylingRange(textPosition, span.Start - textPosition, (int)Style.Default, lineColors);
            ApplyStylingRange(span.Start, span.Length, span.IsMarkup ? MarkupStyle : GetStyleIndex(span.Style), lineColors);
            textPosition = span.Start + span.Length;
        }

        ApplyStylingRange(textPosition, text.Length - textPosition, (int)Style.Default, lineColors);
        UpdateCurrentLineMarker();
        UpdateSelectionHighlight(lineColors, unsortedLineIndices);
    }

    private void UpdateCurrentLineMarker()
    {
        if (!IsHandleCreated || Lines.Count == 0)
        {
            return;
        }

        Markers[CurrentLineMarker].DeleteAll();
        Lines[Math.Clamp(CurrentLine, 0, Lines.Count - 1)].MarkerAdd(CurrentLineMarker);
    }

    private void UpdateSelectionHighlight()
    {
        if (!IsHandleCreated || _updatingSelectionHighlight)
        {
            return;
        }

        var selectionStart = Math.Min(SelectionStart, SelectionEnd);
        var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
        if (selectionStart >= selectionEnd || IsRectangularSelection())
        {
            _updatingSelectionHighlight = true;
            try
            {
                ClearSelectionHighlights();
                if (IsRectangularSelection())
                {
                    EnableRectangularSelectionRendering();
                }
                else
                {
                    DisableRectangularSelectionRendering();
                }
            }
            finally
            {
                _updatingSelectionHighlight = false;
            }

            return;
        }

        UpdateSelectionHighlight(GetLineColors(), GetUnsortedLineIndices().ToHashSet());
    }

    private void UpdateSelectionHighlight(IReadOnlyList<int> lineColors, IReadOnlySet<int> unsortedLineIndices)
    {
        if (!IsHandleCreated || _updatingSelectionHighlight)
        {
            return;
        }

        _updatingSelectionHighlight = true;
        try
        {
            ClearSelectionHighlights();

            // Scintilla exposes the rectangle limits through SelectionStart
            // and SelectionEnd while rendering it as multiple ranges. The
            // semantic overlay would paint the text between those limits, so
            // leave the column rendering to Scintilla.
            if (IsRectangularSelection())
            {
                EnableRectangularSelectionRendering();
                return;
            }

            DisableRectangularSelectionRendering();

            var selectionStart = Math.Min(SelectionStart, SelectionEnd);
            var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
            if (selectionStart >= selectionEnd)
            {
                return;
            }

            var firstLine = LineFromPosition(selectionStart);
            var lastLine = LineFromPosition(selectionEnd - 1);
            for (var lineIndex = firstLine; lineIndex <= lastLine; lineIndex++)
            {
                // The original red background for an unsorted line stays authoritative.
                if (unsortedLineIndices.Contains(lineIndex))
                {
                    continue;
                }

                var lineStart = Lines[lineIndex].Position;
                var nextLineStart = lineIndex + 1 < Lines.Count ? Lines[lineIndex + 1].Position : TextLength;
                var segmentStart = Math.Max(selectionStart, lineStart);
                var segmentEnd = Math.Min(selectionEnd, nextLineStart);
                if (segmentStart >= segmentEnd)
                {
                    continue;
                }

                var colorIndex = lineIndex < lineColors.Count ? lineColors[lineIndex] : 0;
                FillIndicator(SelectionBackgroundIndicatorStart + colorIndex, segmentStart, segmentEnd - segmentStart);
                FillIndicator(SelectionTextIndicator, segmentStart, segmentEnd - segmentStart);

                var wholeLineSelected = segmentStart == lineStart &&
                    (lineIndex + 1 < Lines.Count ? selectionEnd >= nextLineStart : selectionEnd >= TextLength);
                if (wholeLineSelected)
                {
                    Lines[lineIndex].MarkerAdd(SelectionBackgroundMarkerStart + colorIndex);
                }
            }
        }
        finally
        {
            _updatingSelectionHighlight = false;
        }
    }

    private void ClearSelectionHighlights()
    {
        for (var colorIndex = 0; colorIndex < SemanticColors.Length; colorIndex++)
        {
            Markers[SelectionBackgroundMarkerStart + colorIndex].DeleteAll();
            IndicatorCurrent = SelectionBackgroundIndicatorStart + colorIndex;
            IndicatorClearRange(0, TextLength);
        }

        IndicatorCurrent = SelectionTextIndicator;
        IndicatorClearRange(0, TextLength);
    }

    private bool IsRectangularSelection() =>
        DirectMessage(SciSelectionIsRectangle) != IntPtr.Zero;

    private void EnableRectangularSelectionRendering()
    {
        if (_rectangularSelectionRendering)
        {
            return;
        }

        // A rectangular selection is split into one main selection and
        // additional selections. The main selection is normally transparent
        // because semantic selection colours are drawn by indicators, so use
        // the native additional-selection colours for the whole rectangle.
        SelectionTextColor = SelectionAdditionalTextColor;
        SelectionBackColor = SelectionAdditionalBackColor;
        _rectangularSelectionRendering = true;
    }

    private void DisableRectangularSelectionRendering()
    {
        if (!_rectangularSelectionRendering)
        {
            return;
        }

#pragma warning disable CS0618 // Restore semantic selection foreground colours.
        SetSelectionForeColor(use: false, Color.Empty);
#pragma warning restore CS0618
        SelectionBackColor = Color.Transparent;
        _rectangularSelectionRendering = false;
    }

    private void FillIndicator(int indicatorIndex, int start, int length)
    {
        if (length <= 0)
        {
            return;
        }

        IndicatorCurrent = indicatorIndex;
        IndicatorFillRange(start, length);
    }

    private void ApplyStylingRange(int start, int length, int baseStyle, IReadOnlyList<int> lineColors)
    {
        while (length > 0)
        {
            var lineIndex = Math.Clamp(LineFromPosition(start), 0, Math.Max(0, Lines.Count - 1));
            var nextLinePosition = lineIndex + 1 < Lines.Count ? Lines[lineIndex + 1].Position : TextLength;
            var segmentLength = Math.Min(length, Math.Max(1, nextLinePosition - start));
            var colorIndex = lineIndex < lineColors.Count ? lineColors[lineIndex] : 0;
            var semanticBaseStyle = colorIndex == InvalidFormatColorIndex ? (int)Style.Default : baseStyle;
            SetStyling(segmentLength, GetSemanticStyleIndex(semanticBaseStyle, colorIndex));
            start += segmentLength;
            length -= segmentLength;
        }
    }

    private IReadOnlyList<int> GetLineColors()
    {
        var lineCount = Lines.Count;
        var lineTexts = new string[lineCount];
        for (var i = 0; i < lineCount; i++)
        {
            lineTexts[i] = Lines[i].Text;
        }

        return ComputeLineColors(
            lineTexts,
            _framesPerSecond,
            _highlightInvalidFormat,
            _highlightVeryShortLines,
            _highlightShortLines);
    }

    internal static IReadOnlyList<int> ComputeLineColors(
        IReadOnlyList<string> lines,
        double framesPerSecond,
        bool highlightInvalidFormat,
        bool highlightVeryShortLines,
        bool highlightShortLines)
    {
        var lineCount = lines.Count;
        var colors = new int[lineCount];
        if (lineCount == 0)
        {
            return colors;
        }

        var isSrtDocument = false;
        for (var i = 0; i < lineCount; i++)
        {
            if (lines[i].Contains("-->", StringComparison.Ordinal))
            {
                isSrtDocument = true;
                break;
            }
        }

        var nextStarts = new long?[lineCount];
        long? nextCueStart = null;
        for (var i = lineCount - 1; i >= 0; i--)
        {
            nextStarts[i] = nextCueStart;
            if (SubtitleDocument.TryGetCueStartMilliseconds(lines[i], framesPerSecond, out var start))
            {
                nextCueStart = start;
            }
        }

        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            var line = lines[lineIndex];

            if (string.IsNullOrWhiteSpace(line))
            {
                colors[lineIndex] = isSrtDocument || !highlightInvalidFormat ? 0 : InvalidFormatColorIndex;
                continue;
            }

            if (isSrtDocument && !line.Contains("-->", StringComparison.Ordinal))
            {
                colors[lineIndex] = 0;
                continue;
            }

            var characterCount = isSrtDocument ? GetSrtCueTextLength(lines, lineIndex) : GetCueText(line, framesPerSecond).Length;
            var nextStart = nextStarts[lineIndex];

            colors[lineIndex] = DetermineLineColor(
                line,
                isSrtDocument,
                framesPerSecond,
                characterCount,
                nextStart,
                highlightInvalidFormat,
                highlightVeryShortLines,
                highlightShortLines);
        }

        return colors;
    }

    internal static int DetermineLineColor(
        string line,
        bool isSrtDocument,
        double framesPerSecond,
        int characterCount,
        long? nextStart,
        bool highlightInvalidFormat,
        bool highlightVeryShortLines,
        bool highlightShortLines)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return isSrtDocument || !highlightInvalidFormat ? 0 : InvalidFormatColorIndex;
        }

        if (isSrtDocument && !line.Contains("-->", StringComparison.Ordinal))
        {
            return 0;
        }

        long start;
        long end;
        if (SubtitleDocument.TryGetCueTiming(line, framesPerSecond, out start, out end, out var hasEnd))
        {
            if (!hasEnd)
            {
                return 0;
            }

            if (nextStart is { } value)
            {
                end = Math.Min(end, value);
            }
        }
        else if (SubtitleDocument.TryGetTmPlayerCue(line, out start, out var tmPlayerText))
        {
            if (nextStart is null)
            {
                return 0;
            }

            end = nextStart.Value;
            characterCount = tmPlayerText.Length;
        }
        else
        {
            return highlightInvalidFormat ? InvalidFormatColorIndex : 0;
        }

        var durationMilliseconds = end - start;
        var intensity = durationMilliseconds <= 0
            ? 200
            : Math.Clamp((int)Math.Round(characterCount * 7000d / durationMilliseconds, MidpointRounding.AwayFromZero), 0, 200);
        return intensity >= 200 && highlightVeryShortLines
            ? VeryShortColorIndex
            : highlightShortLines
                ? (int)Math.Round(intensity * (GrayShadeCount - 1) / 200d, MidpointRounding.AwayFromZero)
                : 0;
    }

    private IEnumerable<int> GetUnsortedLineIndices()
    {
        if (!_highlightUnsortedLines)
        {
            yield break;
        }

        long? previousStart = null;
        var isSrtDocument = Lines.Cast<Line>().Any(line => line.Text.Contains("-->", StringComparison.Ordinal));
        for (var lineIndex = 0; lineIndex < Lines.Count; lineIndex++)
        {
            var line = Lines[lineIndex].Text;
            if ((isSrtDocument && !line.Contains("-->", StringComparison.Ordinal)) ||
                !SubtitleDocument.TryGetCueStartMilliseconds(line, _framesPerSecond, out var start))
            {
                continue;
            }

            if (previousStart is { } value && start < value)
            {
                yield return lineIndex;
            }

            previousStart = start;
        }
    }

    internal static string GetCueText(string line, double framesPerSecond)
    {
        return SubtitleDocument.TryGetInlineCue(line, framesPerSecond, out _, out _, out var text) ? text : line;
    }

    internal static int GetSrtCueTextLength(IReadOnlyList<string> lines, int timingLineIndex)
    {
        var length = 0;
        for (var lineIndex = timingLineIndex + 1; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            length += line.Length;
        }

        return length;
    }

    private static int GetSemanticStyleIndex(int baseStyle, int colorIndex)
    {
        var baseStyleOffset = baseStyle switch
        {
            (int)Style.Default => 0,
            MarkupStyle => 1,
            BoldStyle => 2,
            ItalicStyle => 3,
            UnderlineStyle => 4,
            BoldItalicStyle => 5,
            BoldUnderlineStyle => 6,
            ItalicUnderlineStyle => 7,
            BoldItalicUnderlineStyle => 8,
            _ => 0
        };
        return SemanticStyleStart + colorIndex * 9 + baseStyleOffset;
    }

    private static int GetStyleIndex(SubtitleTextStyle style) => style switch
    {
        SubtitleTextStyle.Bold => BoldStyle,
        SubtitleTextStyle.Italic => ItalicStyle,
        SubtitleTextStyle.Underline => UnderlineStyle,
        SubtitleTextStyle.Bold | SubtitleTextStyle.Italic => BoldItalicStyle,
        SubtitleTextStyle.Bold | SubtitleTextStyle.Underline => BoldUnderlineStyle,
        SubtitleTextStyle.Italic | SubtitleTextStyle.Underline => ItalicUnderlineStyle,
        SubtitleTextStyle.Bold | SubtitleTextStyle.Italic | SubtitleTextStyle.Underline => BoldItalicUnderlineStyle,
        _ => (int)Style.Default
    };

    private void UpdateLineNumberMargin()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var digits = Math.Max(2, Lines.Count.ToString().Length);
        var width = TextWidth(Style.LineNumber, new string('9', digits)) + 10;
        if (Margins[0].Width == width)
        {
            return;
        }

        Margins[0].Width = width;
        LineNumberMarginWidthChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal readonly record struct SubtitleEditorViewState(int Line, int Column, int FirstVisibleLine);
