using System.Drawing;
using System.Reflection;
using System.Text.Json;
using System.Windows.Forms;
using SubEdit.Player.Core.Configuration;
using SubEdit.Player.UI.DesignSystem;
using SubEdit.Player.WinForms;
using Xunit;
namespace SubEdit.Player.Tests;
[Collection("Subtitle dialog layout")]
public class OptionsIntegrationTests
{
    [Theory]
    [InlineData(0, 255)]
    [InlineData(25, 191)]
    [InlineData(50, 128)]
    [InlineData(75, 64)]
    [InlineData(100, 0)]
    public void BackgroundTransparency_PreviewsAndVideoUseSameAlpha(int transparency, int alpha) => SubtitleDialogLayoutTests.Sta(() =>
    {
        var s=NonDefault();
        s.InformationOsd.Position=InformationOsdPosition.TopRight;
        s.InformationOsd.BackgroundTransparencyPercent=transparency;
        s.SubtitleBackgroundTransparencyPercent=transparency;
        s.SubtitleBackgroundColorArgb=Color.Red.ToArgb();
        s.SubtitleTextColorArgb=Color.Lime.ToArgb();
        s.SubtitleOutlineColorArgb=Color.Blue.ToArgb();
        using var d=Dialog(s);d.Show();
        foreach(var tab in new[]{3,5})
        {
            d.LayoutView.Tabs.SelectedIndex=tab;SubtitleDialogLayoutTests.Pump();
            var sample=d.Samples.Single(v=>
            {
                for(Control? parent=v.Parent;parent is not null;parent=parent.Parent)
                    if(parent==d.LayoutView.Tabs.TabPages[tab])return true;
                return false;
            });
            using var bitmap=new Bitmap(sample.Width,sample.Height);sample.DrawToBitmap(bitmap,sample.ClientRectangle);
            var expectedRed=(int)Math.Round((255*alpha+48*(255-alpha))/255d);
            var expectedOther=(int)Math.Round(48*(255-alpha)/255d);
            var count=0;
            for(var y=0;y<bitmap.Height;y++)for(var x=0;x<bitmap.Width;x++)
            {
                var c=bitmap.GetPixel(x,y);
                if(Math.Abs(c.R-expectedRed)<=1&&Math.Abs(c.G-expectedOther)<=1&&Math.Abs(c.B-expectedOther)<=1)count++;
            }
            Assert.True(count>10,$"Preview {tab}, transparency {transparency}");
        }
        using var overlay=new VideoOverlayForm { ClientSize=new Size(640,360),TransparentSubtitles=false,
            SubtitleBackgroundTransparencyPercent=transparency,SubtitleBackgroundColor=Color.Red,
            SubtitleTextColor=Color.Lime,SubtitleOutlineColor=Color.Blue,
            SubtitleCue=new SubtitleCue(0,1000,[new SubtitleLine([new SubtitleTextRun("Sample",SubtitleTextStyle.None,null)])]) };
        using var actual=new Bitmap(640,360);
        using var g=Graphics.FromImage(actual);
        typeof(VideoOverlayForm).GetMethod("DrawSubtitle",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,[g]);
        var maxRedAlpha=0;
        for(var y=0;y<actual.Height;y++)for(var x=0;x<actual.Width;x++)
        {
            var c=actual.GetPixel(x,y);
            if(c.R>=254&&c.G==0&&c.B==0)maxRedAlpha=Math.Max(maxRedAlpha,c.A);
        }
        Assert.Equal(alpha,maxRedAlpha);
        d.Close();
    });

    private static EditorOptionsDialog Dialog(AppSettings s) => new(s.Editor,s.Editing,s.SubtitlePlayback,s.Playback,s.InformationOsd,
        s.SubtitleFontName,s.SubtitleFontSize,s.SubtitleFontBold,s.SubtitleFontSizeMode,s.MinimumSubtitleFontSize,s.SubtitleTextColorArgb,
        s.SubtitleBackgroundColorArgb,s.SubtitleBackgroundTransparencyPercent,s.SubtitleOutlineColorArgb,s.SubtitleOutlineThickness,s.SubtitleAlignment,
        s.SubtitleMarginLeft,s.SubtitleMarginRight,s.SubtitleMarginTop,s.SubtitleMarginBottom,s.PositionSubtitlesRelativeToVideoFrame,s.LastOptionsTabIndex);
    private static AppSettings Capture(EditorOptionsDialog d) => new()
    {
        Editor=d.CreateEditorSettings(),Editing=d.CreateEditingSettings(),SubtitlePlayback=d.CreateSubtitlePlaybackSettings(),InformationOsd=d.CreateInformationOsdSettings(),
        Playback=new(){UserSeekBackwardSeconds=d.SelectedUserSeekBackwardSeconds,UserSeekForwardSeconds=d.SelectedUserSeekForwardSeconds,PauseOnMinimize=d.SelectedPauseOnMinimize,
            PauseWhenDialogDisplayed=d.SelectedPauseWhenDialogDisplayed,PauseOnDeactivate=d.SelectedPauseOnDeactivate,ExitFullscreenWhenPlaybackEnds=d.SelectedExitFullscreenWhenPlaybackEnds,VideoDecodingMode=d.SelectedVideoDecodingMode},
        SubtitleFontName=d.SelectedSubtitleFontName,SubtitleFontSize=d.SelectedSubtitleFontSize,SubtitleFontBold=d.SelectedSubtitleFontBold,SubtitleFontSizeMode=d.SelectedSubtitleFontSizeMode,
        MinimumSubtitleFontSize=d.SelectedMinimumSubtitleFontSize,SubtitleTextColorArgb=d.SelectedSubtitleTextColor.ToArgb(),SubtitleBackgroundColorArgb=d.SelectedSubtitleBackgroundColor.ToArgb(),
        SubtitleBackgroundTransparencyPercent=d.SelectedSubtitleBackgroundTransparencyPercent,SubtitleOutlineColorArgb=d.SelectedSubtitleOutlineColor.ToArgb(),SubtitleOutlineThickness=d.SelectedSubtitleOutlineThickness,
        SubtitleAlignment=d.SelectedSubtitleAlignment,SubtitleMarginLeft=d.SelectedSubtitleMarginLeft,SubtitleMarginRight=d.SelectedSubtitleMarginRight,SubtitleMarginTop=d.SelectedSubtitleMarginTop,
        SubtitleMarginBottom=d.SelectedSubtitleMarginBottom,PositionSubtitlesRelativeToVideoFrame=d.SelectedPositionSubtitlesRelativeToVideoFrame,LastOptionsTabIndex=d.SelectedTabIndex
    };
    private static AppSettings NonDefault() => new()
    {
        Editor=new(){AutoSaveEnabled=false,AutoSaveMinutes=17,CreateBackupFile=false,BackupFileCount=8,FontName="Arial",FontSize=17,HighlightInvalidFormat=false,HighlightUnsortedLines=false,
            HighlightShortLines=false,HighlightVeryShortLines=false,MoveCaretToCurrentSubtitleOnEditorShow=true,MoveCaretToCurrentSubtitleOnFullScreenExit=true},
        Editing=new(){ReactionSeconds=12.34,ApplyReactionToEndFrame=true,ApplyReactionWhenMeasuringSubtitleDelay=true,PreserveSubtitleDurationWhenInsertingStartFrame=true,
            MoveCaretToNextLineAfterInsertingFrame=true,DefaultFiftyCharacterDisplaySeconds=45.67,AdditionalDefaultDisplaySeconds=89.12},
        SubtitlePlayback=new(){AdjustLineWidthToScreen=false,InterpretMicroDvdControlCodes=false,WarnOnUnsortedSubtitles=false,ItalicLinesStartingWithSlash=false,ItalicLinesStartingWithAsterisk=false,ItalicLinesStartingWithHash=false,MinimumGapFrames=13},
        Playback=new(){VideoDecodingMode=VideoDecodingMode.Software,UserSeekBackwardSeconds=127,UserSeekForwardSeconds=259,PauseOnMinimize=false,PauseWhenDialogDisplayed=true,PauseOnDeactivate=true,ExitFullscreenWhenPlaybackEnds=true},
        InformationOsd=new(){Position=InformationOsdPosition.Hidden,FontSize=23,DisplayDurationSeconds=12.3,ShowInWindowedMode=false,TextColorArgb=Color.Lime.ToArgb(),BackgroundColorArgb=Color.Red.ToArgb(),
            BackgroundTransparencyPercent=37,BorderColorArgb=Color.Blue.ToArgb(),BorderThickness=4},
        SubtitleFontName="Courier New",SubtitleFontSize=43,SubtitleFontBold=true,SubtitleFontSizeMode=SubtitleFontSizeMode.Constant,MinimumSubtitleFontSize=19,
        SubtitleTextColorArgb=Color.Yellow.ToArgb(),SubtitleBackgroundColorArgb=Color.Brown.ToArgb(),SubtitleBackgroundTransparencyPercent=42,SubtitleOutlineColorArgb=Color.Purple.ToArgb(),SubtitleOutlineThickness=5,
        SubtitleAlignment=SubtitleAlignment.TopRight,SubtitleMarginLeft=-13,SubtitleMarginRight=18,SubtitleMarginTop=23,SubtitleMarginBottom=-7,PositionSubtitlesRelativeToVideoFrame=true,LastOptionsTabIndex=4
    };
    [Theory] [InlineData(false)] [InlineData(true)]
    public void All58Settings_MapAndPersistWithoutChangingInput(bool customized) => SubtitleDialogLayoutTests.Sta(() =>
    {
        var original=customized?NonDefault():new AppSettings();var json=JsonSerializer.Serialize(original);
        using var d=Dialog(original);d.Show();SubtitleDialogLayoutTests.Pump();
        Assert.Equal(45,d.AdditionalControls.Count); // 45 + 12 Editor + 1 Video = 58.
        Assert.Equal(json,JsonSerializer.Serialize(Capture(d)));Assert.Equal(json,JsonSerializer.Serialize(original));
        var dir=Path.Combine(Path.GetTempPath(),"SubEdit-UI1-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try { var store=new PortableSettingsStore(dir);Assert.True(store.TrySave(Capture(d)));using var reopened=Dialog(store.Load());Assert.Equal(json,JsonSerializer.Serialize(Capture(reopened))); }
        finally { Directory.Delete(dir,true); }
        d.Close();
    });
    [Theory] [InlineData(9f)] [InlineData(10f)] [InlineData(12f)] [InlineData(15f)] [InlineData(18f)] [InlineData(24f)]
    public void SevenPages_FontRoundTripNavigationAndFallbackScroll(float points) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var d=Dialog(new());d.Show();d.SetUiFont(9);SubtitleDialogLayoutTests.Pump();var baseline=d.ClientSize;
        d.SetUiFont(points);SubtitleDialogLayoutTests.Pump();var stable=d.Size;
        Console.WriteLine($"Options {points} pt: ClientSize={d.ClientSize}, Outer={d.Size}");
        Assert.Equal(new[]{"Edytor","Edycja","Napisy","Wygląd napisów","Film","OSD","Wideo"},d.LayoutView.Tabs.TabPages.Cast<TabPage>().Select(p=>p.Text));
        for(var page=0;page<7;page++)
        {
            d.LayoutView.Tabs.SelectedIndex=page;SubtitleDialogLayoutTests.Pump();Assert.Equal(stable,d.Size);
            var entry=d.LayoutView.Pages[page];Assert.False(entry.Viewport.HorizontalScroll.Visible);
            if(points<=10) Assert.False(entry.Viewport.IsScrollVisible, $"Normal size unexpectedly scrolls on page {page}");
            var order=Ordered(entry.Page).ToArray();Assert.NotEmpty(order);order[0].Focus();
            for(var i=1;i<order.Length;i++){ Key(d,Keys.Tab); Assert.True(order[i].ContainsFocus,$"Page {page}: {order[i].AccessibleName ?? order[i].Text}"); }
            Key(d,Keys.Tab);Assert.True(d.LayoutView.Actions.Button(DialogActionRole.Apply).Focused);
            Key(d,Keys.Tab);Assert.True(d.LayoutView.Actions.Button(DialogActionRole.Ok).Focused);Key(d,Keys.Tab);Assert.True(d.LayoutView.Actions.Button(DialogActionRole.Cancel).Focused);
            Key(d,Keys.Shift|Keys.Tab);Key(d,Keys.Shift|Keys.Tab);Key(d,Keys.Shift|Keys.Tab);
            for(var i=order.Length-1;i>=0;i--){Assert.True(order[i].ContainsFocus);if(i>0)Key(d,Keys.Shift|Keys.Tab);}
            Assert.All(SubtitleDialogLayoutTests.Descendants(entry.Page).OfType<NumericUpDown>(), n=> {
                Assert.True(n.Width>=NativeControlLayout.NumericWidth(n)); Assert.Equal(n.PreferredHeight,n.Height);
                var before=n.Size;n.Value=n.Maximum;Assert.Equal(before,n.Size);
            });
        }
        d.SetUiFont(9);SubtitleDialogLayoutTests.Pump();Assert.Equal(baseline,d.ClientSize);
        d.LayoutView.Tabs.SelectedIndex=0;SubtitleDialogLayoutTests.Pump();d.ClientSize=new Size(d.ClientSize.Width,270);SubtitleDialogLayoutTests.Pump();
        Assert.True(d.LayoutView.Pages[0].Viewport.IsScrollVisible);
        d.Close();
    });
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ModalOpening_RealizesOnlyRequestedPagesAndKeepsFirstBounds(int selectedPage) => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var owner = new Form();
        owner.Show();
        using var dialog = Dialog(new AppSettings { LastOptionsTabIndex = selectedPage });
        var observations = new List<(int Page, Rectangle Bounds, int[] Handles)>();
        var correctOwnerAndDpi = false;
        dialog.VisibleChanged += (_, _) =>
        {
            if (!dialog.Visible) return;
            correctOwnerAndDpi = dialog.Modal && dialog.Owner == owner && dialog.DeviceDpi == owner.DeviceDpi;
            Observe();
        };
        dialog.Shown += (_, _) =>
        {
            try
            {
                foreach (var page in new[] { (selectedPage + 1) % 7, selectedPage })
                {
                    dialog.LayoutView.Tabs.SelectedIndex = page;
                    Observe();
                }
            }
            finally { dialog.DialogResult = DialogResult.Cancel; }
        };

        Assert.Equal(DialogResult.Cancel, dialog.ShowForOwner(owner));
        Assert.True(correctOwnerAndDpi);
        Assert.Equal(3, observations.Count);
        var first = observations[0];
        Assert.Equal(selectedPage, first.Page);
        Assert.True(Screen.FromControl(owner).WorkingArea.Contains(first.Bounds));
        Assert.Equal([selectedPage], CreatedPages(first.Handles));
        Assert.Equal(new[] { selectedPage, (selectedPage + 1) % 7 }.Order().ToArray(), CreatedPages(observations[1].Handles));
        Assert.Equal(observations[1].Handles, observations[2].Handles);
        Assert.All(observations, observation => Assert.Equal(first.Bounds, observation.Bounds));
        Assert.False(dialog.IsHandleCreated);

        void Observe() => observations.Add((dialog.SelectedTabIndex, dialog.Bounds,
            dialog.LayoutView.Tabs.TabPages.Cast<TabPage>()
                .Select(page => (page.IsHandleCreated ? 1 : 0)
                    + SubtitleDialogLayoutTests.Descendants(page).Count(control => control.IsHandleCreated)).ToArray()));

        static int[] CreatedPages(int[] handles) => Enumerable.Range(0, handles.Length).Where(i => handles[i] > 0).ToArray();
    });

    private static IEnumerable<Control> Ordered(Control parent)
    {
        foreach(var c in parent.Controls.Cast<Control>().OrderBy(c=>c.TabIndex))
        {
            if(c is RadioButton r&&!r.Checked)continue;
            if(c is UpDownBase){if(c.CanSelect&&c.TabStop&&c.Enabled&&c.Visible)yield return c;continue;}
            if(c is ButtonBase or TextBoxBase or ComboBox){if(c.CanSelect&&c.TabStop&&c.Enabled&&c.Visible)yield return c;}
            else foreach(var n in Ordered(c))yield return n;
        }
    }
    private static void Key(Form form,Keys key)
    {
        var focus=SubtitleDialogLayoutTests.Descendants(form).First(c=>c.Focused);
        Assert.True((bool)typeof(Control).GetMethod("ProcessDialogKey",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(focus,[key])!);
    }
    [Fact]
    public void EditedValuesAcrossAllPages_PersistAndReload() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var d=Dialog(new());d.Show();SubtitleDialogLayoutTests.Pump();
        d.AutoSave.Checked=false;d.Minutes.Value=16;d.Copies.Value=5;d.Software.Checked=true;
        foreach(var pair in d.AdditionalControls)
        {
            switch(pair.Value)
            {
                case NumericUpDown n: n.Value=n.Minimum+(n.Maximum-n.Minimum)/2;break;
                case CheckBox c: c.Checked=!c.Checked;break;
                case RadioButton radio:
                    var other=radio.Parent!.Controls.OfType<RadioButton>().Single(r=>r!=radio);other.Checked=!other.Checked;break;
                case ComboBox combo: combo.SelectedItem=combo.Text=="Arial"?"Courier New":"Arial";break;
                case SettingsColorButton color: color.SetColor(Color.FromArgb(255,17,93,151));break;
                case PositionChoice position: position.Controls.OfType<RadioButton>().First().Checked=true;break;
                default: throw new InvalidOperationException("Unverified binding: "+pair.Key);
            }
        }
        var snapshot=Capture(d);var json=JsonSerializer.Serialize(snapshot);
        for(var i=0;i<7;i++){d.LayoutView.Tabs.SelectedIndex=i;SubtitleDialogLayoutTests.Pump();}
        d.LayoutView.Tabs.SelectedIndex=snapshot.LastOptionsTabIndex;Assert.Equal(json,JsonSerializer.Serialize(Capture(d)));
        var dir=Path.Combine(Path.GetTempPath(),"SubEdit-Edited-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {var store=new PortableSettingsStore(dir);Assert.True(store.TrySave(snapshot));using var again=Dialog(store.Load());Assert.Equal(json,JsonSerializer.Serialize(Capture(again)));}
        finally {Directory.Delete(dir,true);}
        d.Close();
    });
    [Fact]
    public void ApplyCancelOk_KeepProductionEventSemanticsAndDependencies() => SubtitleDialogLayoutTests.Sta(() =>
    {
        var s=new AppSettings();using var d=Dialog(s);var applied=0;AppSettings? saved=null;
        d.ApplyRequested+=(_,_)=>{applied++;saved=Capture(d);};d.Show();SubtitleDialogLayoutTests.Pump();
        d.AutoSave.Checked=false;Assert.False(d.Minutes.Enabled);d.AutoSave.Checked=true;Assert.True(d.Minutes.Enabled);
        d.Minutes.Value=19;d.LayoutView.Actions.Button(DialogActionRole.Apply).PerformClick();Assert.Equal(1,applied);Assert.True(d.Visible);Assert.Equal(19,saved!.Editor.AutoSaveMinutes);
        d.Minutes.Value=27;d.LayoutView.Actions.Button(DialogActionRole.Cancel).PerformClick();Assert.Equal(1,applied);Assert.Equal(19,saved.Editor.AutoSaveMinutes);Assert.Equal(DialogResult.Cancel,d.DialogResult);d.Close();
        using var ok=Dialog(s);ok.ApplyRequested+=(_,_)=>applied++;ok.Show();SubtitleDialogLayoutTests.Pump();ok.LayoutView.Actions.Button(DialogActionRole.Ok).PerformClick();Assert.Equal(2,applied);Assert.Equal(DialogResult.OK,ok.DialogResult);ok.Close();
    });
    [Fact]
    public void SixNewLimitsAndPrecision_ClampOldSettingsIndividually() => SubtitleDialogLayoutTests.Sta(() =>
    {
        using var d=Dialog(new());
        foreach(var key in new[]{"ReactionSeconds","DefaultFiftyCharacterDisplaySeconds","AdditionalDefaultDisplaySeconds"})
        {
            var n=Assert.IsType<NumericUpDown>(d.AdditionalControls[key]);Assert.Equal(0,n.Minimum);Assert.Equal(99,n.Maximum);Assert.Equal(2,n.DecimalPlaces);Assert.Equal(.01m,n.Increment);
            n.Text="9999";typeof(NumericUpDown).GetMethod("ValidateEditText",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(n,null);Assert.Equal(99,n.Value);
            n.Text="-9999";typeof(NumericUpDown).GetMethod("ValidateEditText",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(n,null);Assert.Equal(0,n.Value);
            n.Value=98.99m;Assert.Equal(98.99m,n.Value);
        }
        foreach(var key in new[]{"UserSeekBackwardSeconds","UserSeekForwardSeconds"})
        {var n=Assert.IsType<NumericUpDown>(d.AdditionalControls[key]);Assert.Equal(1,n.Minimum);Assert.Equal(999,n.Maximum);}
        var osd=Assert.IsType<NumericUpDown>(d.AdditionalControls["OsdDuration"]);Assert.Equal(99,osd.Maximum);Assert.Equal(1,osd.DecimalPlaces);Assert.Equal(.1m,osd.Increment);
        var dir=Path.Combine(Path.GetTempPath(),"SubEdit-Limits-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {
            var store=new PortableSettingsStore(dir);var s=NonDefault();s.Editing.ReactionSeconds=120;s.Editing.DefaultFiftyCharacterDisplaySeconds=-4;s.Editing.AdditionalDefaultDisplaySeconds=9999;
            s.Playback.UserSeekBackwardSeconds=0;s.Playback.UserSeekForwardSeconds=2000;s.InformationOsd.DisplayDurationSeconds=9999;Assert.True(store.TrySave(s));
            var loaded=store.Load();Assert.Equal(99,loaded.Editing.ReactionSeconds);Assert.Equal(0,loaded.Editing.DefaultFiftyCharacterDisplaySeconds);Assert.Equal(99,loaded.Editing.AdditionalDefaultDisplaySeconds);
            Assert.Equal(1,loaded.Playback.UserSeekBackwardSeconds);Assert.Equal(999,loaded.Playback.UserSeekForwardSeconds);Assert.Equal(99,loaded.InformationOsd.DisplayDurationSeconds);
            Assert.Equal(s.SubtitleTextColorArgb,loaded.SubtitleTextColorArgb);Assert.Equal(s.Editor.AutoSaveMinutes,loaded.Editor.AutoSaveMinutes);
        } finally {Directory.Delete(dir,true);}
    });
    [Fact]
    public void OsdPreview_SeparatesRgbBackgroundOutlineAndTransparency() => SubtitleDialogLayoutTests.Sta(() =>
    {
        var s=NonDefault();s.InformationOsd.Position=InformationOsdPosition.TopRight;s.InformationOsd.BackgroundTransparencyPercent=50;
        using var d=Dialog(s);d.Show();d.LayoutView.Tabs.SelectedIndex=5;SubtitleDialogLayoutTests.Pump();var sample=d.Samples.Single(v=>v.AccessibleDescription?.StartsWith("OSD")==true);
        using var bitmap=new Bitmap(sample.Width,sample.Height);sample.DrawToBitmap(bitmap,sample.ClientRectangle);
        static int Count(Bitmap b,Func<Color,bool> match){var n=0;for(var x=0;x<b.Width;x++)for(var y=0;y<b.Height;y++)if(match(b.GetPixel(x,y)))n++;return n;}
        Assert.True(Count(bitmap,c=>c.B>220&&c.R<20&&c.G<20)>10); // Blue border is independent of background transparency.
        Assert.True(Count(bitmap,c=>c.R>120&&c.R<180&&c.G<35&&c.B<35)>10); // 50% red over the sample surface, without blue underneath.
        Assert.True(Count(bitmap,c=>c.G>220&&c.R<20&&c.B<20)>5); // Lime text.
        ((NumericUpDown)d.AdditionalControls["OsdTransparency"]).Value=100;SubtitleDialogLayoutTests.Pump();sample.DrawToBitmap(bitmap,sample.ClientRectangle);
        Assert.Equal(0,Count(bitmap,c=>c.R>120&&c.R<180&&c.G<35&&c.B<35));
        Assert.True(Count(bitmap,c=>c.B>220&&c.R<20&&c.G<20)>10);d.Close();
    });
}
