namespace SubEdit.Player.WinForms;

internal sealed class ItalicMarkerConversionDialog : SubEditDialog
{
    private readonly CheckBox _slash;
    private readonly CheckBox _asterisk;
    private readonly CheckBox _hash;

    public ItalicMarkerConversionDialog()
    {
        Text = "Zamień znaki kursywy";
        ClientSize = new Size(360, 145);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 3,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            AutoSize = true
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var description = new Label
        {
            Text = "Zamień znaki kursywy na <i> </i>",
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 12)
        };

        var markerPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        _slash = CreateMarkerOption("/", isChecked: true);
        _asterisk = CreateMarkerOption("*", isChecked: false);
        _hash = CreateMarkerOption("#", isChecked: false);
        markerPanel.Controls.AddRange([_slash, _asterisk, _hash]);

        var buttonPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 12, 0, 0)
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(75, 25),
            Margin = new Padding(0, 0, 6, 0)
        };
        var cancel = new Button
        {
            Text = "Anuluj",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(75, 25),
            Margin = Padding.Empty
        };
        buttonPanel.Controls.AddRange([ok, cancel]);

        layout.Controls.Add(description, 0, 0);
        layout.Controls.Add(markerPanel, 0, 1);
        layout.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
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

    private static CheckBox CreateMarkerOption(string marker, bool isChecked) => new()
    {
        Text = marker,
        Checked = isChecked,
        AutoSize = true,
        Margin = new Padding(10, 0, 10, 0)
    };
}
