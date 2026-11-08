namespace SubEdit.Player.WinForms;

internal sealed class ItalicMarkerConversionDialog : StandardDialog
{
    private readonly CheckBox _slash;
    private readonly CheckBox _asterisk;
    private readonly CheckBox _hash;

    public ItalicMarkerConversionDialog() : base(360)
    {
        Text = "Konwertuj znaczniki kursywy";
        var row = Page.AddSection("Konwertuj znaczniki kursywy na <i> </i>").AddAdaptiveRow();
        _slash = row.AddCheckBox("/", true); _asterisk = row.AddCheckBox("*", false); _hash = row.AddCheckBox("#", false);
        ConfigureActions();
    }

    public IReadOnlySet<char> SelectedMarkers
    {
        get
        {
            var markers = new HashSet<char>();
            if (_slash.Checked)
            {
                markers.Add('/');
            }

            if (_asterisk.Checked)
            {
                markers.Add('*');
            }

            if (_hash.Checked)
            {
                markers.Add('#');
            }

            return markers;
        }
    }

}
