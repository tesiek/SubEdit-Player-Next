using System.Drawing;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using SubEdit.Player.WinForms.Controls;
using Xunit;
namespace SubEdit.Player.Tests;
[Collection("Subtitle dialog layout")]
public class SearchDialogQaTests
{
    [Fact]
    public void Replace_IsPositionedBeforeShowAndDoesNotJumpAtFirstVisibility() => SubtitleDialogLayoutTests.Sta(() =>
    {
        foreach (var screen in Screen.AllScreens)
        foreach (var offset in new[] { new Point(40,50), new Point(180,120) })
        {
            var area=screen.WorkingArea;
            using var owner=new Form { StartPosition=FormStartPosition.Manual,
                Bounds=new Rectangle(area.Left+offset.X,area.Top+offset.Y,Math.Min(950,area.Width-offset.X),Math.Min(650,area.Height-offset.Y)) };
            owner.Show();
            using var d=new SubtitleReplaceDialog("test",false,false);
            d.PrepareForShow(owner);
            Assert.False(d.Visible);Assert.Equal(FormStartPosition.Manual,d.StartPosition);
            var prepared=d.Bounds;Rectangle? first=null;
            d.VisibleChanged+=(_,_)=>{if(d.Visible&&first is null)first=d.Bounds;};
            d.Show(owner);SubtitleDialogLayoutTests.Pump();
            Assert.NotNull(first);Assert.Equal(prepared,first!.Value);Assert.Equal(first.Value,d.Bounds);
            Assert.True(area.Contains(d.Bounds));Assert.Same(owner,d.Owner);Assert.False(d.Modal);Assert.True(owner.Enabled);
            Assert.Equal(Math.Clamp(owner.Left+(owner.Width-d.Width)/2,area.Left,Math.Max(area.Left,area.Right-d.Width)),d.Left);
            Assert.Equal(Math.Clamp(owner.Top+(owner.Height-d.Height)/2,area.Top,Math.Max(area.Top,area.Bottom-d.Height)),d.Top);
            d.Close();owner.Close();
        }
    });
    [Theory] [InlineData(9f)] [InlineData(12f)] [InlineData(24f)]
    public void SearchActions_AreCompactAndReplaceIsCenteredModeless(float font) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner=new Form { StartPosition=FormStartPosition.Manual, Bounds=new Rectangle(260,180,1000,750) };owner.Show();
        foreach(var d in new StandardDialog[]{new SubtitleFindDialog("test"),new SubtitleReplaceDialog("test",true,false)})
        using(d)
        {
            d.Show(owner);SubtitleDialogLayoutTests.Pump();
            if(d is SubtitleReplaceDialog)
            {
                Assert.Same(owner,d.Owner);Assert.False(d.Modal);Assert.True(owner.Enabled);
                Assert.InRange(Math.Abs(d.Left+(d.Width/2)-(owner.Left+owner.Width/2)),0,1);
                Assert.InRange(Math.Abs(d.Top+(d.Height/2)-(owner.Top+owner.Height/2)),0,1);
            }
            d.SetUiFont(font);SubtitleDialogLayoutTests.Pump();
            foreach(var b in d.Shell.Actions.Buttons.Values)
            {
                var text=TextRenderer.MeasureText(b.Text,b.Font,Size.Empty,TextFormatFlags.SingleLine);
                Assert.InRange(b.Height,text.Height,Math.Max(25*d.DeviceDpi/96,text.Height+10*d.DeviceDpi/96));
                Assert.InRange(b.Width,text.Width+12*d.DeviceDpi/96,Math.Max(75*d.DeviceDpi/96,text.Width+12*d.DeviceDpi/96));
                Assert.True(b.TabStop,"TabStop");Assert.True(d.Shell.Actions.RectangleToScreen(d.Shell.Actions.ClientRectangle).Contains(b.RectangleToScreen(b.ClientRectangle)), $"{d.GetType().Name}/{font}/{b.Text}: bar {d.Shell.Actions.RectangleToScreen(d.Shell.Actions.ClientRectangle)} button {b.RectangleToScreen(b.ClientRectangle)}");
            }
            Assert.NotNull(d.AcceptButton);Assert.NotNull(d.CancelButton);d.Close();
        }
        owner.Close();
    });
    [Theory] [InlineData(9f)] [InlineData(12f)] [InlineData(24f)]
    public void ReplaceFields_FillSameRightEdge(float points) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var d=new SubtitleReplaceDialog("szukaj",false,false);d.Show();d.SetUiFont(points);SubtitleDialogLayoutTests.Pump();
        var fields=SubtitleDialogLayoutTests.Descendants(d.Page).OfType<TextBox>().ToArray();Assert.Equal(2,fields.Length);
        Assert.Equal(fields[0].Left,fields[1].Left);Assert.Equal(fields[0].Width,fields[1].Width);
        foreach(var field in fields)
        {
            var parent=Assert.IsType<TableLayoutPanel>(field.Parent);
            Assert.InRange(parent.ClientSize.Width-field.Right,0,parent.Padding.Right+1);
        }
        fields[0].Text="nowe";fields[1].Text="zamiennik";Assert.Equal("nowe",d.Query);Assert.Equal("zamiennik",d.Replacement);d.Close();
    });
    [Fact]
    public void RapidMerge_ValueAndUnitRemainOneUsableBlock() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var d=new RapidSubtitleMergeDialog(new(){MaximumCharacters=110});d.Show();SubtitleDialogLayoutTests.Pump();
        var section=SubtitleDialogLayoutTests.Descendants(d.Page).OfType<GroupBox>().Single(g=>g.Text=="Długość napisu");
        var field=Assert.Single(SubtitleDialogLayoutTests.Descendants(section).OfType<FieldValueLayout>());
        var input=Assert.IsType<TextBox>(field.Editor);Assert.Equal("110",input.Text);Assert.Equal("znaków.",field.Description.Text);
        Assert.True(input.Width>=TextRenderer.MeasureText("110",input.Font).Width+4);
        Assert.True(input.Right<field.Description.Left);Assert.True(field.ClientRectangle.Contains(input.Bounds));
        var check=Assert.Single(SubtitleDialogLayoutTests.Descendants(section).OfType<CheckBox>());check.Checked=false;Assert.False(input.Enabled);check.Checked=true;Assert.True(input.Enabled);
        input.Text="125";Assert.IsType<Button>(d.AcceptButton).PerformClick();Assert.Equal(DialogResult.OK,d.DialogResult);Assert.Equal(125,d.CreateSettings().MaximumCharacters);d.Close();
    });
}
