using System.Diagnostics;

namespace SubEdit.Player.WinForms;

internal sealed class AboutDialog : SubEditDialog
{
    internal const string DescriptionText =
        "SubEdit-Player Next\r\nopracowany przy pomocy AI\r\nna podstawie SubEdit-Player build 4072";
    private const string AboutLogoResourceName = "SubEdit.Player.WinForms.Assets.AboutLogo";
    private static readonly Color DialogBackColor = Color.FromArgb(27, 33, 41);
    private static readonly Color DialogTextColor = Color.FromArgb(231, 237, 244);
    private static readonly Color NextTextColor = Color.FromArgb(88, 183, 235);
    private readonly Image? _brandingImage;

    public AboutDialog()
    {
        _brandingImage = LoadBrandingImage();

        Text = "O programie";
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(620, 600);
        BackColor = DialogBackColor;
        ForeColor = DialogTextColor;
        Padding = new Padding(24);

        var layout = new TableLayoutPanel
        {
            BackColor = DialogBackColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 12
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

        layout.Controls.Add(CreateBrandingControl(), 0, 0);
        layout.Controls.Add(CreateTextLabel(
            $"Version {ApplicationBuildInfo.Current.ProductVersion}\r\nbuild date: {ApplicationBuildInfo.Current.BuildDate}", 10f), 0, 1);
        layout.Controls.Add(CreateTextLabel(DescriptionText, 10f), 0, 3);

        var authorLayout = new TableLayoutPanel
        {
            BackColor = DialogBackColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        authorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        authorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        authorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        authorLayout.Controls.Add(CreateTextLabel("TSS", 10f), 0, 0);
        authorLayout.Controls.Add(CreateLink("subedit@poczta.fm", "mailto:subedit@poczta.fm"), 0, 1);
        layout.Controls.Add(authorLayout, 0, 5);

        layout.Controls.Add(CreateTextLabel(
            "SubEdit-Player (c) by Artur Sikora\r\n2000-07-14 - 2008-12-17",
            10f), 0, 7);

        var linksLayout = new TableLayoutPanel
        {
            BackColor = DialogBackColor,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 2
        };
        linksLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        linksLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        linksLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        linksLayout.Controls.Add(CreateLink("https://subedit.com.pl", "https://subedit.com.pl"), 0, 0);
        linksLayout.Controls.Add(CreateLink("subedit@subedit.pl", "mailto:subedit@subedit.pl"), 0, 1);
        layout.Controls.Add(linksLayout, 0, 9);

        var okButton = new Button
        {
            Anchor = AnchorStyles.None,
            BackColor = DialogBackColor,
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            ForeColor = DialogTextColor,
            Margin = Padding.Empty,
            Size = new Size(100, 29),
            Text = "OK",
            UseVisualStyleBackColor = false
        };
        okButton.FlatAppearance.BorderColor = Color.DimGray;
        okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(48, 48, 48);
        okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 32, 32);

        var buttonLayout = new TableLayoutPanel
        {
            BackColor = DialogBackColor,
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        buttonLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        buttonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        buttonLayout.Controls.Add(okButton, 1, 0);
        layout.Controls.Add(buttonLayout, 0, 11);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = okButton;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _brandingImage?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Label CreateTextLabel(string text, float fontSize) => new()
    {
        BackColor = DialogBackColor,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", fontSize, FontStyle.Regular, GraphicsUnit.Point),
        ForeColor = DialogTextColor,
        Margin = Padding.Empty,
        Text = text,
        TextAlign = ContentAlignment.MiddleCenter
    };

    private Control CreateBrandingControl()
    {
        var branding = new TableLayoutPanel
        {
            Anchor = AnchorStyles.None,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = DialogBackColor,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            RowCount = 1
        };
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        branding.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        branding.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        branding.Controls.Add(new PictureBox
        {
            BackColor = DialogBackColor,
            Image = _brandingImage,
            Margin = new Padding(0, 0, 16, 0),
            Size = new Size(150, 150),
            SizeMode = PictureBoxSizeMode.Zoom,
            TabStop = false
        }, 0, 0);

        branding.Controls.Add(new BrandingTextControl { Anchor = AnchorStyles.Left }, 1, 0);

        return branding;
    }

    private sealed class BrandingTextControl : Control
    {
        private const string TitleText = "SubEdit-Player";
        private const string NextText = "Next";
        private const float BrandFontSize = 36f;
        private const int Gap = 3;
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        private readonly Font _titleFont;
        private readonly Font _nextFont;

        public BrandingTextControl()
        {
            using var brandFontFamily = new FontFamily("Segoe UI");
            _titleFont = new Font(brandFontFamily, BrandFontSize, FontStyle.Bold, GraphicsUnit.Point);
            _nextFont = new Font(brandFontFamily, BrandFontSize, FontStyle.Regular, GraphicsUnit.Point);

            AutoSize = true;
            BackColor = DialogBackColor;
            TabStop = false;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var layout = CalculateTextLayout(DeviceDpi);
            return new Size(layout.Width, layout.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var layout = CalculateTextLayout(e.Graphics.DpiY);

            TextRenderer.DrawText(e.Graphics, TitleText, _titleFont, layout.TitleLocation, DialogTextColor, TextFlags);
            TextRenderer.DrawText(e.Graphics, NextText, _nextFont, layout.NextLocation, NextTextColor, TextFlags);
        }

        private TextLayout CalculateTextLayout(float dpiY)
        {
            var titleBounds = GetGlyphBounds(TitleText, _titleFont, dpiY);
            var nextBounds = GetGlyphBounds(NextText, _nextFont, dpiY);
            var sharedGlyphLeft = Math.Max(titleBounds.Left, nextBounds.Left);
            var titleLocation = new Point(
                (int)Math.Ceiling(sharedGlyphLeft - titleBounds.Left),
                0);
            var nextLocation = new Point(
                (int)Math.Ceiling(sharedGlyphLeft - nextBounds.Left),
                (int)Math.Ceiling(titleLocation.Y + titleBounds.Bottom + Gap - nextBounds.Top));
            var width = (int)Math.Ceiling(Math.Max(
                titleLocation.X + titleBounds.Right,
                nextLocation.X + nextBounds.Right));
            var height = (int)Math.Ceiling(nextLocation.Y + nextBounds.Bottom);

            return new TextLayout(titleLocation, nextLocation, width, height);
        }

        private static RectangleF GetGlyphBounds(string text, Font font, float dpiY)
        {
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddString(
                text,
                font.FontFamily,
                (int)font.Style,
                font.SizeInPoints * dpiY / 72f,
                Point.Empty,
                StringFormat.GenericTypographic);
            return path.GetBounds();
        }

        private readonly record struct TextLayout(Point TitleLocation, Point NextLocation, int Width, int Height);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _nextFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private LinkLabel CreateLink(string text, string address)
    {
        var link = new LinkLabel
        {
            ActiveLinkColor = Color.LightSkyBlue,
            BackColor = DialogBackColor,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = DialogTextColor,
            LinkBehavior = LinkBehavior.HoverUnderline,
            LinkColor = DialogTextColor,
            Margin = Padding.Empty,
            TabStop = true,
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            VisitedLinkColor = DialogTextColor
        };
        link.LinkClicked += (_, _) => OpenExternal(address);
        return link;
    }

    private static Image? LoadBrandingImage()
    {
        using var stream = typeof(AboutDialog).Assembly.GetManifestResourceStream(AboutLogoResourceName);
        if (stream is null)
        {
            return null;
        }

        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }

    private void OpenExternal(string address)
    {
        try
        {
            Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Nie można otworzyć odnośnika", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
