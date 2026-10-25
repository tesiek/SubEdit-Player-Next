using System;
using System.Threading;
using ScintillaNET;
using Xunit;

namespace SubEdit.Player.Tests;

public class ScintillaLineCollectionTests
{
    private static void RunInSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (ex != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    private static void AssertScintillaInvariants(Scintilla sc)
    {
        string text = sc.Text;
        Assert.Equal(text.Length, sc.TextLength);

        int expectedPosition = 0;
        for (int i = 0; i < sc.Lines.Count; i++)
        {
            Line line = sc.Lines[i];
            Assert.Equal(expectedPosition, line.Position);
            Assert.Equal(line.Text.Length, line.Length);
            expectedPosition += line.Length;

            // Invariant: SetStyling within line bounds must never throw ArgumentOutOfRangeException
            sc.StartStyling(line.Position);
            sc.SetStyling(line.Length, 1);
        }
    }

    [Theory]
    [InlineData("X")]          // 1-byte ASCII
    [InlineData("ż")]          // 2-byte UTF-8
    [InlineData("€")]          // 3-byte UTF-8
    [InlineData("😀")]         // 4-byte UTF-8
    public void LineCollection_SingleLineEditsAndUndoRedo_MaintainsInvariants(string unicodeChar)
    {
        RunInSta(() =>
        {
            using var sc = new Scintilla();
            _ = sc.Handle; // Ensure native window is created

            sc.Text = "Line1\r\nLine2\r\nLine3\r\n";
            sc.EmptyUndoBuffer();
            AssertScintillaInvariants(sc);

            // Insert character at position 2 in line 0
            sc.InsertText(2, unicodeChar);
            AssertScintillaInvariants(sc);

            // Undo insertion
            sc.Undo();
            AssertScintillaInvariants(sc);

            // Redo insertion
            sc.Redo();
            AssertScintillaInvariants(sc);

            // Delete inserted character
            sc.DeleteRange(2, unicodeChar.Length);
            AssertScintillaInvariants(sc);

            // Undo deletion
            sc.Undo();
            AssertScintillaInvariants(sc);
        });
    }

    [Theory]
    [InlineData("X")]          // 1-byte ASCII
    [InlineData("ż")]          // 2-byte UTF-8
    [InlineData("€")]          // 3-byte UTF-8
    [InlineData("😀")]         // 4-byte UTF-8
    public void LineCollection_MultiSelectionRectangularPasteSimulation_MaintainsInvariants(string unicodeChar)
    {
        RunInSta(() =>
        {
            using var sc = new Scintilla();
            _ = sc.Handle;

            sc.MultipleSelection = true;
            sc.MouseSelectionRectangularSwitch = true;
            sc.Text = "AAAA\r\nBBBB\r\nCCCC\r\n";
            sc.EmptyUndoBuffer();
            AssertScintillaInvariants(sc);

            // Setup multi-selection at column 2 across 3 lines
            int line0Pos = sc.Lines[0].Position + 2;
            int line1Pos = sc.Lines[1].Position + 2;
            int line2Pos = sc.Lines[2].Position + 2;

            sc.SelectionStart = line0Pos;
            sc.SelectionEnd = line0Pos;
            sc.AddSelection(line1Pos, line1Pos);
            sc.AddSelection(line2Pos, line2Pos);

            // Multi-insert across all selections
            sc.ReplaceSelection(unicodeChar);
            AssertScintillaInvariants(sc);

            // Rectangular Undo
            sc.Undo();
            AssertScintillaInvariants(sc);
            Assert.Equal("AAAA\r\nBBBB\r\nCCCC\r\n", sc.Text);

            // Rectangular Redo
            sc.Redo();
            AssertScintillaInvariants(sc);

            // Second Undo to return to baseline
            sc.Undo();
            AssertScintillaInvariants(sc);
            Assert.Equal("AAAA\r\nBBBB\r\nCCCC\r\n", sc.Text);
        });
    }

    [Fact]
    public void LineCollection_PolishDiacriticsBurstUndo_DoesNotCorruptLengthOrCrashSetStyling()
    {
        RunInSta(() =>
        {
            using var sc = new Scintilla();
            _ = sc.Handle;

            sc.MultipleSelection = true;
            // 5 subtitle lines
            sc.Text = "00:01:00,000 --> 00:01:04,000\r\n" +
                      "00:01:05,000 --> 00:01:08,000\r\n" +
                      "00:01:09,000 --> 00:01:12,000\r\n" +
                      "00:01:13,000 --> 00:01:16,000\r\n" +
                      "00:01:17,000 --> 00:01:20,000\r\n";
            sc.EmptyUndoBuffer();
            AssertScintillaInvariants(sc);

            // Simulate column insert of mixed Polish diacritics "ąćęłńóśźż"
            const string payload = "żółć";
            for (int i = 0; i < 5; i++)
            {
                int pos = sc.Lines[i].Position + 12;
                if (i == 0)
                {
                    sc.SelectionStart = pos;
                    sc.SelectionEnd = pos;
                }
                else
                {
                    sc.AddSelection(pos, pos);
                }
            }

            sc.ReplaceSelection(payload);
            AssertScintillaInvariants(sc);

            // Undo - this triggered the bug in unpatched Scintilla5.NET 7.0.0
            sc.Undo();
            AssertScintillaInvariants(sc);

            // Redo
            sc.Redo();
            AssertScintillaInvariants(sc);

            // Undo again
            sc.Undo();
            AssertScintillaInvariants(sc);
        });
    }

    [Fact]
    public void SubtitleTextEditor_ColumnPasteAndUndo_DoesNotCrashHighlightMarkup()
    {
        RunInSta(() =>
        {
            using var editor = new SubEdit.Player.WinForms.Controls.SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "00:01:00,000 --> 00:01:04,000\r\n" +
                          "00:01:05,000 --> 00:01:08,000\r\n" +
                          "00:01:09,000 --> 00:01:12,000\r\n" +
                          "00:01:13,000 --> 00:01:16,000\r\n";
            editor.EmptyUndoBuffer();

            // Setup multi-selection across all lines at column 10
            for (int i = 0; i < 4; i++)
            {
                int pos = editor.Lines[i].Position + 10;
                if (i == 0)
                {
                    editor.SelectionStart = pos;
                    editor.SelectionEnd = pos;
                }
                else
                {
                    editor.AddSelection(pos, pos);
                }
            }

            // Column paste with multi-byte Polish characters
            editor.ReplaceSelection("żółć");
            System.Windows.Forms.Application.DoEvents();

            // Undo column paste
            editor.Undo();
            System.Windows.Forms.Application.DoEvents();

            // Verify invariant: editor TextLength must equal Text.Length
            Assert.Equal(editor.Text.Length, editor.TextLength);

            // Redo and Undo again
            editor.Redo();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(editor.Text.Length, editor.TextLength);

            editor.Undo();
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(editor.Text.Length, editor.TextLength);
        });
    }
}
