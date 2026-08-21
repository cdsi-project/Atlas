using CDSI.Agent.Core.Collections;

namespace CDSI.Agent.WinForms;

internal sealed class AssetCollectionSelectionForm : Form
{
    private readonly ComboBox _collectionComboBox = new();

    public AssetCollectionSelectionForm(
        IReadOnlyCollection<AssetCollectionSummary> collections,
        int selectedAssetCount)
    {
        ArgumentNullException.ThrowIfNull(collections);
        if (collections.Count == 0)
        {
            throw new ArgumentException("至少需要一个资产清单。", nameof(collections));
        }

        Text = "加入项目";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 180);
        MinimumSize = new Size(460, 180);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"将 {selectedAssetCount:N0} 个资产加入",
            Font = new Font("Segoe UI Semibold", 10F),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(31, 37, 43)
        }, 0, 0);

        _collectionComboBox.Dock = DockStyle.Fill;
        _collectionComboBox.Margin = new Padding(0, 6, 0, 6);
        _collectionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _collectionComboBox.DisplayMember = nameof(CollectionChoice.DisplayName);
        _collectionComboBox.AccessibleName = "目标项目";
        _collectionComboBox.Items.AddRange(collections
            .Select(collection => new CollectionChoice(collection))
            .Cast<object>()
            .ToArray());
        _collectionComboBox.SelectedIndex = 0;
        layout.Controls.Add(_collectionComboBox, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0)
        };
        var confirmButton = new Button
        {
            Text = "加入",
            DialogResult = DialogResult.OK,
            Size = new Size(96, 32),
            BackColor = Color.FromArgb(24, 121, 78),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        confirmButton.FlatAppearance.BorderSize = 0;
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

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
    }

    public Guid SelectedCollectionId =>
        ((CollectionChoice)_collectionComboBox.SelectedItem!).Summary.Id;

    private sealed record CollectionChoice(AssetCollectionSummary Summary)
    {
        public string DisplayName =>
            $"{Summary.Name} · {FormatCollectionType(Summary.Type)} · {Summary.AssetCount:N0} 个资产";
    }

    private static string FormatCollectionType(AssetCollectionType type)
    {
        return type switch
        {
            AssetCollectionType.Video => "视频",
            AssetCollectionType.Audio => "音频",
            AssetCollectionType.Image => "图片",
            AssetCollectionType.Text => "文字",
            AssetCollectionType.Mixed => "综合",
            _ => type.ToString()
        };
    }
}
