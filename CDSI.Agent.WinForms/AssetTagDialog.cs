using CDSI.Agent.Application.Assets;
using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

internal sealed class AssetTagDialog : Form
{
    private readonly ComboBox _tagNameComboBox = new();

    public AssetTagDialog(
        IReadOnlyList<AssetTagSummary> existingTags,
        int selectedAssetCount)
    {
        Text = "自定义标签";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 174);
        MinimumSize = new Size(440, 174);
        MaximumSize = new Size(760, 220);
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "标签名称",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        }, 0, 0);
        _tagNameComboBox.Dock = DockStyle.Fill;
        _tagNameComboBox.Margin = new Padding(0, 7, 0, 7);
        _tagNameComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        _tagNameComboBox.MaxLength = AssetTagService.MaximumNameLength;
        _tagNameComboBox.AccessibleName = "自定义资产标签";
        _tagNameComboBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _tagNameComboBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        _tagNameComboBox.Items.AddRange(existingTags
            .Select(tag => (object)tag.Name)
            .ToArray());
        layout.Controls.Add(_tagNameComboBox, 1, 0);

        var description = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"将标签添加到所选 {selectedAssetCount:N0} 个资产",
            ForeColor = Color.FromArgb(88, 98, 106),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(description, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var confirmButton = new Button
        {
            Text = "添加",
            Size = new Size(96, 32),
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        confirmButton.FlatAppearance.BorderSize = 0;
        confirmButton.Click += ConfirmButton_Click;
        var cancelButton = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Size = new Size(88, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        buttons.Controls.Add(confirmButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 2);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    public string TagName => _tagNameComboBox.Text.Trim();

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (TagName.Length == 0)
        {
            MessageBox.Show(
                this,
                "请输入标签名称。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            _tagNameComboBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
