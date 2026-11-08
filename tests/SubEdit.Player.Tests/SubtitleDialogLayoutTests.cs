using System.Drawing;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using Xunit;
namespace SubEdit.Player.Tests;

[CollectionDefinition("Subtitle dialog layout", DisableParallelization = true)]
public sealed class SubtitleDialogLayoutCollection;
[Collection("Subtitle dialog layout")]
public class SubtitleDialogLayoutTests
{
    internal static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch(Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(100)), "UI test timed out");
        if(error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
    internal static void Pump() { for(var i=0;i<6;i++) Application.DoEvents(); }
    internal static IEnumerable<Control> Descendants(Control c)
    { foreach(Control child in c.Controls) { yield return child; foreach(var nested in Descendants(child)) yield return nested; } }
    [Theory]
    [InlineData(9f)] [InlineData(9.99f)] [InlineData(10.5f)] [InlineData(12f)] [InlineData(15f)] [InlineData(18f)] [InlineData(24f)]
    public void StandardDialogs_MeasuredTextActionsAndFontRoundTrip(float points) => Sta(() =>
    {
        Func<StandardDialog>[] create = [
            () => new QuestionDialog("Otwierany plik napisów jest w formacie SubRip (SRT). Czy przekonwertować napisy do formatu mDVD?", true, "Yes", "No"),
            () => new FindLongSubtitleLineDialog(45), () => new SubtitleFormatDialog(SubtitleTimingFormat.MicroDvd),
            () => new SubtitleConversionDialog(), () => new LongSubtitleLineSplitDialog(new()),
            () => new DefaultFrameRateDialog(23.976, 25), () => new SubtitleFrameRateDialog(25),
            () => new FindShortSubtitleDialog(new(),25), () => new FindSubtitleGapDialog(new(),25),
            () => new GoToSubtitleDialog(25), () => new GoToVideoPositionDialog(25,1000), () => new ItalicMarkerConversionDialog(),
            () => new SubtitleLineLimitDialog(new()), () => new UnknownSubtitleLineDialog(true),
            () => new SubtitleTimeShiftDialog(25, .5), () => new SubtitleCorrectionDialog(new()),
            () => new SubtitleDisplayTimeDialog(25,new()), () => new RapidSubtitleMergeDialog(new()),
            () => new MergeSubtitleSelectLineDialog(["Pierwsza linia", "Druga linia"],true),
            () => new GeometricSubtitleShiftDialog(new(0,"Początek",1000,1300),new(30,"Koniec",10000,10500),25,true,true),
            () => new SubEdit.Player.WinForms.Controls.SubtitleFindDialog("tekst"),
            () => new SubEdit.Player.WinForms.Controls.SubtitleReplaceDialog("tekst",true,true),
            () => new UnsortedSubtitlesWarningDialog(5)
        ];
        foreach(var factory in create)
        {
            using var d=factory(); d.Show(); d.SetUiFont(9); Pump(); var baseline=d.ClientSize;
            d.SetUiFont(points); Pump();
            Assert.True(d.MinimumSize.Width>0, d.GetType().Name);
            Assert.False(d.Shell.Viewport.HorizontalScroll.Visible);
            Assert.All(d.Shell.Actions.Buttons.Values,b => Assert.True(b.Height>=b.GetPreferredSize(Size.Empty).Height));
            Assert.All(Descendants(d.Page).OfType<Label>().Where(l=>l.Visible && l.Text.Length > 0), l =>
                Assert.True(l.Height>=l.GetPreferredSize(new Size(l.Width,0)).Height-1, $"{d.GetType().Name}: {l.Text} clipped {l.Bounds}, preferred {l.GetPreferredSize(new Size(l.Width,0))}, native {TextRenderer.MeasureText(l.Text,l.Font,new Size(l.Width,int.MaxValue),TextFormatFlags.WordBreak)}, font {l.Font.SizeInPoints}, parent {l.Parent!.Font.SizeInPoints}"));
            d.SetUiFont(9); Pump(); Assert.True(baseline == d.ClientSize, $"{d.GetType().Name} round-trip: {baseline} -> {d.ClientSize}"); d.Close();
        }
    });
    [Theory] [InlineData(false)] [InlineData(true)]
    public void Question_PreservesMessageAndResults(bool cancel) => Sta(() =>
    {
        const string text="Aktualny plik tekstowy jest zmodyfikowany.\r\n\r\nCzy zachować zmiany?";
        using var d=new QuestionDialog(text,cancel,"Tak","Nie"); d.Show(); Pump();
        Assert.Contains(Descendants(d.Page).OfType<Label>(), l=>l.Text==text);
        Assert.Equal(DialogResult.Yes,Assert.IsType<Button>(d.AcceptButton).DialogResult);
        Assert.Equal(cancel?DialogResult.Cancel:DialogResult.No,Assert.IsType<Button>(d.CancelButton).DialogResult);
        d.Close();
    });
    [Fact]
    public void LongLine_ValidationResultAndSplitDependenciesAreProduction() => Sta(() =>
    {
        using var d=new FindLongSubtitleLineDialog(41); d.Show(); Pump();
        var input=Assert.Single(Descendants(d.Page).OfType<TextBox>()); Assert.Equal("41",input.Text);
        input.Text="72"; Assert.IsType<Button>(d.AcceptButton).PerformClick(); Assert.Equal(72,d.MinimumLength); Assert.Equal(DialogResult.OK,d.DialogResult);d.Close();
        using var split=new LongSubtitleLineSplitDialog(new(){MaximumLineLength=52, MaximumLines=4, RemoveExistingSplitters=true, PreserveSplittersBeforeDialogues=true}); split.Show(); Pump();
        Assert.Equal(52,split.CreateSettings().MaximumLineLength); Assert.Equal(4,split.CreateSettings().MaximumLines);
        var remove=Descendants(split.Page).OfType<RadioButton>().Single(r=>r.AccessibleName=="&Usuń znaki podziału");remove.Checked=true;Pump();
        Assert.False(split.SplitLongLines);Assert.All(Descendants(split.Page).OfType<TextBox>(),t=>Assert.False(t.Enabled));split.Close();
    });
    [Theory] [InlineData(Keys.Enter)] [InlineData(Keys.Escape)]
    public void ModalDialogKeys_PreserveResultsAndOwner(Keys key) => Sta(() =>
    {
        using var owner=new Form();owner.Show();
        foreach(var make in new Func<StandardDialog>[] {()=>new QuestionDialog("Pytanie?",true),()=>new FindLongSubtitleLineDialog(41),()=>new SubtitleFormatDialog(SubtitleTimingFormat.Mpl2)})
        {
            using var d=make();d.Shown+=(_,_)=>d.BeginInvoke((Action)(()=>
            {
                Assert.True((bool)typeof(Control).GetMethod("ProcessDialogKey",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(d,[key])!);
            }));
            Assert.Equal(key==Keys.Escape?DialogResult.Cancel:d is QuestionDialog?DialogResult.Yes:DialogResult.OK,d.ShowDialog(owner));
            Assert.True(owner.Enabled);
        }
        owner.Close();
    });
    [Fact]
    public void FindReplace_PreserveEventsAndF3Shortcuts() => Sta(() =>
    {
        using var find=new SubEdit.Player.WinForms.Controls.SubtitleFindDialog("szukaj");var next=0;var previous=0;
        find.FindNextRequested+=(_,_)=>next++;find.FindPreviousRequested+=(_,_)=>previous++;find.Show();Pump();
        foreach(var key in new[]{Keys.F3,Keys.Shift|Keys.F3})
        {var message=new Message();Assert.True((bool)find.GetType().GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(find,[message,key])!);}
        Assert.Equal(1,next);Assert.Equal(1,previous);Assert.True(find.Visible);Assert.Equal("szukaj",find.Query);find.Close();
        using var replace=new SubEdit.Player.WinForms.Controls.SubtitleReplaceDialog("szukaj",true,true);var replaced=0;var all=0;
        replace.FindNextRequested+=(_,_)=>next++;replace.ReplaceRequested+=(_,_)=>replaced++;replace.ReplaceAllRequested+=(_,_)=>all++;
        replace.Show();Pump();replace.Shell.Actions.Button(DialogActionRole.Secondary).PerformClick();replace.Shell.Actions.Button(DialogActionRole.Apply).PerformClick();replace.Shell.Actions.Button(DialogActionRole.Yes).PerformClick();
        Assert.Equal(2,next);Assert.Equal(1,replaced);Assert.Equal(1,all);Assert.True(replace.Visible);Assert.True(replace.MatchCase&&replace.WholeWord);replace.Close();
    });
    [Fact]
    public void FitQueue_DoesNoFitDuringDragAndOneAfterRelease() => Sta(() =>
    {
        using var f=new Form();var surface=new CountingSurface();f.Controls.Add(surface);
        using var fit=new AdaptiveDialogLayout(f,surface,330);f.Show();Pump();var count=surface.Measures;
        typeof(Form).GetMethod("OnResizeBegin",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(f,[EventArgs.Empty]);
        using var font=new Font(f.Font.FontFamily,18);f.Font=font;fit.FitToContent();Pump();Assert.Equal(count,surface.Measures);
        typeof(Form).GetMethod("OnResizeEnd",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(f,[EventArgs.Empty]);Pump();Assert.Equal(count+1,surface.Measures);f.Close();
    });
    private sealed class CountingSurface : Panel,IAdaptiveDialogSurface
    {
        public int Measures;public Control View=>this;public UiDesignTokens Tokens=>UiDesignTokens.Current;public int MinimumClientHeight=>100;
        public int MeasurePreferredHeight(int width){Measures++;return 150;}public void ArrangeViewport(){}
    }
    [Fact]
    public void ProductionBindings_KeepValidationDependenciesAndToolTips() => Sta(() =>
    {
        using var correction=new SubtitleCorrectionDialog(new(){FixDialogueDashes=false});correction.Show();Pump();
        var check=Descendants(correction.Page).OfType<CheckBox>().Single(c=>c.AccessibleName=="Popraw pozycję znaku myślnika (dialogi)");
        var radios=Descendants(correction.Page).OfType<RadioButton>().Where(r=>r.AccessibleName!.StartsWith("|-" )).ToArray();
        Assert.All(radios,r=>Assert.False(r.Enabled));check.Checked=true;Assert.All(radios,r=>Assert.True(r.Enabled));
        Assert.True(correction.CreateSettings().FixDialogueDashes);
        var tips=(ToolTip)typeof(SubtitleCorrectionDialog).GetField("_toolTip",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(correction)!;
        foreach(var options in Descendants(correction.Page).OfType<NativeOptionList>())
            foreach(var b in options.Controls.OfType<ButtonBase>())
                if(!string.IsNullOrEmpty(tips.GetToolTip(b)))Assert.Equal(tips.GetToolTip(b),tips.GetToolTip(options.CaptionFor(b)));
        correction.Close();
        using var duration=new SubtitleDisplayTimeDialog(25,new(){BaseDurationSeconds=3,AdditionalDurationSeconds=1});duration.Show();Pump();
        Assert.Equal(3,duration.CreateSettings().BaseDurationSeconds);Assert.Equal(1,duration.CreateSettings().AdditionalDurationSeconds);
        var clear=Descendants(duration.Page).OfType<CheckBox>().Single(c=>c.AccessibleName=="Wpisz &puste pozycje");
        clear.Checked=true;Assert.False(Descendants(duration.Page).OfType<TextBox>().First().Enabled);clear.Checked=false;Assert.True(Descendants(duration.Page).OfType<TextBox>().First().Enabled);
        Assert.IsType<Button>(duration.AcceptButton).PerformClick();Assert.Equal(DialogResult.OK,duration.DialogResult);duration.Close();
        using var gap=new FindSubtitleGapDialog(new(){MinimumGapSeconds=1.25},24);gap.Show();Pump();Assert.IsType<Button>(gap.AcceptButton).PerformClick();Assert.Equal(1.25,gap.MinimumGapSeconds);gap.Close();
        using var shortLine=new FindShortSubtitleDialog(new(){MaximumDurationSeconds=2,AdjustForLineLength=false},25);shortLine.Show();Pump();Assert.IsType<Button>(shortLine.AcceptButton).PerformClick();Assert.Equal(2,shortLine.MaximumDurationSeconds);Assert.False(shortLine.AdjustForLineLength);shortLine.Close();
        using var geometric=new GeometricSubtitleShiftDialog(new(0,"Początek",1000,1300),new(20,"Koniec",10000,10500),25,true,false);geometric.Show();Pump();Assert.IsType<Button>(geometric.AcceptButton).PerformClick();Assert.Equal(.3,geometric.InitialDelaySeconds);Assert.Equal(.5,geometric.FinalDelaySeconds);Assert.True(geometric.ChangeFromInitialLine);Assert.False(geometric.ChangeToFinalLine);geometric.Close();
        using var warning=new UnsortedSubtitlesWarningDialog(17);warning.Show();
        for(var i=0;i<5;i++)typeof(UnsortedSubtitlesWarningDialog).GetMethod("AdvanceCountdown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(warning,null);
        Assert.Equal(DialogResult.OK,warning.DialogResult);warning.Close();
    });
}
