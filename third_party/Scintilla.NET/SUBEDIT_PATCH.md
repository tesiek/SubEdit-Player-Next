# Scintilla.NET Vendor Patch for SubEdit-Player Next

## Upstream Baseline
- **Repository:** https://github.com/desjarlais/Scintilla.NET
- **Version:** `7.0.0`
- **Commit:** `6ce2f2db095a9e5f9b0c3d4601f8df4af51c117c` (tag `v7.0.0`)
- **License:** MIT (Jacob Slusser, VPKSoft, desjarlais, Ahmet Sait)

## Reason for Local Patch
In upstream `Scintilla5.NET 7.0.0`, `LineCollection.cs` tracks line lengths using character counts deduced from `SCNotification` payload or incremental deltas.
During operations involving multi-byte UTF-8 characters under certain modifications (notably rectangular / column paste, undo, and redo of column edits), the native Scintilla core raises `SCN_MODIFIED` notifications in byte-by-byte or fragmented chunks (e.g. trailing continuation bytes first, or partial multibyte fragments).

Attempting to decode `scn.text` with `Encoding.GetCharCount` or using naive incremental deltas in `TrackDeleteText()` and `TrackInsertText()` causes:
1. Malformed UTF-8 byte sequences being counted as replacement characters (``, 1 char instead of valid codepoint/byte length),
2. Permanent desynchronization of `LineCollection.TextLength`, `Lines[i].Position`, and `Lines[i].Length` against native document positions (`SCI_GETTEXTLENGTH`),
3. Subsequent operations such as `SetStyling` throwing `ArgumentOutOfRangeException` because ScintillaNET validates against its out-of-sync `TextLength`.

## Modified Methods
In `Scintilla.NET/LineCollection.cs`:
- `TrackDeleteText(SciApi.SCNotification scn)`:
  Instead of branching on `linesAdded == 0` and decoding partial bytes from `scn.text`, the modified line's UTF-16 character length is directly remeasured from native Scintilla via `SCI_POSITIONFROMLINE`, `SCI_LINELENGTH`, and `SCI_COUNTCODEUNITS`:
  ```csharp
  int startLine = this.scintilla.DirectMessage(SciApi.SCI_LINEFROMPOSITION, scn.position).ToInt32();
  int lineByteStart = this.scintilla.DirectMessage(SciApi.SCI_POSITIONFROMLINE, new IntPtr(startLine)).ToInt32();
  int lineByteLength = this.scintilla.DirectMessage(SciApi.SCI_LINELENGTH, new IntPtr(startLine)).ToInt32();
  AdjustLineLength(startLine, GetCharCount(lineByteStart, lineByteLength) - CharLineLength(startLine));
  ```
- `TrackInsertText(SciApi.SCNotification scn)`:
  Symmetrically remeasures the target line directly from native Scintilla so that 3-byte and 4-byte UTF-8 sequences (e.g. emoji, special symbols) in column operations do not cause drift:
  ```csharp
  int startLine = this.scintilla.DirectMessage(SciApi.SCI_LINEFROMPOSITION, scn.position).ToInt32();
  int lineByteStart = this.scintilla.DirectMessage(SciApi.SCI_POSITIONFROMLINE, new IntPtr(startLine)).ToInt32();
  int lineByteLength = this.scintilla.DirectMessage(SciApi.SCI_LINELENGTH, new IntPtr(startLine)).ToInt32();
  AdjustLineLength(startLine, GetCharCount(lineByteStart, lineByteLength) - CharLineLength(startLine));
  ```
- Removed unused private helper `GetCharCount(IntPtr text, int length, Encoding encoding)` and unused `using System.Text;`.

## How to Verify Upstream Fix in Future Releases
To verify if a newer upstream release of Scintilla.NET fixes this defect:
1. Test rectangular paste of multi-byte UTF-8 characters (e.g. Polish diacritics `ąćęłńóśźż`, CJK characters, or emoji `😀`).
2. Perform Undo and Redo on the rectangular paste.
3. Check invariant: `scintilla.Text.Length == scintilla.TextLength` and `scintilla.Lines[i].Length == native line length`.
4. Ensure `scintilla.SetStyling(length, style)` does not throw `ArgumentOutOfRangeException`.
