using System.Runtime.InteropServices;
using SubEdit.Player.WinForms.Controls;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    private static Panel CreateFullScreenControlsPanel(
        out StatusStrip statusStrip,
        out ToolStripStatusLabel playbackStatus,
        out ToolStripStatusLabel fileNameStatus,
        out ToolStripStatusLabel timeStatus)
    {
        var panel = new Panel
        {
            BackColor = FullScreenToolbarRenderer.BackgroundColor,
            Visible = false
        };
        statusStrip = new StatusStrip
        {
            AutoSize = false,
            SizingGrip = false,
            Padding = new Padding(3, 0, 3, 0),
            BackColor = Color.Black,
            Renderer = new FullScreenToolbarRenderer()
        };
        playbackStatus = new ToolStripStatusLabel
        {
            AutoSize = false,
            Overflow = ToolStripItemOverflow.Never,
            ForeColor = Color.Gainsboro,
            Margin = Padding.Empty
        };
        fileNameStatus = new ToolStripStatusLabel
        {
            AutoSize = false,
            Overflow = ToolStripItemOverflow.Never,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            Margin = Padding.Empty
        };
        timeStatus = new ToolStripStatusLabel
        {
            AutoSize = false,
            Overflow = ToolStripItemOverflow.Never,
            Width = 120,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.Gainsboro,
            Margin = Padding.Empty
        };
        statusStrip.Items.AddRange([playbackStatus, fileNameStatus, timeStatus]);
        panel.Controls.Add(statusStrip);
        return panel;
    }


    private CleanSplitContainer CreateSplitContainer(
        out Panel videoPanel,
        out Panel editorPanel,
        out ToolStrip transport,
        out VolumeTrackBar volumeSlider,
        out ChapterSeekBar seekSlider,
        out SubtitleTextEditor subtitleEditor,
        out Label editorHeader,
        out SubtitleInfoBar subtitleInfoBar)
    {
        var split = new CleanSplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            BackColor = SystemColors.Control,
            Panel1MinSize = 150,
            Panel2MinSize = 150
        };
        videoPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        var videoSurface = videoPanel;
        var osd = new Label
        {
            AutoSize = true,
            Location = new Point(4, 5),
            ForeColor = Color.FromArgb(255, 220, 180),
            BackColor = Color.Black,
            Font = new Font(FontFamily.GenericMonospace, 12, FontStyle.Regular),
            Text = "0:00:00/0:00:00"
        };
        var logo = new Label
        {
            AutoSize = true,
            ForeColor = Color.FromArgb(44, 180, 245),
            BackColor = Color.Black,
            Font = new Font("Arial", 28, FontStyle.Bold | FontStyle.Italic),
            Text = "SubEdit"
        };
        videoSurface.Resize += (_, _) => logo.Location = new Point(
            Math.Max(20, (videoSurface.ClientSize.Width - logo.Width) / 2),
            Math.Max(40, (videoSurface.ClientSize.Height - logo.Height) / 2));
        videoPanel.Controls.Add(osd);
        videoPanel.Controls.Add(logo);
        videoPanel.DoubleClick += (_, _) => ToggleFullScreenFromVideoSurface();
        osd.DoubleClick += (_, _) => ToggleFullScreenFromVideoSurface();
        logo.DoubleClick += (_, _) => ToggleFullScreenFromVideoSurface();
        videoPanel.MouseMove += (_, _) => UpdateFullScreenControlsFromPointer();
        osd.MouseMove += (_, _) => UpdateFullScreenControlsFromPointer();
        logo.MouseMove += (_, _) => UpdateFullScreenControlsFromPointer();

        seekSlider = CreateSeekSlider();

        transport = new ActivatingToolStrip
        {
            Dock = DockStyle.Bottom,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new Size(20, 20),
            Margin = Padding.Empty,
            Padding = new Padding(3, 0, 0, 0),
            BackColor = SystemColors.Control,
            RenderMode = ToolStripRenderMode.System
        };
        volumeSlider = CreateVolumeSlider();
        transport.Items.AddRange([
            new PlaybackToolbarButton(PlaybackToolbarCommand.Play, "Odtwarzaj (Ctrl+L)", checkOnClick: true),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Pause, "Pauza (Ctrl+L)", checkOnClick: true),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Stop, "Stop (Ctrl+M)"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.SeekBackward, "Wstecz o 5 sekund (Ctrl+[)"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.SeekForward, "W przód o 5 sekund (Ctrl+])"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.PreviousFile, "Wczytaj poprzedni plik (Alt+,)"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.NextFile, "Wczytaj następny plik (Alt+.)"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.PlayDirectory, "Odtwarzaj filmy w aktualnym katalogu (Ctrl+Alt+.)", checkOnClick: true),
            new ToolStripSeparator(),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Subtitles, "Napisy"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Audio, "Ścieżka audio"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Video, "Ścieżka wideo"),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Chapters, "Rozdziały"),
            new SpringToolStripSpacer(),
            new PlaybackToolbarButton(PlaybackToolbarCommand.Volume, "Głośność"),
            new ToolStripControlHost(volumeSlider) { AutoSize = false, Width = 90 }
        ]);
        ConfigureToolbarToolTipBehavior(transport);
        split.Panel1.Controls.Add(videoPanel);
        split.Panel1.Controls.Add(seekSlider);
        split.Panel1.Controls.Add(transport);
        split.SetTransportToolbarRepaintTarget(transport);
        split.SetSeekSliderRepaintTarget(seekSlider);

        // Only the spring space before the Volume group is shortened. Keep host
        // size, icon-to-slider gap and the preceding transport items unchanged.
        var alignedToolbar = transport;
        var alignedSeek = seekSlider;
        var alignedVolume = volumeSlider;
        var volumeHost = transport.Items.OfType<ToolStripControlHost>().Single();
        var aligningVolume = false;
        void AlignVolumeGroup(object? sender, EventArgs e)
        {
            if (aligningVolume || alignedToolbar.IsDisposed || alignedSeek.IsDisposed || alignedVolume.IsDisposed) return;
            var padding = alignedToolbar.Padding;
            var inEditor = alignedToolbar.Parent == split.Panel1 && !split.Panel2Collapsed;
            var inFullscreen = alignedToolbar.Parent is not null && alignedToolbar.Parent != split.Panel1;
            var opticalInset = inEditor || inFullscreen ? VolumeGroupOpticalInset(alignedVolume.DeviceDpi) : 0;
            var right = opticalInset;
            if (inEditor)
            {
                // Dock layout can update the two rows in separate steps. Wait for
                // matching bounds instead of deriving a transient, oversized inset.
                if (alignedSeek.Parent != split.Panel1 || alignedSeek.Width != alignedToolbar.Width ||
                    alignedVolume.Parent != alignedToolbar || volumeHost.Placement != ToolStripItemPlacement.Main ||
                    alignedVolume.Width <= 0) return;
                var geometry = VolumeTrackBar.Geometry(alignedVolume.ClientSize, alignedVolume.DeviceDpi,
                    alignedVolume.Minimum, alignedVolume.Maximum, alignedVolume.Maximum);
                var volumeRight = alignedToolbar.Left + alignedVolume.Left + geometry.Thumb.Right - 1;
                var seekRight = alignedSeek.Left + alignedSeek.MaximumThumbRightPixel;
                // Include the target offset in the residual, so later Layout calls
                // keep the same position rather than adding the offset again.
                right = Math.Max(opticalInset, padding.Right + volumeRight - seekRight + opticalInset);
            }
            if (padding.Right == right) return;
            aligningVolume = true;
            try { alignedToolbar.Padding = new Padding(padding.Left, padding.Top, right, padding.Bottom); }
            finally { aligningVolume = false; }
        }
        alignedToolbar.Layout += AlignVolumeGroup;
        alignedToolbar.ParentChanged += AlignVolumeGroup;
        alignedSeek.SizeChanged += AlignVolumeGroup;
        alignedSeek.LocationChanged += AlignVolumeGroup;
        alignedVolume.DpiChangedAfterParent += AlignVolumeGroup;
        split.Layout += AlignVolumeGroup;
        AlignVolumeGroup(null, EventArgs.Empty);

        editorPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
        subtitleEditor = new SubtitleTextEditor();
        subtitleInfoBar = new SubtitleInfoBar();
        editorHeader = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 20,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            Text = "Napisy.txt",
            TextAlign = ContentAlignment.MiddleLeft
        };
        var editorHeaderSeparator = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.Black
        };
        var subtitleInfoSeparator = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = Color.Black
        };
        AlignEditorHeader(editorHeader, subtitleEditor);
        subtitleInfoBar.SetEditorTextLeft(subtitleEditor.LineNumberMarginWidth);
        var headerForMarginAlignment = editorHeader;
        var editorForMarginAlignment = subtitleEditor;
        var infoBarForMarginAlignment = subtitleInfoBar;
        subtitleEditor.LineNumberMarginWidthChanged += (_, _) =>
        {
            AlignEditorHeader(headerForMarginAlignment, editorForMarginAlignment);
            infoBarForMarginAlignment.SetEditorTextLeft(editorForMarginAlignment.LineNumberMarginWidth);
        };
        editorPanel.Controls.Add(subtitleEditor);
        editorPanel.Controls.Add(subtitleInfoSeparator);
        editorPanel.Controls.Add(subtitleInfoBar);
        editorPanel.Controls.Add(editorHeaderSeparator);
        editorPanel.Controls.Add(editorHeader);
        split.Panel2.Controls.Add(editorPanel);

        return split;
    }

    internal static int VolumeGroupOpticalInset(int dpi) => Math.Max(1, (int)Math.Round(2 * dpi / 96d));

    private static VolumeTrackBar CreateVolumeSlider() => new()
    {
        Minimum = 0,
        Maximum = 100,
        Value = 50,
        SmallChange = 5,
        LargeChange = 5,
        AutoSize = false,
        Width = 82,
        Height = 18,
        Margin = Padding.Empty,
        BackColor = SystemColors.Control
    };

    private static ChapterSeekBar CreateSeekSlider() => new()
    {
        Dock = DockStyle.Bottom,
        Minimum = 0,
        Maximum = SeekSliderMaximum,
        Value = 0,
        AutoSize = false,
        Height = 22,
        Margin = Padding.Empty,
        BackColor = SystemColors.Control
    };

    private sealed class SpringToolStripSpacer : ToolStripLabel
    {
        public override Size GetPreferredSize(Size constrainingSize)
        {
            if (Owner is null)
            {
                return base.GetPreferredSize(constrainingSize);
            }

            var width = Owner.DisplayRectangle.Width;
            foreach (ToolStripItem item in Owner.Items)
            {
                if (item != this && item.Available)
                {
                    width -= item.Width + item.Margin.Horizontal;
                }
            }

            return new Size(Math.Max(0, width), base.GetPreferredSize(constrainingSize).Height);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private sealed class CleanSplitContainer : SplitContainer
    {
        private bool _draggingSplitter;
        private int _splitterGrabOffset;
        private ToolStrip? _transportToolbarRepaintTarget;
        private ChapterSeekBar? _seekSliderRepaintTarget;

        public CleanSplitContainer()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        protected override bool ShowFocusCues => false;

        public void SetTransportToolbarRepaintTarget(ToolStrip toolbar)
            => _transportToolbarRepaintTarget = toolbar;

        public void SetSeekSliderRepaintTarget(ChapterSeekBar seekSlider)
            => _seekSliderRepaintTarget = seekSlider;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && SplitterRectangle.Contains(e.Location))
            {
                _draggingSplitter = true;
                _splitterGrabOffset = Orientation == Orientation.Horizontal
                    ? e.Y - SplitterRectangle.Top
                    : e.X - SplitterRectangle.Left;
                Capture = true;
                return;
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_draggingSplitter)
            {
                SetSplitterDistanceFromMouse(e.Location);
                return;
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_draggingSplitter && e.Button == MouseButtons.Left)
            {
                SetSplitterDistanceFromMouse(e.Location);
                _draggingSplitter = false;
                Capture = false;
                return;
            }

            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture && _draggingSplitter)
            {
                _draggingSplitter = false;
            }

            base.OnMouseCaptureChanged(e);
        }

        private void SetSplitterDistanceFromMouse(Point location)
        {
            var position = (Orientation == Orientation.Horizontal ? location.Y : location.X) - _splitterGrabOffset;
            var length = Orientation == Orientation.Horizontal ? ClientSize.Height : ClientSize.Width;
            var maximum = length - Panel2MinSize - SplitterWidth;
            if (maximum < Panel1MinSize)
            {
                return;
            }

            var previousDistance = SplitterDistance;
            SplitterDistance = Math.Clamp(position, Panel1MinSize, maximum);
            if (SplitterDistance != previousDistance)
            {
                Panel2.Refresh();
                _transportToolbarRepaintTarget?.Refresh();
                _seekSliderRepaintTarget?.Refresh();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var rectangle = SplitterRectangle;
            using var background = new SolidBrush(SystemColors.Control);
            using var line = new Pen(SystemColors.ControlDark);
            e.Graphics.FillRectangle(background, rectangle);

            if (Orientation == Orientation.Horizontal)
            {
                var y = rectangle.Top + (rectangle.Height / 2);
                e.Graphics.DrawLine(line, rectangle.Left, y, rectangle.Right, y);
            }
            else
            {
                var x = rectangle.Left + (rectangle.Width / 2);
                e.Graphics.DrawLine(line, x, rectangle.Top, x, rectangle.Bottom);
            }
        }
    }

}
