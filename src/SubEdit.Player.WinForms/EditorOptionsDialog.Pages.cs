using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;

internal sealed partial class EditorOptionsDialog
{
    internal Dictionary<string,Control> AdditionalControls { get; } = [];
    private readonly Dictionary<string,Func<object>> _readAdditional = [];
    internal List<SettingsAppearanceSample> Samples { get; } = [];
    private IReadOnlyDictionary<string,object> SnapshotAdditional() => new System.Collections.ObjectModel.ReadOnlyDictionary<string,object>(_readAdditional.ToDictionary(p=>p.Key,p=>p.Value()));
    private T Initial<T>(string key,T fallback) => _initial.TryGetValue(key,out var value) ? (T)Convert.ChangeType(value, typeof(T)) : fallback;
    private void Bind(string key,Control control,Func<object> read)
    {
        AdditionalControls.Add(key,control); _readAdditional.Add(key,read);
        if(control is NumericUpDown n)n.ValueChanged+=(_,_)=>RefreshSamples();
        if(control is CheckBox c)c.CheckedChanged+=(_,_)=>RefreshSamples();
        if(control is RadioButton radio)radio.CheckedChanged+=(_,_)=>RefreshSamples();
        if(control is ComboBox combo)combo.SelectedIndexChanged+=(_,_)=>RefreshSamples();
    }
    private void RefreshSamples(){foreach(var sample in Samples)sample.Invalidate();}
    private NumericUpDown Numeric(string key,decimal value,decimal min,decimal max,int decimals=0)
    {
        var n=new NumericUpDown { Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals==0?1m:decimals==1?.1m:.01m,
            Value=Math.Clamp(Initial(key,value), min, max),TextAlign=HorizontalAlignment.Right };
        Bind(key,n,()=>n.Value);return n;
    }
    private CheckBox Check(SettingsPageLayout.SettingsSection section,string key,string caption,bool value)
    {
        var check=section.AddCheckBox(caption,Initial(key,value));Bind(key,check,()=>check.Checked);return check;
    }
    private SettingsPageLayout EditingPage()
    {
        var page=new SettingsPageLayout();var reaction=page.AddSection("Korekta czasu reakcji");
        // Long explanation can wrap; the compact numeric value stays a coherent field.
        var reactionRows=SettingsComposition.IndependentRows();reaction.AddComposition(reactionRows);
        reactionRows.AddField("Korekta czasu reakcji [sek.]:",Numeric("ReactionSeconds",.25m,0,99,2));
        Check(reaction,"ApplyReactionToEndFrame","Zastosuj dla \"Wstaw czas końcowy napisu\"",false);
        Check(reaction,"ApplyReactionWhenMeasuringSubtitleDelay","Zastosuj przy badaniu opóźnienia napisów względem filmu",false);
        var insertion=page.AddSection("Wstawianie klatek napisów");
        Check(insertion,"PreserveSubtitleDurationWhenInsertingStartFrame","Przy wstawianiu klatki początkowej zachowaj poprzednią różnicę między klatkami",false);
        Check(insertion,"MoveCaretToNextLineAfterInsertingFrame","Po wstawieniu klatki napisu przenieś kursor do następnego wiersza",false);
        var duration=page.AddSection("Domyślny czas wyświetlania napisu");var row=duration.AddAdaptiveRow();
        row.AddField("Czas na 50 znaków napisu:",Numeric("DefaultFiftyCharacterDisplaySeconds",3,0,99,2));
        row.AddField("plus dodatkowo [sek.]:",Numeric("AdditionalDefaultDisplaySeconds",2,0,99,2));return page;
    }
    private SettingsPageLayout SubtitlesPage()
    {
        var page=new SettingsPageLayout();var display=page.AddSection("Wyświetlanie napisów");
        Check(display,"AdjustLineWidthToScreen","Dostosuj szerokość wierszy napisów, jeżeli wychodzą poza ekran",true);
        var formatting=page.AddSection("Formatowanie i znaczniki");
        Check(formatting,"InterpretMicroDvdControlCodes","Interpretuj w napisach kody kontrolne mDVDp (np. {y:i}{c:$FA0530})",true);
        var validation=page.AddSection("Kontrola poprawności");
        Check(validation,"WarnOnUnsortedSubtitles","Podczas oglądania filmu informuj o nieposortowanych napisach",true);
        var row=formatting.AddAdaptiveRow();
        row.AddControl(new Label { Text="Wyświetlaj w kursywie wiersze zaczynające się od:",AutoSize=true,UseMnemonic=false });
        foreach(var (key,caption) in new[]{("ItalicLinesStartingWithSlash","/"),("ItalicLinesStartingWithAsterisk","*"),("ItalicLinesStartingWithHash","#")})
        { var check=row.AddCheckBox(caption,Initial(key,true));Bind(key,check,()=>check.Checked); }
        var gapRows=SettingsComposition.IndependentRows();display.AddComposition(gapRows);
        gapRows.AddField("Minimalny odstęp między wyświetlanymi napisami (1-20) [w klatkach]:",Numeric("MinimumGapFrames",2,1,20));return page;
    }
    private SettingsPageLayout MoviePage()
    {
        var page=new SettingsPageLayout();var seek=page.AddSection("Własne kroki przewijania");
        var row=SettingsComposition.IndependentRows();seek.AddComposition(row);
        row.AddField("Przewinięcie do tyłu [sek.]:",Numeric("UserSeekBackwardSeconds",3,1,999));
        row.AddField("Przewinięcie do przodu [sek.]:",Numeric("UserSeekForwardSeconds",3,1,999));
        var pause=page.AddSection("Automatyczna pauza");
        Check(pause,"PauseOnMinimize","Wstrzymaj film, gdy okno aplikacji jest zminimalizowane",true);
        Check(pause,"PauseWhenDialogDisplayed","Wstrzymaj film, gdy wyświetlane jest okno dialogowe",false);
        Check(pause,"PauseOnDeactivate","Wstrzymaj film, gdy okno aplikacji nie jest aktywne",false);
        Check(page.AddSection("Tryb pełnoekranowy"),"ExitFullscreenWhenPlaybackEnds","Po zakończeniu odtwarzania wyjdź z trybu pełnoekranowego",false);return page;
    }
    private ComboBox FontChoice(string key,string initial)
    {
        var combo=new OptionsFontCombo();
        combo.Items.AddRange(SystemFontCatalog.GetFamilyNames().Append(Initial(key,initial)).Distinct().Cast<object>().ToArray());
        combo.SelectedItem=Initial(key,initial);Bind(key,combo,()=>combo.Text);return combo;
    }
    private SettingsPageLayout AppearancePage(bool osd)
    {
        var page=new SettingsPageLayout();string prefix=osd?"Osd":"Subtitle";
        if(!osd)
        {
            var font=page.AddSection("Czcionka napisów");var row=font.AddAdaptiveRow();
            var subtitleFont=FontChoice("SubtitleFontName","Arial");var subtitleColumn=row.AddField("Nazwa:",subtitleFont,220);
            row.AddField("Wielkość (8-72):",Numeric("SubtitleFontSize",30,8,72));
            var bold=font.AddEditorAlignedCheckBox(subtitleColumn,"Pogrubiona",Initial("SubtitleFontBold",false));Bind("SubtitleFontBold",bold,()=>bold.Checked);
            var mode=page.AddSection("Rozmiar czcionki napisów");
            var relative=Initial("SubtitleRelative",true);
            mode.AddRadioOption("Stała wielkość",isChecked:!relative);
            var rel=mode.AddRadioOption("Zależny od wielkości okna",isChecked:relative);Bind("SubtitleRelative",rel,()=>rel.Checked);
            var minimum=Numeric("MinimumSubtitleFontSize",8,2,50);
            var minimumRows=SettingsComposition.IndependentRows();mode.AddComposition(minimumRows);
            var label=minimumRows.AddField("Minimalny rozmiar czcionki (2-50):",minimum);
            NativeOptionList.BindEnabled(minimum,rel);NativeOptionList.BindEnabled(label,rel);
        }
        else
        {
            var position=page.AddSection("Wyświetlanie OSD");
            var hidden=Initial("OsdHidden",false);
            position.AddRadioOption("Prawy górny róg okna",isChecked:!hidden);
            var off=position.AddRadioOption("Nie wyświetlaj",isChecked:hidden);Bind("OsdHidden",off,()=>off.Checked);
            var row=SettingsComposition.IndependentRows(equalValueWidths:true);position.AddComposition(row);
            row.AddField("Wielkość czcionki OSD (10-30):",Numeric("OsdFontSize",18,10,30));
            row.AddField("Czas wyświetlania OSD [sek.]",Numeric("OsdDuration",1.5m,0,99,1));
            var window=Check(position,"OsdWindowed","Wyświetlaj również w trybie okienkowym",true);
            off.CheckedChanged+=(_,_)=>window.Enabled=!off.Checked;window.Enabled=!off.Checked;
        }
        var colors=page.AddSection(osd?"Kolory i obrys OSD":"Kolory i obrys");
        var colorSettings=SettingsComposition.IndependentRows();
        colorSettings.AddWide(ColorButton(prefix+"TextColor","Kolor napisu",osd?Color.Silver:Color.White));
        colorSettings.AddWide(ColorButton(prefix+"BackgroundColor",osd?"Kolor tła":"Kolor tła napisu",osd?Color.FromArgb(32,32,32):Color.Black));
        colorSettings.AddField("Przezroczystość tła:",Numeric(prefix+"Transparency",osd?50:25,0,100));
        colorSettings.AddWide(ColorButton(prefix+"OutlineColor","Kolor obrysu",osd?Color.FromArgb(50,50,50):Color.Black));
        colorSettings.AddField("Grubość obrysu:",Numeric(prefix+"Outline",osd?1:2,0,100));
        var sample=new SettingsAppearanceSample(()=>SnapshotAdditional(),osd) { TabStop=false };
        Samples.Add(sample);colors.AddComposition(SettingsComposition.SettingsWithPreview(colorSettings,sample));
        if(!osd)
        {
            var placement=page.AddSection("Położenie napisów i marginesy");
            var picker=new PositionChoice(Initial("SubtitleAlignment",7));Bind("SubtitleAlignment",picker,()=>picker.Selection);
            var first=new AdaptiveSettingsRow();first.AddField("Lewo",Numeric("SubtitleMarginLeft",0,-10000,10000));first.AddField("Prawo",Numeric("SubtitleMarginRight",0,-10000,10000));
            var second=new AdaptiveSettingsRow();second.AddField("Góra",Numeric("SubtitleMarginTop",0,-10000,10000));second.AddField("Dół",Numeric("SubtitleMarginBottom",5,-10000,10000));
            var margins=SettingsComposition.RelatedFieldsRows(first,second);
            placement.AddComposition(SettingsComposition.SettingsWithPreview(picker,margins));
            picker.Changed+=(_,_)=>RefreshSamples();
            Check(placement,"PositionSubtitlesRelativeToVideoFrame","Pozycja napisów względem klatki obrazu",false);
        }
        return page;
    }
    private SettingsColorButton ColorButton(string key,string caption,Color initial)
    {
        var button=new SettingsColorButton(caption,Color.FromArgb(Initial(key,initial.ToArgb())));
        Bind(key,button,()=>button.Value.ToArgb());button.Changed+=(_,_)=>RefreshSamples();return button;
    }
}
