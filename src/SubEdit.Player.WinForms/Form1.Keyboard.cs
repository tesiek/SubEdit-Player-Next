using System.Windows.Forms;

namespace SubEdit.Player.WinForms;

public partial class Form1
{
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.O))
        {
            OpenVideo();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.O))
        {
            OpenSubtitleFile();
            return true;
        }

        if (keyData == (Keys.Control | Keys.N))
        {
            NewSubtitle();
            return true;
        }

        if (keyData == (Keys.Control | Keys.S))
        {
            SaveSubtitle();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.S))
        {
            SaveSubtitleAs();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.F4))
        {
            CloseVideoFile();
            return true;
        }

        if (keyData == (Keys.Control | Keys.F4))
        {
            CloseSubtitles();
            return true;
        }

        if (keyData == Keys.Insert)
        {
            InsertVideoPosition(insertStart: true);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Insert))
        {
            InsertVideoPosition(insertStart: false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Oemtilde))
        {
            ShowSubtitleFormatDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.T))
        {
            ShowSubtitleConversionDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.D))
        {
            ShowSubtitleTimeShiftDialog();
            return true;
        }

        if (keyData is (Keys.Control | Keys.OemQuestion) or (Keys.Control | Keys.Divide))
        {
            ShowMeasuredSubtitleDelayDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.F1))
        {
            MeasureSubtitleGeometryPoint(isInitial: true);
            return true;
        }

        if (keyData == (Keys.Control | Keys.F2))
        {
            MeasureSubtitleGeometryPoint(isInitial: false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.F3))
        {
            ShowGeometricSubtitleShiftDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.D))
        {
            ShowFindShortSubtitleDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.F))
        {
            ShowFindSubtitleGapDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.F))
        {
            FindUnsortedSubtitle();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.D))
        {
            ShowFindLongSubtitleLineDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.J))
        {
            ToggleSubtitleTrace();
            return true;
        }

        if (keyData == (Keys.Control | Keys.W))
        {
            ShowSubtitleDisplayTimeDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.G))
        {
            ShowGoToSubtitleDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.G))
        {
            ShowRapidSubtitleMergeDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.Y))
        {
            ShowLongSubtitleLineSplitDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.G))
        {
            ShowSubtitleLineLimitDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Q))
        {
            ShowSubtitleFrameRateDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.Q))
        {
            ShowDefaultFrameRateDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.R))
        {
            SortSubtitles();
            return true;
        }

        if (keyData == (Keys.Control | Keys.OemPeriod))
        {
            ShiftCurrentSubtitleByHalfSecond(0.5d);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Oemcomma))
        {
            ShiftCurrentSubtitleByHalfSecond(-0.5d);
            return true;
        }

        if (keyData == (Keys.Control | Keys.R))
        {
            ShowSubtitleCorrectionDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.F))
        {
            ShowFindDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.I))
        {
            ShowItalicMarkerConversionDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.I))
        {
            ToggleCurrentSubtitleLineItalic();
            return true;
        }

        if (keyData == (Keys.Control | Keys.B))
        {
            ToggleCurrentSubtitleLineBold();
            return true;
        }

        if (keyData == (Keys.Control | Keys.U))
        {
            ToggleCurrentSubtitleLineUnderline();
            return true;
        }

        if (keyData == (Keys.Control | Keys.H))
        {
            ShowReplaceDialog();
            return true;
        }

        if (keyData == (Keys.Shift | Keys.F3) && FindPreviousSubtitleText())
        {
            return true;
        }

        if (keyData == Keys.F3 && FindNextSubtitleText())
        {
            return true;
        }

        if (_subtitleEditor.Focused && keyData == Keys.Home)
        {
            MoveSubtitleCaretHome();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == Keys.End)
        {
            MoveSubtitleCaretEnd();
            return true;
        }

        if (keyData == Keys.M && (_isFullScreen || _splitContainer.Panel2Collapsed))
        {
            ToggleSubtitleTransparency();
            return true;
        }

        if (keyData == (Keys.Control | Keys.E))
        {
            ToggleEditor();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.N))
        {
            ToggleSubtitleVisibility();
            return true;
        }

        if (keyData == Keys.F6)
        {
            ToggleAlwaysOnTop();
            return true;
        }

        if (keyData == Keys.F11)
        {
            ToggleFullScreen();
            return true;
        }

        if (keyData == Keys.F8)
        {
            ShowVideoContextMenu();
            return true;
        }

        if (keyData == Keys.F12)
        {
            ShowEditorOptionsDialog();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Enter))
        {
            ToggleFullScreen();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Space)
        {
            TogglePlayback();
            return true;
        }

        if (keyData == (Keys.Control | Keys.L))
        {
            TogglePlayback();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.L))
        {
            JumpToSubtitleAtCurrentTime();
            return true;
        }

        if (keyData == (Keys.Control | Keys.K))
        {
            ShowGoToVideoPositionDialog();
            return true;
        }

        if (keyData == (Keys.Control | Keys.P))
        {
            if (!_splitContainer.Panel2Collapsed && _subtitleEditor.Focused)
            {
                PlayFromEditorLine();
            }

            return true;
        }

        if (keyData == (Keys.Control | Keys.M))
        {
            StopPlayback();
            return true;
        }

        if (keyData == (Keys.Control | Keys.OemOpenBrackets))
        {
            QueuePreciseSeekWithAccumulatedOsd(-5_000);
            return true;
        }

        if (keyData == (Keys.Control | Keys.OemCloseBrackets))
        {
            QueuePreciseSeekWithAccumulatedOsd(5_000);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.OemOpenBrackets))
        {
            SeekToBoundary(toEnd: false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Shift | Keys.OemCloseBrackets))
        {
            SeekToBoundary(toEnd: true);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Left) && CanUseFrameStepShortcut())
        {
            StepFrame(forward: false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Right) && CanUseFrameStepShortcut())
        {
            StepFrame(forward: true);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Left))
        {
            SeekUserBackward();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Right))
        {
            SeekUserForward();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Oemcomma))
        {
            PlayAdjacentVideo(-1);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.OemPeriod))
        {
            PlayAdjacentVideo(1);
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Down))
        {
            DecreasePlaybackSpeed();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.Up))
        {
            IncreasePlaybackSpeed();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.Up))
        {
            AdjustMovieElevation(5);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.Down))
        {
            AdjustMovieElevation(-5);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.Left))
        {
            AdjustMovieHorizontalOffset(-5);
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.Right))
        {
            AdjustMovieHorizontalOffset(5);
            return true;
        }

        if (keyData is (Keys.Control | Keys.Alt | Keys.Oemplus) or (Keys.Control | Keys.Alt | Keys.Add))
        {
            AdjustMovieZoom(10);
            return true;
        }

        if (keyData is (Keys.Control | Keys.Alt | Keys.OemMinus) or (Keys.Control | Keys.Alt | Keys.Subtract))
        {
            AdjustMovieZoom(-10);
            return true;
        }

        if (keyData is (Keys.Control | Keys.Alt | Keys.D0) or (Keys.Control | Keys.Alt | Keys.NumPad0))
        {
            ResetMovieTransformations();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Left)
        {
            QueuePreciseSeekWithAccumulatedOsd(-5_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Right)
        {
            QueuePreciseSeekWithAccumulatedOsd(5_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Up)
        {
            QueuePreciseSeekWithAccumulatedOsd(60_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Down)
        {
            QueuePreciseSeekWithAccumulatedOsd(-60_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Home)
        {
            SeekToBoundary(toEnd: false);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.End)
        {
            SeekToBoundary(toEnd: true);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == (Keys.Shift | Keys.Oemcomma))
        {
            StepFrame(forward: false);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == (Keys.Shift | Keys.OemPeriod))
        {
            StepFrame(forward: true);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.R)
        {
            ToggleLoopPlaybackFromShortcut();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.D)
        {
            ToggleDirectoryPlaybackFromShortcut();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.PageUp)
        {
            AdjustVolume(1);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.PageDown)
        {
            AdjustVolume(-1);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == (Keys.Control | Keys.PageUp))
        {
            AdjustSubtitleFontSize(1);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == (Keys.Control | Keys.PageDown))
        {
            AdjustSubtitleFontSize(-1);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Back)
        {
            ToggleMute();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Escape && _isFullScreen)
        {
            ToggleFullScreen();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.NumPad1)
        {
            QueuePreciseSeekWithAccumulatedOsd(-5_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.NumPad2)
        {
            TogglePlayback();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.NumPad3)
        {
            QueuePreciseSeekWithAccumulatedOsd(5_000);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.NumPad4)
        {
            StepFrame(forward: false);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.NumPad6)
        {
            StepFrame(forward: true);
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.Add)
        {
            PlayFromEditorLine();
            return true;
        }

        if (CanUsePlayerOnlyShortcut() && keyData == Keys.T)
        {
            ToggleTimeCounterVisibility();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Alt | Keys.OemPeriod))
        {
            ToggleDirectoryPlaybackFromShortcut();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Oem5))
        {
            ToggleMinimizeWindow();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.A))
        {
            _subtitleEditor.SelectAll();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.C))
        {
            _subtitleEditor.Copy();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.X))
        {
            _subtitleEditor.Cut();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.V))
        {
            _subtitleEditor.Paste();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.Z))
        {
            UndoSubtitleEdit();
            return true;
        }

        if (_subtitleEditor.Focused && keyData is (Keys.Control | Keys.Y) or (Keys.Control | Keys.Shift | Keys.Z))
        {
            RedoSubtitleEdit();
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.Home))
        {
            _subtitleEditor.ExecuteCmd(ScintillaNET.Command.DocumentStart);
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.End))
        {
            _subtitleEditor.ExecuteCmd(ScintillaNET.Command.DocumentEnd);
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.PageUp))
        {
            _subtitleEditor.ExecuteCmd(ScintillaNET.Command.StutteredPageUp);
            return true;
        }

        if (_subtitleEditor.Focused && keyData == (Keys.Control | Keys.PageDown))
        {
            _subtitleEditor.ExecuteCmd(ScintillaNET.Command.StutteredPageDown);
            return true;
        }

        if (_subtitleEditor.Focused && IsScintillaControlNavigationShortcut(keyData))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        var modifiers = keyData & Keys.Modifiers;
        if (_subtitleEditor.Focused && modifiers is Keys.Control or (Keys.Control | Keys.Shift))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
