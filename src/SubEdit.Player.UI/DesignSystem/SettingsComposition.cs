using System.ComponentModel;
namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Explicit semantic roles, rendered by one table per composition.</summary>
public sealed class SettingsComposition : UserControl, IDialogContent
{
    private readonly TableLayoutPanel _grid = new() { Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=Padding.Empty,ColumnCount=2 };
    private readonly List<(Label? Label,Control Value)> _rows=[];
    private readonly bool _columns;
    private readonly bool _equalValueWidths;
    private bool _arranging;
    private bool? _lastStacked;
    public event EventHandler? MetricsChanged;
    [Browsable(false)] public Control View=>this;
    private readonly Dictionary<(string,Font,int,Size,TextFormatFlags),Size> _textCache=[];
    private Size MeasuredText(string text,Font font,Size proposed=default,TextFormatFlags flags=TextFormatFlags.Default)
    {
        var key=(text,font,OptionsMeasureContext.Dpi(this),proposed,flags);
        if(_textCache.TryGetValue(key,out var size))return size;
        if(_textCache.Count>=256)_textCache.Clear();
        return _textCache[key]=TextRenderer.MeasureText(text,font,proposed,flags);
    }
    private int Gap=>UiDesignTokens.Pixels(UiDesignTokens.Current.FieldGap,OptionsMeasureContext.Dpi(this));
    public static SettingsComposition IndependentRows(bool equalValueWidths=false)=>new(false,equalValueWidths);
    public static SettingsComposition RelatedFieldsRows(params AdaptiveSettingsRow[] rows)
    {
        var result=IndependentRows();
        var fields=rows.SelectMany(row=>row.Controls.OfType<FieldValueLayout>()).ToArray();
        foreach(var field in fields)field.CaptionWidthProvider=()=>fields.Select(f=>TextRenderer.MeasureText(f.Description.Text,OptionsMeasureContext.FontOf(f.Description)).Width).DefaultIfEmpty(0).Max();
        foreach(var row in rows)result.AddWide(row);
        return result;
    }
    public static SettingsComposition SettingsWithPreview(Control settings,Control preview)
    {
        var result=new SettingsComposition(true);result.AddWide(settings);result.AddWide(preview);return result;
    }
    private SettingsComposition(bool columns,bool equalValueWidths=false)
    {
        _columns=columns;_equalValueWidths=equalValueWidths;Margin=Padding.Empty;AutoScaleMode=AutoScaleMode.Inherit;
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute));_grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(_grid);
    }
    public Label AddField(string caption,Control control)
    {
        var label=new Label { Text=caption,AutoSize=false,UseMnemonic=false,TextAlign=ContentAlignment.MiddleLeft,TabStop=false };
        control.AccessibleName ??= caption.TrimEnd(':');label.Click+=(_,_)=>control.Focus();
        Add(label,control);return label;
    }
    public void AddWide(Control control)=>Add(null,control);
    private void Add(Label? label,Control value)
    {
        var index=_rows.Count;_rows.Add((label,value));
        if(label is not null){label.TabIndex=index*2;_grid.Controls.Add(label);label.TextChanged+=(_,_)=>MetricsChanged?.Invoke(this,EventArgs.Empty);}
        value.TabIndex=index*2+1;_grid.Controls.Add(value);
        value.EnabledChanged+=(_,_)=>UpdateTabStop();
        value.TabStopChanged+=(_,_)=>UpdateTabStop();
        UpdateTabStop();
        if(value is IDialogContent content)content.MetricsChanged+=(_,_)=>MetricsChanged?.Invoke(this,EventArgs.Empty);
        MetricsChanged?.Invoke(this,EventArgs.Empty);
    }
    // A purely structural container with no enabled input must not become a blank Tab stop.
    private void UpdateTabStop()
    {
        static bool HasInput(Control c)=>c.Enabled&&((c is ButtonBase or TextBoxBase or ListControl or UpDownBase)&&c.TabStop||c.Controls.Cast<Control>().Any(HasInput));
        TabStop=_rows.Any(r=>HasInput(r.Value));
    }
    // Width queries must not recursively measure child heights. In a nested composition
    // that repeated the full subtree for each width lookup, even during tab activation.
    private int NaturalWidth(Control c)=>c switch
    {
        SettingsComposition composition=>composition.PreferredContentWidth,
        AdaptiveSettingsRow row=>row.PreferredContentWidth,
        NativeOptionList options=>options.PreferredContentWidth,
        NumericUpDown n=>NativeControlLayout.NumericWidth(n),
        _=>OptionsMeasureContext.Preferred(c, Size.Empty).Width
    };
    private Size Natural(Control c)=>new(NaturalWidth(c),HeightFor(c,NaturalWidth(c)));
    private int Minimum(Control c)=>c switch
    {
        IDialogContent content=>content.MinimumContentWidth,
        AdaptiveSettingsRow row=>row.MinimumContentWidth,
        NativeOptionList options=>options.MinimumContentWidth,
        _=>NaturalWidth(c)
    };
    private int CaptionWidth=>_rows.Where(r=>r.Label is not null).Select(r=>MeasuredText(r.Label!.Text,OptionsMeasureContext.FontOf(r.Label)).Width).DefaultIfEmpty(0).Max();
    private int ValueWidth=>_rows.Where(r=>r.Label is not null).Select(r=>NaturalWidth(r.Value)).DefaultIfEmpty(0).Max();
    public int PreferredContentWidth=>_columns?_rows.Sum(r=>NaturalWidth(r.Value))+Gap:
        Math.Max(CaptionWidth+Gap+ValueWidth,_rows.Where(r=>r.Label is null).Select(r=>NaturalWidth(r.Value)).DefaultIfEmpty(0).Max());
    public int MinimumContentWidth=>_rows.Select(r=>Math.Max(Minimum(r.Value),r.Label?.Text.Split(' ').Select(w=>MeasuredText(w,OptionsMeasureContext.FontOf(r.Label)).Width).DefaultIfEmpty(0).Max()??0)).DefaultIfEmpty(0).Max();
    public int MeasureForWidth(int width)=>Measure(width,true);
    public int MeasureHeightForWidth(int width)=>Measure(width,false);
    private int HeightFor(Control c,int width)=>c switch
    {
        IDialogContent content=>content.MeasureHeightForWidth(width),
        AdaptiveSettingsRow row=>row.Measure(width,false),
        NativeOptionList options=>options.MeasureHeight(width),
        _=>OptionsMeasureContext.Preferred(c, Size.Empty).Height
    };
    private int Measure(int width,bool apply)
    {
        if(_rows.Count==0 || (_columns&&_rows.Count<2))return 1;
        width=Math.Max(1,width);var stacked=width<PreferredContentWidth;
        var heights=new List<int>();
        if(_columns)
        {
            if(stacked) foreach(var r in _rows)heights.Add(HeightFor(r.Value,width));
            else heights.Add(Math.Max(HeightFor(_rows[0].Value,NaturalWidth(_rows[0].Value)),HeightFor(_rows[1].Value,width-NaturalWidth(_rows[0].Value)-Gap)));
        }
        else
        {
            var uniform=_rows.Select(r=>Math.Max(r.Label is null?0:MeasuredText(r.Label.Text,OptionsMeasureContext.FontOf(r.Label)).Height,HeightFor(r.Value,width))).DefaultIfEmpty(0).Max();
            foreach(var r in _rows)
            {
                if(stacked&&r.Label is not null)heights.Add(MeasuredText(r.Label.Text,OptionsMeasureContext.FontOf(r.Label),new Size(width,int.MaxValue),TextFormatFlags.WordBreak).Height);
                heights.Add(r.Label is null?Math.Max(uniform,HeightFor(r.Value,width)):uniform);
            }
        }
        var total=heights.Sum()+Math.Max(0,heights.Count-1)*Gap;
        if(!apply)return Math.Max(1,total);
        _grid.SuspendLayout();
        try
        {
            if(_lastStacked!=stacked||_grid.RowStyles.Count!=heights.Count)
            {
                _lastStacked=stacked;_grid.RowCount=heights.Count;
                while(_grid.RowStyles.Count<heights.Count)_grid.RowStyles.Add(new RowStyle(SizeType.Absolute));
                while(_grid.RowStyles.Count>heights.Count)_grid.RowStyles.RemoveAt(_grid.RowStyles.Count-1);
                int row=0;
                for(var i=0;i<_rows.Count;i++)
                {
                    var item=_rows[i];
                    if(_columns){Place(item.Value,stacked?0:i,stacked?i:0,stacked?2:1);continue;}
                    if(item.Label is not null){Place(item.Label,0,row,stacked?2:1);if(stacked)row++;}
                    Place(item.Value,item.Label is null||stacked?0:1,row,item.Label is null||stacked?2:1);row++;
                }
            }
            _grid.ColumnStyles[0].Width=_columns?(_rows.Count==0?0:NaturalWidth(_rows[0].Value)+Gap):CaptionWidth+Gap;
            if(stacked)_grid.ColumnStyles[0].Width=0;
            for(var i=0;i<heights.Count;i++)_grid.RowStyles[i].Height=heights[i]+(i==heights.Count-1?0:Gap);
            foreach(var item in _rows)
            {
                var rowIndex=_grid.GetRow(item.Value);
                item.Value.Margin=new Padding(0,0,_columns&&!stacked&&item.Value==_rows[0].Value?Gap:0,rowIndex==heights.Count-1?0:Gap);
                if(item.Label is not null){item.Label.Dock=DockStyle.Fill;var labelRow=_grid.GetRow(item.Label);item.Label.Margin=new Padding(0,0,stacked?0:Gap,labelRow==heights.Count-1?0:Gap);}
                item.Value.Dock=_columns||item.Label is null?DockStyle.Fill:DockStyle.None;
                if(item.Label is not null){item.Value.Anchor=AnchorStyles.Left;item.Value.Size=new Size(_equalValueWidths?ValueWidth:NaturalWidth(item.Value),Natural(item.Value).Height);}
            }
        }
        finally{_grid.ResumeLayout(true);}
        return Math.Max(1,total);
        void Place(Control c,int column,int row,int span){_grid.SetCellPosition(c,new TableLayoutPanelCellPosition(column,row));_grid.SetColumnSpan(c,span);c.Margin=Padding.Empty;}
    }
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);if(_arranging)return;_arranging=true;try{Measure(ClientSize.Width,true);}finally{_arranging=false;}}
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);_textCache.Clear();MetricsChanged?.Invoke(this,EventArgs.Empty);}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);_textCache.Clear();MetricsChanged?.Invoke(this,EventArgs.Empty);}
}
