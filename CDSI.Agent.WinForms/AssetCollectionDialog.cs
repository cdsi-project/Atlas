using CDSI.Agent.Core.Collections;

namespace CDSI.Agent.WinForms;

internal sealed class AssetCollectionDialog : Form
{
    private readonly TextBox _nameTextBox = new();
    private readonly ComboBox _typeComboBox = new();

    public AssetCollectionDialog()
    {
        Text = "创建资产清单";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 210);
        MinimumSize = new Size(440, 210);
        MaximumSize = new Size(760, 260);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateLabel("名称"), 0, 0);
        _nameTextBox.Dock = DockStyle.Fill;
        _nameTextBox.Margin = new Padding(0, 7, 0, 7);
        _nameTextBox.MaxLength = 120;
        _nameTextBox.AccessibleName = "资产清单名称";
        layout.Controls.Add(_nameTextBox, 1, 0);

        layout.Controls.Add(CreateLabel("类型"), 0, 1);
        _typeComboBox.Dock = DockStyle.Fill;
        _typeComboBox.Margin = new Padding(0, 7, 0, 7);
        _typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _typeComboBox.DisplayMember = nameof(TypeChoice.DisplayName);
        _typeComboBox.AccessibleName = "资产清单类型";
        _typeComboBox.Items.AddRange(CollectionTypeChoices
            .Select(choice => (object)choice)
            .ToArray());
        _typeComboBox.SelectedIndex = CollectionTypeChoices.Count - 1;
        layout.Controls.Add(_typeComboBox, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var createButton = new Button
        {
            Text = "创建",
            Size = new Size(96, 32),
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        createButton.FlatAppearance.BorderSize = 0;
        createButton.Click += CreateButton_Click;
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        buttons.Controls.Add(createButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = createButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    public string CollectionName => _nameTextBox.Text.Trim();

    public AssetCollectionType CollectionType =>
        ((TypeChoice)_typeComboBox.SelectedItem!).Type;

    internal static IReadOnlyList<TypeChoice> CollectionTypeChoices { get; } =
    [
        new(AssetCollectionType.Video, "视频"),
        new(AssetCollectionType.Audio, "音频"),
        new(AssetCollectionType.Image, "图片"),
        new(AssetCollectionType.Text, "文字"),
        new(AssetCollectionType.Mixed, "综合")
    ];

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        };
    }

    private void CreateButton_Click(object? sender, EventArgs e)
    {
        if (CollectionName.Length == 0)
        {
            MessageBox.Show(
                this,
                "请输入资产清单名称。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _nameTextBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    internal sealed record TypeChoice(
        AssetCollectionType Type,
        string DisplayName);
}
