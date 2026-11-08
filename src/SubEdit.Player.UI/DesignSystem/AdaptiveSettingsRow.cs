namespace SubEdit.Player.UI.DesignSystem;

/// <summary>Ordered, indivisible logical groups; greedy wrapping without nested tables.</summary>
public sealed class AdaptiveSettingsRow : UserControl
{
    private readonly List<Control> _groups = [];
    private bool _arranging;
    [System.ComponentModel.DefaultValue(false)]
    public bool KeepTogether { get; init; }
    public event EventHandler? MetricsChanged;
    private Size Natural(Control control) => control switch
    {
        FieldValueLayout value => value.NaturalSize,
        NativeOptionList options => new Size(options.PreferredContentWidth, options.MeasureHeight(options.PreferredContentWidth)),
        _ => OptionsMeasureContext.Preferred(control, Size.Empty)
    };
    public int MinimumContentWidth => KeepTogether ? PreferredContentWidth : _groups.Select(g => Natural(g).Width).DefaultIfEmpty(0).Max();
    public int PreferredContentWidth => _groups.Sum(g => Natural(g).Width) + Math.Max(0,_groups.Count-1)*Gap;
    private int Gap => UiDesignTokens.Pixels(UiDesignTokens.Current.SectionGap,OptionsMeasureContext.Dpi(this));
    public CheckBox AddCheckBox(string caption,bool isChecked)
    {
        var options=new NativeOptionList();
        var check=options.Add(new CheckBox { Checked=isChecked, UseVisualStyleBackColor=true },caption);
        options.MetricsChanged += (_,_) => MetricsChanged?.Invoke(this,EventArgs.Empty);
        Add(options); return check;
    }
    public FieldValueLayout AddField(string caption,Control editor,int? preferredWidth=null,string? widthSample=null)
    {
        editor.AccessibleName ??= caption.Replace("&", "").TrimEnd(':');
        var value = new FieldValueLayout(editor,caption,keepTogether:true,descriptionFirst:true,logicalEditorWidth:preferredWidth,widthSample:widthSample);
        value.Description.Click += (_,_) => editor.Focus();
        Add(value);
        if(editor is ComboBox combo) NativeControlLayout.BindComboBox(combo);
        return value;
    }
    public void AddControl(Control control) => Add(control);
    private void Add(Control control)
    {
        control.TabIndex=_groups.Count; control.Margin=Padding.Empty;
        _groups.Add(control); Controls.Add(control);
        MetricsChanged?.Invoke(this,EventArgs.Empty);
    }
    public int Measure(int width,bool apply)
    {
        var lines=new List<List<(Control Control,Size Size)>>();
        var line=new List<(Control Control,Size Size)>(); var used=0;
        foreach(var group in _groups)
        {
            var size=Natural(group);
            if(!KeepTogether && line.Count>0 && used+Gap+size.Width>width) { lines.Add(line);line=[];used=0; }
            line.Add((group,size)); used+=(line.Count>1?Gap:0)+size.Width;
        }
        if(line.Count>0)lines.Add(line);
        var top=0;
        foreach(var row in lines)
        {
            var height=row.Max(g=>g.Size.Height);var left=0;
            foreach(var item in row)
            {
                if(apply)item.Control.Bounds=new Rectangle(left,top+(height-item.Size.Height)/2,item.Size.Width,item.Size.Height);
                left+=item.Size.Width+Gap;
            }
            top+=height+Gap;
        }
        return Math.Max(1,top-(lines.Count>0?Gap:0));
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);if(_arranging)return;_arranging=true;
        try { Measure(ClientSize.Width,true); } finally { _arranging=false; }
    }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e);MetricsChanged?.Invoke(this,EventArgs.Empty); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e);MetricsChanged?.Invoke(this,EventArgs.Empty); }
}
