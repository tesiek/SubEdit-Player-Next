using System;
using System.Threading;
using SubEdit.Player.WinForms.Controls;
using Xunit;

namespace SubEdit.Player.Tests;

public class SubtitleEditorLifecycleTests
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

    [Fact]
    public void Clear_WithEmptyUndoBuffer_PreventsGhostDocumentRestorationOnUndoOrRedo()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "{100}{200}Pierwszy napis\r\n{300}{400}Drugi napis";
            editor.EmptyUndoBuffer();

            // Simulate user editing the active document
            editor.AppendText("\r\n{500}{600}Trzeci napis");
            Assert.True(editor.CanUndo);

            // Lifecycle boundary: clear text and empty undo buffer in production sequence
            editor.Text = string.Empty;
            editor.EmptyUndoBuffer();

            Assert.Equal(string.Empty, editor.Text);
            Assert.False(editor.CanUndo);
            Assert.False(editor.CanRedo);

            // Pressing Undo (Ctrl+Z) must not resurrect the closed document
            editor.Undo();
            Assert.Equal(string.Empty, editor.Text);
            Assert.False(editor.CanUndo);

            // Pressing Redo (Ctrl+Y) must not resurrect anything
            editor.Redo();
            Assert.Equal(string.Empty, editor.Text);
            Assert.False(editor.CanRedo);
        });
    }

    [Fact]
    public void Clear_SequenceOrderSensitivity_ProvesEmptyUndoBufferMustFollowTextClear()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "{100}{200}Dokument testowy";
            editor.AppendText("\r\n{300}{400}Edycja w dokumencie");
            Assert.True(editor.CanUndo);

            // 1. WRONG ORDER: EmptyUndoBuffer before setting Text = string.Empty
            // Scintilla records setting Text to empty as a new undoable action.
            editor.EmptyUndoBuffer();
            editor.Text = string.Empty;

            Assert.True(editor.CanUndo, "Wrong order leaves setting Text=empty as an undoable action in Scintilla");
            editor.Undo();
            Assert.Equal("{100}{200}Dokument testowy\r\n{300}{400}Edycja w dokumencie", editor.Text);

            // 2. CORRECT ORDER (Production fix): Text = string.Empty followed by EmptyUndoBuffer
            editor.Text = string.Empty;
            editor.EmptyUndoBuffer();

            Assert.False(editor.CanUndo, "Correct order purges the undo buffer completely");
            Assert.False(editor.CanRedo);
            editor.Undo();
            Assert.Equal(string.Empty, editor.Text);
        });
    }

    [Fact]
    public void ApplyFullDocumentEdit_WhenReadOnly_UpdatesTextAndPreservesReadOnly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "OLD";
            editor.ReadOnly = true;

            editor.ApplyFullDocumentEdit("NEW", new SubtitleEditorViewState(0, 0, 0));

            Assert.Equal("NEW", editor.Text);
            Assert.True(editor.ReadOnly);
        });
    }

    [Fact]
    public void ApplyFullDocumentEdit_WhenNotReadOnly_UpdatesTextAndPreservesReadOnly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "OLD";
            editor.ReadOnly = false;

            editor.ApplyFullDocumentEdit("NEW", new SubtitleEditorViewState(0, 0, 0));

            Assert.Equal("NEW", editor.Text);
            Assert.False(editor.ReadOnly);
        });
    }

    [Fact]
    public void ApplyFullDocumentEdit_WhenReadOnly_ProducesSingleAtomicUndoStep()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "{100}{200}Pierwsza linia\r\n{300}{400}Druga linia";
            editor.EmptyUndoBuffer();
            editor.ReadOnly = true;

            editor.ApplyFullDocumentEdit(
                "{1000}{1100}Przesunięta linia 1\r\n{1200}{1300}Przesunięta linia 2",
                new SubtitleEditorViewState(0, 0, 0));

            Assert.Equal("{1000}{1100}Przesunięta linia 1\r\n{1200}{1300}Przesunięta linia 2", editor.Text);
            Assert.True(editor.ReadOnly);

            // Native Scintilla reports CanUndo = false while ReadOnly = true
            Assert.False(editor.CanUndo);

            // Under writable mutation, CanUndo reports true because the undo buffer has records
            editor.ExecuteWritableMutation(() => Assert.True(editor.CanUndo));

            // Scintilla native blocks Undo while ReadOnly is true
            editor.Undo();
            Assert.Equal("{1000}{1100}Przesunięta linia 1\r\n{1200}{1300}Przesunięta linia 2", editor.Text);

            // ExecuteWritableMutation allows undoing while preserving ReadOnly state
            editor.ExecuteWritableMutation(() => editor.Undo());
            Assert.Equal("{100}{200}Pierwsza linia\r\n{300}{400}Druga linia", editor.Text);
            Assert.True(editor.ReadOnly);
            editor.ExecuteWritableMutation(() => Assert.False(editor.CanUndo, "Full document edit should be reverted in exactly one single undo step"));
        });
    }

    [Fact]
    public void ExecuteWritableMutation_WhenExceptionThrown_RestoresReadOnly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.ReadOnly = true;

            Assert.Throws<InvalidOperationException>(() =>
            {
                editor.ExecuteWritableMutation(() =>
                {
                    Assert.False(editor.ReadOnly);
                    throw new InvalidOperationException("Simulation error");
                });
            });

            Assert.True(editor.ReadOnly, "ReadOnly must be restored even if an exception occurs");
        });
    }

    [Fact]
    public void ExecuteWritableMutation_WhenNested_CorrectlyRestoresReadOnly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.ReadOnly = true;

            editor.ExecuteWritableMutation(() =>
            {
                Assert.False(editor.ReadOnly);
                editor.ExecuteWritableMutation(() =>
                {
                    Assert.False(editor.ReadOnly);
                });
                Assert.False(editor.ReadOnly);
            });

            Assert.True(editor.ReadOnly);
        });
    }

    [Fact]
    public void ExecuteWritableMutation_WhenNotReadOnly_LeavesReadOnlyFalse()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.ReadOnly = false;

            editor.ExecuteWritableMutation(() =>
            {
                Assert.False(editor.ReadOnly);
            });

            Assert.False(editor.ReadOnly);
        });
    }

    [Fact]
    public void ReplaceSelection_UnderExecuteWritableMutation_WhenReadOnly_UpdatesTextAndPreservesReadOnly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "Hello World";
            editor.SetSelection(6, 11); // "World"
            editor.ReadOnly = true;

            editor.ExecuteWritableMutation(() => editor.ReplaceSelection("SubEdit"));

            Assert.Equal("Hello SubEdit", editor.Text);
            Assert.True(editor.ReadOnly);
        });
    }

    [Fact]
    public void ClearTraceSelection_CollapsesSelectionToEmptyCaret()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "{100}{200}Pierwsza linia\r\n{300}{400}Druga linia";
            editor.SetTraceSelection(0, 26);
            Assert.NotEqual(editor.SelectionStart, editor.SelectionEnd);

            editor.ClearTraceSelection();
            Assert.Equal(editor.SelectionStart, editor.SelectionEnd);
        });
    }

    [Fact]
    public void SavePoint_EventsAndModifiedProperty_FollowScintillaSemantics()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "A";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();

            var savePointLeftCount = 0;
            var savePointReachedCount = 0;
            editor.SavePointLeft += (_, _) => savePointLeftCount++;
            editor.SavePointReached += (_, _) => savePointReachedCount++;

            Assert.False(editor.Modified);
            Assert.Equal(0, savePointLeftCount);
            Assert.Equal(0, savePointReachedCount);

            // Step 1: Type / append text
            editor.AppendText("B");
            Assert.True(editor.Modified);
            Assert.Equal(1, savePointLeftCount);
            Assert.Equal(0, savePointReachedCount);

            // Step 2: Undo back to initial save point
            editor.Undo();
            Assert.Equal("A", editor.Text);
            Assert.False(editor.Modified);
            Assert.Equal(1, savePointLeftCount);
            Assert.Equal(1, savePointReachedCount);

            // Step 3: Redo back away from save point
            editor.Redo();
            Assert.Equal("AB", editor.Text);
            Assert.True(editor.Modified);
            Assert.Equal(2, savePointLeftCount);
            Assert.Equal(1, savePointReachedCount);

            // Step 4: Save at "AB" (was modified, so SetSavePoint causes SavePointReached)
            editor.SetSavePoint();
            Assert.False(editor.Modified);
            Assert.Equal(2, savePointLeftCount);
            Assert.Equal(2, savePointReachedCount);

            // Step 5: Undo to "A" (away from new save point)
            editor.Undo();
            Assert.Equal("A", editor.Text);
            Assert.True(editor.Modified);
            Assert.Equal(3, savePointLeftCount);
            Assert.Equal(2, savePointReachedCount);

            // Step 6: Redo to "AB" (back to save point)
            editor.Redo();
            Assert.Equal("AB", editor.Text);
            Assert.False(editor.Modified);
            Assert.Equal(3, savePointLeftCount);
            Assert.Equal(3, savePointReachedCount);
        });
    }

    [Fact]
    public void SavePoint_WithApplyFullDocumentEdit_AllowsUndoToCleanAndRedoToDirty()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "ORIGINAL TEXT";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();

            Assert.False(editor.Modified);

            // Programmatic full document edit (e.g., Format / Merge / Shift)
            editor.ApplyFullDocumentEdit("MUTATED TEXT", new SubtitleEditorViewState(0, 0, 0));

            Assert.Equal("MUTATED TEXT", editor.Text);
            Assert.True(editor.Modified);

            // Undo back to original save point
            editor.Undo();
            Assert.Equal("ORIGINAL TEXT", editor.Text);
            Assert.False(editor.Modified);

            // Redo back to mutated text
            editor.Redo();
            Assert.Equal("MUTATED TEXT", editor.Text);
            Assert.True(editor.Modified);
        });
    }

    [Fact]
    public void SavePoint_MultiStepEdits_ReturnsToCleanOnlyAtExactSavePoint()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "STEP0";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();

            Assert.False(editor.Modified);

            editor.ApplyFullDocumentEdit("STEP0 -> STEP1", new SubtitleEditorViewState(0, 0, 0));
            Assert.True(editor.Modified);

            editor.ApplyFullDocumentEdit("STEP0 -> STEP1 -> STEP2", new SubtitleEditorViewState(0, 0, 0));
            Assert.True(editor.Modified);

            // Undo first step -> should still be modified (STEP1)
            editor.Undo();
            Assert.Equal("STEP0 -> STEP1", editor.Text);
            Assert.True(editor.Modified);

            // Undo second step -> back to STEP0 (SavePoint)
            editor.Undo();
            Assert.Equal("STEP0", editor.Text);
            Assert.False(editor.Modified);

            // Redo first step -> STEP1 (Modified again)
            editor.Redo();
            Assert.Equal("STEP0 -> STEP1", editor.Text);
            Assert.True(editor.Modified);

            // Redo second step -> STEP2 (Still modified)
            editor.Redo();
            Assert.Equal("STEP0 -> STEP1 -> STEP2", editor.Text);
            Assert.True(editor.Modified);
        });
    }

    [Fact]
    public void DirtyState_NonTextStateAndTextCombinations_BehaveCorrectly()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "A";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();

            var textDirty = editor.Modified;
            var stateDirty = false;
            bool CalculateDocumentDirty() => textDirty || stateDirty;

            Assert.False(CalculateDocumentDirty());

            // Case 1: Non-text state becomes dirty (e.g., encoding changed)
            stateDirty = true;
            Assert.True(CalculateDocumentDirty());

            // Case 2: Text also modified
            editor.AppendText("B");
            textDirty = editor.Modified;
            Assert.True(textDirty);
            Assert.True(CalculateDocumentDirty());

            // Case 3: Text undone to save point, but stateDirty remains true
            editor.Undo();
            textDirty = editor.Modified;
            Assert.False(textDirty);
            Assert.True(stateDirty);
            Assert.True(CalculateDocumentDirty()); // Still dirty because stateDirty == true!

            // Case 4: Successful save clears stateDirty and resets SavePoint
            editor.SetSavePoint();
            textDirty = editor.Modified;
            stateDirty = false;
            Assert.False(textDirty);
            Assert.False(stateDirty);
            Assert.False(CalculateDocumentDirty()); // Now clean!
        });
    }

    [Fact]
    public void SavePoint_SetInMiddleOfUndoHistory_TracksCorrectSavePosition()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "A";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();

            editor.ApplyFullDocumentEdit("B", new SubtitleEditorViewState(0, 0, 0));
            editor.ApplyFullDocumentEdit("C", new SubtitleEditorViewState(0, 0, 0));

            // Undo to B and establish new save point at B
            editor.Undo();
            Assert.Equal("B", editor.Text);
            editor.SetSavePoint();
            Assert.False(editor.Modified);

            // Undo to A -> away from new save point -> modified
            editor.Undo();
            Assert.Equal("A", editor.Text);
            Assert.True(editor.Modified);

            // Redo to B -> at new save point -> not modified
            editor.Redo();
            Assert.Equal("B", editor.Text);
            Assert.False(editor.Modified);

            // Redo to C -> away from new save point -> modified
            editor.Redo();
            Assert.Equal("C", editor.Text);
            Assert.True(editor.Modified);

            // Undo back to B -> at new save point -> not modified
            editor.Undo();
            Assert.Equal("B", editor.Text);
            Assert.False(editor.Modified);
        });
    }

    [Fact]
    public void SavePoint_WithReadOnlyTraceMode_TogglesWritableMutationAndTracksDirtyAccurately()
    {
        RunInSta(() =>
        {
            using var editor = new SubtitleTextEditor();
            _ = editor.Handle;

            editor.Text = "ORIGINAL";
            editor.EmptyUndoBuffer();
            editor.SetSavePoint();
            editor.ReadOnly = true;

            Assert.False(editor.Modified);
            Assert.True(editor.ReadOnly);

            // Programmatic mutation during Trace/ReadOnly mode
            editor.ExecuteWritableMutation(() =>
            {
                editor.ApplyFullDocumentEdit("MUTATED", new SubtitleEditorViewState(0, 0, 0));
            });

            Assert.Equal("MUTATED", editor.Text);
            Assert.True(editor.Modified);
            Assert.True(editor.ReadOnly);

            // Undo during Trace/ReadOnly mode
            editor.ExecuteWritableMutation(() =>
            {
                editor.Undo();
            });

            Assert.Equal("ORIGINAL", editor.Text);
            Assert.False(editor.Modified);
            Assert.True(editor.ReadOnly);
        });
    }
}
