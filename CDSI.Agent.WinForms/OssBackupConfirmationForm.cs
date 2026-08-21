using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.WinForms;

internal sealed partial class OssBackupConfirmationForm : Form
{
    private const string ObjectNameColumnName = "ObjectName";
    private readonly ComboBox _profileComboBox = new();
    private readonly Label _targetLabel = new();
    private readonly DataGridView _assetsGrid = new();
    private readonly string? _objectDirectory;
    private IReadOnlyDictionary<Guid, string> _selectedObjectNames =
        new Dictionary<Guid, string>();

    public OssBackupConfirmationForm(
        IReadOnlyCollection<ConfiguredObjectStorageProfile> profiles,
        IReadOnlyCollection<AssetListItem> assets,
        string? objectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(assets);
        if (profiles.Count == 0)
        {
            throw new ArgumentException("至少需要一个可用的 OSS 配置。", nameof(profiles));
        }
        if (assets.Count == 0)
        {
            throw new ArgumentException("至少需要一个待备份资产。", nameof(assets));
        }
        _objectDirectory = objectDirectory;

        Text = objectDirectory is null ? "确认备份到 OSS" : "确认同步项目到 OSS";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 430);
        Size = new Size(790, 540);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20),
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"将 {assets.Count:N0} 个资产备份到 OSS",
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Color.FromArgb(31, 37, 43),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        var targetLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty
        };
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        targetLayout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "目标配置",
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _profileComboBox.Dock = DockStyle.Fill;
        _profileComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _profileComboBox.DisplayMember = nameof(ProfileChoice.DisplayName);
        _profileComboBox.Items.AddRange(profiles
            .Select(profile => new ProfileChoice(profile))
            .Cast<object>()
            .ToArray());
        _profileComboBox.SelectedIndexChanged += (_, _) => UpdateTargetLabel();
        _profileComboBox.SelectedIndex = 0;
        targetLayout.Controls.Add(_profileComboBox, 1, 0);
        _targetLabel.Dock = DockStyle.Fill;
        _targetLabel.ForeColor = Color.FromArgb(72, 81, 89);
        _targetLabel.TextAlign = ContentAlignment.MiddleLeft;
        _targetLabel.AutoEllipsis = true;
        targetLayout.Controls.Add(_targetLabel, 1, 1);
        layout.Controls.Add(targetLayout, 0, 1);

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = objectDirectory is null
                ? "这会把下列文件内容发送到选定的 OSS Bucket。上传后将校验大小和 SHA-256；本地文件不会被修改或删除。"
                : $"文件将同步到 OSS 目录“{objectDirectory}/”，文件名保持本地原名。上传后将校验大小和 SHA-256。",
            ForeColor = Color.FromArgb(137, 49, 49),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 2);

        ConfigureAssetGrid(assets, allowCustomObjectNames: objectDirectory is null);
        layout.Controls.Add(_assetsGrid, 0, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };
        var confirmButton = new Button
        {
            Text = "确认备份",
            Size = new Size(104, 32),
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
        layout.Controls.Add(buttons, 0, 4);

        AcceptButton = confirmButton;
        CancelButton = cancelButton;
        Controls.Add(layout);
        UpdateTargetLabel();
    }

    public Guid SelectedProfileId =>
        ((ProfileChoice)_profileComboBox.SelectedItem!).Profile.Profile.Id;

    private void UpdateTargetLabel()
    {
        if (_profileComboBox.SelectedItem is not ProfileChoice choice)
        {
            _targetLabel.Text = string.Empty;
            return;
        }

        var profile = choice.Profile.Profile;
        var directoryText = _objectDirectory is null
            ? string.Empty
            : $" · 目录: {_objectDirectory}/";
        _targetLabel.Text =
            $"Bucket: {profile.BucketName} · Endpoint: {profile.Endpoint}{directoryText}";
    }

    private sealed record ProfileChoice(ConfiguredObjectStorageProfile Profile)
    {
        public string DisplayName =>
            $"{Profile.Profile.DisplayName} ({Profile.Profile.BucketName})";
    }
}
