using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using SubEdit.Player.Core.Configuration;

namespace SubEdit.Player.WinForms;

internal sealed class VideoOverlayForm : Form
{
    private const float SubtitleFontReferenceHeight = 768f;
    private const int OsdOuterMargin = 10;
    private const int OsdItemSpacing = 4;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x80;
    private const int WsExLayered = 0x00080000;
    private const int UlwAlpha = 0x00000002;
    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);
    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;
    private string _osdText = string.Empty;
    private string _playbackText = string.Empty;
    private string _volumeText = string.Empty;
    private SubtitleCue? _subtitleCue;
    private SubtitleCue? _subtitlePreviewCue;
    private Size _videoSourceSize;
    private bool _isFullScreen;
    private bool _transparentSubtitles = true;
    private string _subtitleFontName = "Arial";
    private int _subtitleFontSize = 48;
    private bool _subtitleFontBold;
    private SubtitleFontSizeMode _subtitleFontSizeMode = SubtitleFontSizeMode.RelativeToWindow;
    private int _minimumSubtitleFontSize = 8;
    private Color _subtitleTextColor = Color.White;
    private Color _subtitleBackgroundColor = Color.Black;
    private int _subtitleBackgroundOpacityPercent;
    private Color _subtitleOutlineColor = Color.Black;
    private int _subtitleOutlineThickness;
    private SubtitleAlignment _subtitleAlignment = SubtitleAlignment.BottomCenter;
    private int _subtitleMarginLeft;
    private int _subtitleMarginRight;
    private int _subtitleMarginTop = 20;
    private int _subtitleMarginBottom = 1;
    private bool _positionSubtitlesRelativeToVideoFrame = true;
    private int _videoVerticalOffset;
    private bool _adjustLineWidthToScreen = true;
    private int _osdFontSize = 14;
    private Color _osdTextColor = Color.Gainsboro;
    private Color _osdBackgroundColor = Color.FromArgb(24, 30, 36);
    private int _osdBackgroundOpacityPercent = 50;
    private Color _osdBorderColor = Color.FromArgb(36, 42, 46);
    private int _osdBorderThickness = 1;

    public VideoOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OsdText
    {
        get => _osdText;
        set { _osdText = value; RenderLayered(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string VolumeText
    {
        get => _volumeText;
        set { _volumeText = value; RenderLayered(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaybackText
    {
        get => _playbackText;
        set { _playbackText = value; RenderLayered(); }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int OsdFontSize
    {
        get => _osdFontSize;
        set
        {
            var normalized = Math.Clamp(value, 10, 30);
            if (_osdFontSize == normalized)
            {
                return;
            }

            _osdFontSize = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OsdTextColor
    {
        get => _osdTextColor;
        set
        {
            var normalized = Color.FromArgb(255, value);
            if (_osdTextColor == normalized)
            {
                return;
            }

            _osdTextColor = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OsdBackgroundColor
    {
        get => _osdBackgroundColor;
        set
        {
            var normalized = Color.FromArgb(255, value);
            if (_osdBackgroundColor == normalized)
            {
                return;
            }

            _osdBackgroundColor = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int OsdBackgroundOpacityPercent
    {
        get => _osdBackgroundOpacityPercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (_osdBackgroundOpacityPercent == normalized)
            {
                return;
            }

            _osdBackgroundOpacityPercent = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color OsdBorderColor
    {
        get => _osdBorderColor;
        set
        {
            var normalized = Color.FromArgb(255, value);
            if (_osdBorderColor == normalized)
            {
                return;
            }

            _osdBorderColor = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int OsdBorderThickness
    {
        get => _osdBorderThickness;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (_osdBorderThickness == normalized)
            {
                return;
            }

            _osdBorderThickness = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SubtitleCue? SubtitleCue
    {
        get => _subtitleCue;
        set
        {
            if (ReferenceEquals(_subtitleCue, value))
            {
                return;
            }

            _subtitleCue = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SubtitleCue? SubtitlePreviewCue
    {
        get => _subtitlePreviewCue;
        set
        {
            _subtitlePreviewCue = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleFontSize
    {
        get => _subtitleFontSize;
        set
        {
            var normalized = Math.Clamp(value, 8, 72);
            if (_subtitleFontSize == normalized)
            {
                return;
            }

            _subtitleFontSize = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string SubtitleFontName
    {
        get => _subtitleFontName;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Arial" : value;
            if (string.Equals(_subtitleFontName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _subtitleFontName = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SubtitleFontBold
    {
        get => _subtitleFontBold;
        set
        {
            if (_subtitleFontBold == value)
            {
                return;
            }

            _subtitleFontBold = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SubtitleFontSizeMode SubtitleFontSizeMode
    {
        get => _subtitleFontSizeMode;
        set
        {
            if (_subtitleFontSizeMode == value)
            {
                return;
            }

            _subtitleFontSizeMode = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MinimumSubtitleFontSize
    {
        get => _minimumSubtitleFontSize;
        set
        {
            var normalized = Math.Clamp(value, 2, 50);
            if (_minimumSubtitleFontSize == normalized)
            {
                return;
            }

            _minimumSubtitleFontSize = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SubtitleTextColor
    {
        get => _subtitleTextColor;
        set
        {
            if (_subtitleTextColor == value)
            {
                return;
            }

            _subtitleTextColor = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SubtitleBackgroundColor
    {
        get => _subtitleBackgroundColor;
        set
        {
            if (_subtitleBackgroundColor == value)
            {
                return;
            }

            _subtitleBackgroundColor = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleBackgroundOpacityPercent
    {
        get => _subtitleBackgroundOpacityPercent;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (_subtitleBackgroundOpacityPercent == normalized)
            {
                return;
            }

            _subtitleBackgroundOpacityPercent = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SubtitleOutlineColor
    {
        get => _subtitleOutlineColor;
        set
        {
            if (_subtitleOutlineColor == value)
            {
                return;
            }

            _subtitleOutlineColor = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleOutlineThickness
    {
        get => _subtitleOutlineThickness;
        set
        {
            var normalized = Math.Clamp(value, 0, 100);
            if (_subtitleOutlineThickness == normalized)
            {
                return;
            }

            _subtitleOutlineThickness = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SubtitleAlignment SubtitleAlignment
    {
        get => _subtitleAlignment;
        set
        {
            var normalized = Enum.IsDefined(value) ? value : SubtitleAlignment.BottomCenter;
            if (_subtitleAlignment == normalized)
            {
                return;
            }

            _subtitleAlignment = normalized;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleMarginLeft
    {
        get => _subtitleMarginLeft;
        set => SetSubtitleMargin(ref _subtitleMarginLeft, value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleMarginRight
    {
        get => _subtitleMarginRight;
        set => SetSubtitleMargin(ref _subtitleMarginRight, value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleMarginTop
    {
        get => _subtitleMarginTop;
        set => SetSubtitleMargin(ref _subtitleMarginTop, value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SubtitleMarginBottom
    {
        get => _subtitleMarginBottom;
        set => SetSubtitleMargin(ref _subtitleMarginBottom, value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool PositionSubtitlesRelativeToVideoFrame
    {
        get => _positionSubtitlesRelativeToVideoFrame;
        set
        {
            if (_positionSubtitlesRelativeToVideoFrame == value)
            {
                return;
            }

            _positionSubtitlesRelativeToVideoFrame = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AdjustLineWidthToScreen
    {
        get => _adjustLineWidthToScreen;
        set
        {
            if (_adjustLineWidthToScreen == value)
            {
                return;
            }

            _adjustLineWidthToScreen = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size VideoSourceSize
    {
        get => _videoSourceSize;
        set
        {
            if (_videoSourceSize == value)
            {
                return;
            }

            _videoSourceSize = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VideoVerticalOffset
    {
        get => _videoVerticalOffset;
        set
        {
            if (_videoVerticalOffset == value)
            {
                return;
            }

            _videoVerticalOffset = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (_isFullScreen == value)
            {
                return;
            }

            _isFullScreen = value;
            RenderLayered();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TransparentSubtitles
    {
        get => _transparentSubtitles;
        set
        {
            if (_transparentSubtitles == value)
            {
                return;
            }

            _transparentSubtitles = value;
            RenderLayered();
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExTransparent | WsExNoActivate | WsExToolWindow | WsExLayered;
            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            m.Result = HtTransparent;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RenderLayered();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RenderLayered();
    }

    public void BringToFrontWithoutActivation()
    {
        if (IsHandleCreated)
        {
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate | SwpNoOwnerZOrder);
        }
    }

    private void RenderLayered()
    {
        if (!IsHandleCreated || !Visible || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawOverlay(graphics);
        }

        using var screen = Graphics.FromHwnd(IntPtr.Zero);
        var screenDc = IntPtr.Zero;
        var memoryDc = IntPtr.Zero;
        var bitmapHandle = IntPtr.Zero;
        var oldBitmap = IntPtr.Zero;
        var bitmapSelected = false;
        try
        {
            screenDc = screen.GetHdc();
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                return;
            }

            bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
            if (bitmapHandle == IntPtr.Zero)
            {
                return;
            }

            oldBitmap = SelectObject(memoryDc, bitmapHandle);
            if (oldBitmap == IntPtr.Zero || oldBitmap == HgdiError)
            {
                return;
            }

            bitmapSelected = true;

            var size = bitmap.Size;
            var source = Point.Empty;
            var location = Location;
            var blend = new BlendFunction(AcSrcOver, 0, 255, AcSrcAlpha);
            UpdateLayeredWindow(Handle, screenDc, ref location, ref size, memoryDc, ref source, 0, ref blend, UlwAlpha);
        }
        finally
        {
            if (bitmapSelected && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, oldBitmap);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                screen.ReleaseHdc(screenDc);
            }
        }
    }

    private void DrawOverlay(Graphics graphics)
    {
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var osdFont = new Font("Segoe UI", _osdFontSize, FontStyle.Regular);
        using var textFormat = (StringFormat)StringFormat.GenericTypographic.Clone();
        textFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        var backgroundAlpha = OsdAppearanceRenderer.GetAlpha(_osdBackgroundOpacityPercent);
        using var background = new SolidBrush(Color.FromArgb(backgroundAlpha, _osdBackgroundColor));
        using var border = new SolidBrush(Color.FromArgb(backgroundAlpha, _osdBorderColor));
        using var foreground = new SolidBrush(Color.FromArgb(255, _osdTextColor));
        if (!string.IsNullOrEmpty(_osdText))
        {
            var layoutSize = TextRenderer.MeasureText(_osdText, osdFont);
            var textWidth = MeasureOverlayTextWidth(graphics, _osdText, osdFont, textFormat);
            var timeBounds = new Rectangle(
                OsdOuterMargin,
                OsdOuterMargin,
                textWidth + (2 * OsdAppearanceRenderer.HorizontalPadding),
                layoutSize.Height + (2 * OsdAppearanceRenderer.VerticalPadding));
            OsdAppearanceRenderer.DrawBackground(
                graphics,
                timeBounds,
                border,
                background,
                _osdBorderThickness);
            graphics.DrawString(
                _osdText,
                osdFont,
                foreground,
                new PointF(
                    timeBounds.X + OsdAppearanceRenderer.HorizontalPadding,
                    timeBounds.Y + OsdAppearanceRenderer.VerticalPadding),
                textFormat);
        }

        var rightTop = OsdOuterMargin;
        if (!string.IsNullOrEmpty(_playbackText))
        {
            var playbackLayoutSize = TextRenderer.MeasureText(_playbackText, osdFont);
            var playbackTextWidth = MeasureOverlayTextWidth(graphics, _playbackText, osdFont, textFormat);
            var playbackBounds = new Rectangle(
                ClientSize.Width - OsdOuterMargin - playbackTextWidth - (2 * OsdAppearanceRenderer.HorizontalPadding),
                rightTop,
                playbackTextWidth + (2 * OsdAppearanceRenderer.HorizontalPadding),
                playbackLayoutSize.Height + (2 * OsdAppearanceRenderer.VerticalPadding));
            OsdAppearanceRenderer.DrawBackground(
                graphics,
                playbackBounds,
                border,
                background,
                _osdBorderThickness);
            graphics.DrawString(
                _playbackText,
                osdFont,
                foreground,
                new PointF(
                    playbackBounds.X + OsdAppearanceRenderer.HorizontalPadding,
                    playbackBounds.Y + OsdAppearanceRenderer.VerticalPadding),
                textFormat);
            rightTop = playbackBounds.Bottom + OsdItemSpacing;
        }

        if (!string.IsNullOrEmpty(_volumeText))
        {
            var volumeLayoutSize = TextRenderer.MeasureText(_volumeText, osdFont);
            var volumeTextWidth = MeasureOverlayTextWidth(graphics, _volumeText, osdFont, textFormat);
            var volumeBounds = new Rectangle(
                ClientSize.Width - OsdOuterMargin - volumeTextWidth - (2 * OsdAppearanceRenderer.HorizontalPadding),
                rightTop,
                volumeTextWidth + (2 * OsdAppearanceRenderer.HorizontalPadding),
                volumeLayoutSize.Height + (2 * OsdAppearanceRenderer.VerticalPadding));
            OsdAppearanceRenderer.DrawBackground(
                graphics,
                volumeBounds,
                border,
                background,
                _osdBorderThickness);
            graphics.DrawString(
                _volumeText,
                osdFont,
                foreground,
                new PointF(
                    volumeBounds.X + OsdAppearanceRenderer.HorizontalPadding,
                    volumeBounds.Y + OsdAppearanceRenderer.VerticalPadding),
                textFormat);
        }

        DrawSubtitle(graphics);
    }

    private static int MeasureOverlayTextWidth(Graphics graphics, string text, Font font, StringFormat format)
    {
        var measured = graphics.MeasureString(text, font, PointF.Empty, format);
        return Math.Max(1, (int)Math.Ceiling(measured.Width));
    }

    private void DrawSubtitle(Graphics graphics)
    {
        var cue = _subtitlePreviewCue ?? _subtitleCue;
        if (cue is null || cue.Lines.Count == 0)
        {
            return;
        }

        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var textFormat = new StringFormat(StringFormat.GenericTypographic)
        {
            FormatFlags = StringFormatFlags.MeasureTrailingSpaces
        };
        var fontSizePoints = _subtitleFontSizeMode == SubtitleFontSizeMode.Constant
            ? _subtitleFontSize
            : Math.Max(
                _minimumSubtitleFontSize,
                (float)Math.Floor(ClientSize.Height * _subtitleFontSize / SubtitleFontReferenceHeight + 0.5f));
        var fontSize = fontSizePoints * DeviceDpi / 72f;
        var minimumLineHeight = fontSize * 1.1f;
        var lineSpacing = Math.Max(1, fontSize * 0.03f);
        var outlineWidth = _subtitleOutlineThickness * DeviceDpi / 96f;
        var position = cue.Position;
        var positionedCue = position.HasValue;
        var placementBounds = positionedCue
            ? new RectangleF(12f, 0f, Math.Max(1f, ClientSize.Width - 24f), ClientSize.Height)
            : GetSubtitlePlacementBounds();
        var lines = _adjustLineWidthToScreen
            ? WrapSubtitleLines(cue.Lines, graphics, textFormat, fontSize, Math.Max(1f, placementBounds.Width))
            : cue.Lines;
        var lineWidths = new float[lines.Count];
        var lineHeights = new float[lines.Count];
        var totalHeight = 0f;
        for (var index = 0; index < lines.Count; index++)
        {
            foreach (var run in lines[index].Runs)
            {
                using var font = CreateSubtitleFont(run, fontSize);
                var size = graphics.MeasureString(run.Text, font, int.MaxValue, textFormat);
                lineWidths[index] += size.Width;
                lineHeights[index] = Math.Max(lineHeights[index], size.Height);
            }

            totalHeight += Math.Max(lineHeights[index], minimumLineHeight) + lineSpacing;
        }

        var contentHeight = Math.Max(0f, totalHeight - lineSpacing);
        var anchor = positionedCue ? ScaleSubtitlePosition(position.GetValueOrDefault()) : PointF.Empty;
        var y = positionedCue
            ? anchor.Y
            : GetAlignedSubtitleY(placementBounds, contentHeight);
        if (!positionedCue && IsTopSubtitleAlignment())
        {
            y -= GetVisibleSubtitleTopOffset(lines[0], textFormat, fontSize, outlineWidth);
        }
        else if (!positionedCue && IsBottomSubtitleAlignment())
        {
            y += contentHeight - GetVisibleSubtitleBottomOffset(
                lines,
                lineHeights,
                textFormat,
                fontSize,
                minimumLineHeight,
                lineSpacing,
                outlineWidth);
        }

        var lineXs = new float[lines.Count];
        var lineYs = new float[lines.Count];
        var lineEffectiveHeights = new float[lines.Count];

        var currentY = y;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            lineXs[lineIndex] = positionedCue
                ? Math.Clamp(anchor.X, 12f, Math.Max(12f, ClientSize.Width - lineWidths[lineIndex] - 12f))
                : GetAlignedSubtitleX(placementBounds, lineWidths[lineIndex]);
            lineYs[lineIndex] = currentY;
            lineEffectiveHeights[lineIndex] = Math.Max(lineHeights[lineIndex], minimumLineHeight);
            currentY += lineEffectiveHeights[lineIndex] + lineSpacing;
        }

        var backgroundOpacityPercent = _transparentSubtitles ? 0 : _subtitleBackgroundOpacityPercent;
        if (backgroundOpacityPercent > 0)
        {
            var horizontalPadding = Math.Max(4f, fontSize * 0.14f);
            var verticalPadding = Math.Max(2f, fontSize * 0.07f);
            var alpha = (int)Math.Round(backgroundOpacityPercent * 255d / 100d, MidpointRounding.AwayFromZero);
            using var background = new SolidBrush(Color.FromArgb(alpha, _subtitleBackgroundColor));
            using var backgroundPath = new GraphicsPath(FillMode.Winding);
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                backgroundPath.AddRectangle(new RectangleF(
                    lineXs[lineIndex] - horizontalPadding,
                    lineYs[lineIndex] - verticalPadding,
                    lineWidths[lineIndex] + (horizontalPadding * 2),
                    lineEffectiveHeights[lineIndex] + (verticalPadding * 2)));
            }

            graphics.FillPath(background, backgroundPath);
        }

        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var currentX = lineXs[lineIndex];
            var currentLineY = lineYs[lineIndex];
            foreach (var run in lines[lineIndex].Runs)
            {
                using var font = CreateSubtitleFont(run, fontSize);
                var size = graphics.MeasureString(run.Text, font, int.MaxValue, textFormat);
                using var foreground = new SolidBrush(run.Color ?? _subtitleTextColor);
                using var glyphs = new GraphicsPath();
                glyphs.AddString(run.Text, font.FontFamily, (int)font.Style, font.Size, new PointF(currentX, currentLineY), textFormat);
                if (outlineWidth > 0f)
                {
                    using var outline = new Pen(_subtitleOutlineColor, outlineWidth) { LineJoin = LineJoin.Round };
                    graphics.DrawPath(outline, glyphs);
                }
                graphics.FillPath(foreground, glyphs);
                currentX += size.Width;
            }
        }
    }

    private void SetSubtitleMargin(ref int field, int value)
    {
        var normalized = Math.Clamp(value, -10000, 10000);
        if (field == normalized)
        {
            return;
        }

        field = normalized;
        RenderLayered();
    }

    private RectangleF GetSubtitlePlacementBounds()
    {
        var target = new RectangleF(0f, 0f, ClientSize.Width, ClientSize.Height);
        if (_positionSubtitlesRelativeToVideoFrame && _videoSourceSize.Width > 0 && _videoSourceSize.Height > 0)
        {
            var scale = Math.Min(
                ClientSize.Width / (float)_videoSourceSize.Width,
                ClientSize.Height / (float)_videoSourceSize.Height);
            var width = _videoSourceSize.Width * scale;
            var height = _videoSourceSize.Height * scale;
            target = new RectangleF(
                (ClientSize.Width - width) / 2f,
                ((ClientSize.Height - height) / 2f) - _videoVerticalOffset,
                width,
                height);
        }

        var left = target.Left + _subtitleMarginLeft;
        var top = target.Top + _subtitleMarginTop;
        var right = target.Right - _subtitleMarginRight;
        var bottom = target.Bottom - _subtitleMarginBottom;
        if (right <= left)
        {
            right = left + 1f;
        }
        if (bottom <= top)
        {
            bottom = top + 1f;
        }

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    private float GetAlignedSubtitleX(RectangleF bounds, float lineWidth) => _subtitleAlignment switch
    {
        SubtitleAlignment.TopLeft or SubtitleAlignment.MiddleLeft or SubtitleAlignment.BottomLeft => bounds.Left,
        SubtitleAlignment.TopRight or SubtitleAlignment.MiddleRight or SubtitleAlignment.BottomRight => bounds.Right - lineWidth,
        _ => bounds.Left + ((bounds.Width - lineWidth) / 2f)
    };

    private float GetAlignedSubtitleY(RectangleF bounds, float contentHeight) => _subtitleAlignment switch
    {
        SubtitleAlignment.TopLeft or SubtitleAlignment.TopCenter or SubtitleAlignment.TopRight => bounds.Top,
        SubtitleAlignment.MiddleLeft or SubtitleAlignment.MiddleCenter or SubtitleAlignment.MiddleRight =>
            bounds.Top + ((bounds.Height - contentHeight) / 2f),
        _ => bounds.Bottom - contentHeight
    };

    private bool IsTopSubtitleAlignment() => _subtitleAlignment is
        SubtitleAlignment.TopLeft or SubtitleAlignment.TopCenter or SubtitleAlignment.TopRight;

    private bool IsBottomSubtitleAlignment() => _subtitleAlignment is
        SubtitleAlignment.BottomLeft or SubtitleAlignment.BottomCenter or SubtitleAlignment.BottomRight;

    private float GetVisibleSubtitleTopOffset(
        SubtitleLine line,
        StringFormat textFormat,
        float fontSize,
        float outlineWidth)
    {
        float? visibleTop = null;
        foreach (var run in line.Runs)
        {
            if (string.IsNullOrEmpty(run.Text))
            {
                continue;
            }

            using var font = CreateSubtitleFont(run, fontSize);
            using var glyphs = new GraphicsPath();
            glyphs.AddString(run.Text, font.FontFamily, (int)font.Style, font.Size, PointF.Empty, textFormat);
            if (glyphs.PointCount == 0)
            {
                continue;
            }

            var runVisibleTop = GetOutlinedGlyphBounds(glyphs, outlineWidth).Top;
            visibleTop = visibleTop.HasValue
                ? Math.Min(visibleTop.Value, runVisibleTop)
                : runVisibleTop;
        }

        return visibleTop ?? 0f;
    }

    private float GetVisibleSubtitleBottomOffset(
        IReadOnlyList<SubtitleLine> lines,
        IReadOnlyList<float> lineHeights,
        StringFormat textFormat,
        float fontSize,
        float minimumLineHeight,
        float lineSpacing,
        float outlineWidth)
    {
        float? visibleBottom = null;
        var lineY = 0f;
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            foreach (var run in lines[lineIndex].Runs)
            {
                if (string.IsNullOrEmpty(run.Text))
                {
                    continue;
                }

                using var font = CreateSubtitleFont(run, fontSize);
                using var glyphs = new GraphicsPath();
                glyphs.AddString(run.Text, font.FontFamily, (int)font.Style, font.Size, PointF.Empty, textFormat);
                if (glyphs.PointCount == 0)
                {
                    continue;
                }

                var runVisibleBottom = lineY + GetOutlinedGlyphBounds(glyphs, outlineWidth).Bottom;
                visibleBottom = visibleBottom.HasValue
                    ? Math.Max(visibleBottom.Value, runVisibleBottom)
                    : runVisibleBottom;
            }

            lineY += Math.Max(lineHeights[lineIndex], minimumLineHeight) + lineSpacing;
        }

        return visibleBottom ?? Math.Max(0f, lineY - lineSpacing);
    }

    private static RectangleF GetOutlinedGlyphBounds(GraphicsPath glyphs, float outlineWidth)
    {
        if (outlineWidth <= 0f)
        {
            return glyphs.GetBounds();
        }

        using var outline = new Pen(Color.Black, outlineWidth) { LineJoin = LineJoin.Round };
        return glyphs.GetBounds(matrix: null, pen: outline);
    }

    private IReadOnlyList<SubtitleLine> WrapSubtitleLines(
        IReadOnlyList<SubtitleLine> sourceLines,
        Graphics graphics,
        StringFormat textFormat,
        float fontSize,
        float maximumWidth)
    {
        var wrapped = new List<SubtitleLine>();
        foreach (var sourceLine in sourceLines)
        {
            var wrappedLineCountBefore = wrapped.Count;
            var currentRuns = new List<SubtitleTextRun>();
            var currentWidth = 0f;
            void FinishLine()
            {
                if (currentRuns.Count > 0)
                {
                    wrapped.Add(new SubtitleLine(currentRuns.ToArray()));
                    currentRuns.Clear();
                    currentWidth = 0f;
                }
            }

            foreach (var run in sourceLine.Runs)
            {
                for (var index = 0; index < run.Text.Length;)
                {
                    var whitespace = char.IsWhiteSpace(run.Text[index]);
                    var segmentStart = index;
                    while (index < run.Text.Length && char.IsWhiteSpace(run.Text[index]) == whitespace)
                    {
                        index++;
                    }

                    var segment = run.Text[segmentStart..index];
                    using var font = CreateSubtitleFont(run, fontSize);
                    var segmentWidth = graphics.MeasureString(segment, font, int.MaxValue, textFormat).Width;
                    if (whitespace && currentRuns.Count == 0)
                    {
                        continue;
                    }

                    if (!whitespace && currentRuns.Count > 0 && currentWidth + segmentWidth > maximumWidth)
                    {
                        FinishLine();
                    }

                    if (whitespace && currentWidth + segmentWidth > maximumWidth)
                    {
                        FinishLine();
                        continue;
                    }

                    AppendRun(currentRuns, run with { Text = segment });
                    currentWidth += segmentWidth;
                }
            }

            FinishLine();
            if (wrapped.Count == wrappedLineCountBefore)
            {
                wrapped.Add(sourceLine);
            }
        }

        return wrapped;
    }

    private static void AppendRun(List<SubtitleTextRun> runs, SubtitleTextRun run)
    {
        if (runs.Count > 0)
        {
            var previous = runs[^1];
            if (previous.Style == run.Style &&
                previous.Color == run.Color &&
                previous.FontName == run.FontName &&
                previous.FontSize == run.FontSize)
            {
                runs[^1] = previous with { Text = previous.Text + run.Text };
                return;
            }
        }

        runs.Add(run);
    }

    private PointF ScaleSubtitlePosition(Point position)
    {
        if (_videoSourceSize.Width <= 0 || _videoSourceSize.Height <= 0)
        {
            return position;
        }

        return new PointF(
            position.X * ClientSize.Width / (float)_videoSourceSize.Width,
            position.Y * ClientSize.Height / (float)_videoSourceSize.Height);
    }

    private Font CreateSubtitleFont(SubtitleTextRun run, float defaultSize)
    {
        var fontStyle = _subtitleFontBold ? FontStyle.Bold : FontStyle.Regular;
        if (run.Style.HasFlag(SubtitleTextStyle.Bold)) fontStyle |= FontStyle.Bold;
        if (run.Style.HasFlag(SubtitleTextStyle.Italic)) fontStyle |= FontStyle.Italic;
        if (run.Style.HasFlag(SubtitleTextStyle.Underline)) fontStyle |= FontStyle.Underline;
        if (run.Style.HasFlag(SubtitleTextStyle.Strikeout)) fontStyle |= FontStyle.Strikeout;
        var size = run.FontSize is { } specifiedSize
            ? specifiedSize * DeviceDpi / 72f
            : defaultSize;
        try
        {
            return new Font(string.IsNullOrWhiteSpace(run.FontName) ? _subtitleFontName : run.FontName, size, fontStyle, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            return new Font("Arial", size, fontStyle, GraphicsUnit.Pixel);
        }
    }

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;
    private static readonly IntPtr HgdiError = new(-1);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr windowHandle,
        IntPtr destinationDc,
        ref Point destinationPosition,
        ref Size size,
        IntPtr sourceDc,
        ref Point sourcePosition,
        int colorKey,
        ref BlendFunction blend,
        int flags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr objectHandle);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr dc);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct BlendFunction(byte blendOperation, byte blendFlags, byte sourceConstantAlpha, byte alphaFormat)
    {
        public readonly byte BlendOperation = blendOperation;
        public readonly byte BlendFlags = blendFlags;
        public readonly byte SourceConstantAlpha = sourceConstantAlpha;
        public readonly byte AlphaFormat = alphaFormat;
    }
}
