using System.Drawing.Drawing2D;
using SubEdit.Player.UI.DesignSystem;
namespace SubEdit.Player.WinForms;

internal sealed class SettingsColorButton : Button
{
    internal Color Value { get; private set; }
    internal event EventHandler? Changed;
    internal SettingsColorButton(string caption,Color initial)
    {
        Text=caption;NativeControlLayout.ConfigureButton(this);SetColor(initial);
        Click+=(_,_)=>{using var picker=new ColorDialog { Color=Value,FullOpen=true };if(picker.ShowDialog(FindForm())==DialogResult.OK)SetColor(picker.Color);};
    }
    internal void SetColor(Color color)
    {
        Value=color;var old=Image;var side=UiDesignTokens.Pixels(16,DeviceDpi);var bitmap=new Bitmap(side,side);
        using(var g=Graphics.FromImage(bitmap))g.Clear(color);
        Image=bitmap;old?.Dispose();ImageAlign=ContentAlignment.MiddleLeft;TextImageRelation=TextImageRelation.Overlay;
        AccessibleDescription=$"ARGB {color.ToArgb():X8}";Changed?.Invoke(this,EventArgs.Empty);Invalidate();
    }
    public override Size GetPreferredSize(Size proposedSize) => new(TextRenderer.MeasureText(Text,OptionsMeasureContext.FontOf(this)).Width+UiDesignTokens.Pixels(48,OptionsMeasureContext.Dpi(this)),
        NativeControlLayout.MeasureButtonHeight(this,int.MaxValue/4));
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);SetColor(Value);}
    protected override void Dispose(bool disposing){if(disposing){Image?.Dispose();Image=null;}base.Dispose(disposing);}
}
internal sealed class PositionChoice : UserControl
{
    internal event EventHandler? Changed;
    internal int Selection => Array.FindIndex(_buttons,b=>b.Checked);
    private readonly RadioButton[] _buttons;
    internal PositionChoice(int selected)
    {
        string[] names=["Góra, lewo","Góra, środek","Góra, prawo","Środek, lewo","Środek","Środek, prawo","Dół, lewo","Dół, środek","Dół, prawo"];
        _buttons=names.Select((n,i)=>new RadioButton { AccessibleName=n,Checked=i==selected,TabIndex=i }).ToArray();
        Controls.AddRange(_buttons);foreach(var button in _buttons)button.CheckedChanged+=(_,_)=>Changed?.Invoke(this,EventArgs.Empty);
    }
    public override Size GetPreferredSize(Size proposedSize) => new((OptionsMeasureContext.FontOf(this).Height+12)*3,(OptionsMeasureContext.FontOf(this).Height+12)*3);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(_buttons is null)return;
        var cell=Font.Height+12;for(var i=0;i<9;i++)_buttons[i].Bounds=new Rectangle(i%3*cell,i/3*cell,cell,cell);
    }
}
internal sealed class SettingsAppearanceSample(Func<IReadOnlyDictionary<string,object>> values,bool osd) : Control
{
    internal static float ResolvePoints(float selected,bool relative,float minimum,float referenceHeight=768f) => relative?Math.Max(minimum,(float)Math.Floor(referenceHeight*selected/768f+.5f)):selected;
    internal static float EmPixels(float points,float dpi)=>points*dpi/72f;
    internal float EffectivePoints
    {
        get { var state=values();return osd?Convert.ToSingle(state["OsdFontSize"]):ResolvePoints(Convert.ToSingle(state["SubtitleFontSize"]),(bool)state["SubtitleRelative"],Convert.ToSingle(state["MinimumSubtitleFontSize"])); }
    }
    public override Size GetPreferredSize(Size proposedSize)=>new(UiDesignTokens.Pixels(320,OptionsMeasureContext.Dpi(this)),UiDesignTokens.Pixels(105,OptionsMeasureContext.Dpi(this)));
    protected override void OnPaint(PaintEventArgs e)
    {

        base.OnPaint(e);var state=values();string p=osd?"Osd":"Subtitle";
        decimal N(string key,decimal fallback=0)=>state.TryGetValue(key,out var value)?Convert.ToDecimal(value):fallback;
        bool B(string key)=>state.TryGetValue(key,out var value)&&value is true;
        Color C(string key)=>Color.FromArgb((int)state[key]);
        var g=e.Graphics;g.Clear(Color.FromArgb(48,48,48));g.SmoothingMode=SmoothingMode.AntiAlias;
        if(osd&&B("OsdHidden"))return;
        var rect=ClientRectangle;rect.Inflate(-8,-8);
        if(!osd&&B("PositionSubtitlesRelativeToVideoFrame")){rect.Inflate(-10,-8);using var frame=new Pen(Color.Gray);g.DrawRectangle(frame,rect);}
        var alpha=OsdAppearanceRenderer.GetAlpha((int)N(p+"Transparency"));
        var fontName=osd?"Segoe UI":(string)state["SubtitleFontName"];
        var size=EffectivePoints;
        AccessibleDescription=osd?$"OSD {size} pt, 1:1 przy bieżącym DPI":$"{size} pt, próbka 1:1; tryb zależny: okno referencyjne wysokości 768 px";
        using var font=new Font(fontName,size,!osd&&B("SubtitleFontBold")?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point);
        var text=osd?"OSD 12:34":"Text... 123...";
        using var path=new GraphicsPath();path.AddString(text,font.FontFamily,(int)font.Style,EmPixels(font.SizeInPoints,g.DpiY),Point.Empty,StringFormat.GenericTypographic);
        var bounds=path.GetBounds();var index=osd?2:Convert.ToInt32(N("SubtitleAlignment",7));
        var x=index%3==0?rect.Left:index%3==1?rect.Left+(rect.Width-bounds.Width)/2:rect.Right-bounds.Width;
        var y=index/3==0?rect.Top:index/3==1?rect.Top+(rect.Height-bounds.Height)/2:rect.Bottom-bounds.Height;
        if(!osd){x+=(float)(N("SubtitleMarginLeft")-N("SubtitleMarginRight"));y+=(float)(N("SubtitleMarginTop")-N("SubtitleMarginBottom"));}
        using var transform=new Matrix();transform.Translate(x-bounds.Left,y-bounds.Top);path.Transform(transform);
        using var background=new SolidBrush(Color.FromArgb(alpha,C(p+"BackgroundColor")));
        var backdrop=osd?RectangleF.Inflate(path.GetBounds(),4,2):rect;g.FillRectangle(background,backdrop);
        var thickness=(float)N(p+"Outline")*DeviceDpi/96f;
        if(thickness>0){using var pen=new Pen(C(p+"OutlineColor"),thickness){LineJoin=LineJoin.Round};if(osd)g.DrawRectangle(pen,backdrop.X,backdrop.Y,backdrop.Width,backdrop.Height);else g.DrawPath(pen,path);}
        using var foreground=new SolidBrush(C(p+"TextColor"));g.FillPath(foreground,path);
    }
}
