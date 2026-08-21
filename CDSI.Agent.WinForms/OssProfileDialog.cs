using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

public sealed class OssProfileDialog : Form
{
    private readonly Guid? _profileId;
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _displayNameTextBox = new();
    private readonly TextBox _endpointTextBox = new();
    private readonly TextBox _bucketTextBox = new();
    private readonly TextBox _regionTextBox = new();
    private readonly TextBox _accessKeyIdTextBox = new();
    private readonly TextBox _accessKeySecretTextBox = new();
    private readonly CheckBox _useHttpsCheckBox = new();

    public OssProfileDialog(ObjectStorageProfile? profile = null)
    {
        _profileId = profile?.Id;

        Text = profile is null ? "添加备份配置" : "编辑备份配置";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(650, 530);
        MinimumSize = new Size(600, 530);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 10,
            Padding = new Padding(24),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 8; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.Items.AddRange(
        [
            new ProviderChoice("阿里云 OSS", ObjectStorageProvider.AliyunOss),
            new ProviderChoice("七牛云 Kodo", ObjectStorageProvider.QiniuKodo)
        ]);
        AddField(layout, 0, "提供商", _providerComboBox);
        AddField(layout, 1, "配置名称", _displayNameTextBox);
        AddField(layout, 2, "Endpoint", _endpointTextBox);
        AddField(layout, 3, "Bucket", _bucketTextBox);
        AddField(layout, 4, "地域 / Region ID", _regionTextBox);
        AddField(layout, 5, "AccessKey ID", _accessKeyIdTextBox);
        AddField(layout, 6, "AccessKey Secret", _accessKeySecretTextBox);

        _accessKeySecretTextBox.UseSystemPasswordChar = true;
        _accessKeySecretTextBox.AccessibleName = "AccessKey Secret";
        _accessKeySecretTextBox.PlaceholderText = profile is null
            ? "必填"
            : "留空保留现有凭据";

        _useHttpsCheckBox.Text = "使用 HTTPS";
        _useHttpsCheckBox.Checked = profile?.UseHttps ?? true;
        _useHttpsCheckBox.AutoSize = true;
        _useHttpsCheckBox.Dock = DockStyle.Fill;
        _useHttpsCheckBox.Margin = new Padding(0, 8, 0, 8);
        layout.Controls.Add(_useHttpsCheckBox, 1, 7);

        var securityNote = new Label
        {
            Text = "AccessKey Secret 仅保存到 Windows 凭据管理器，不写入 CDSI 数据库。",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(88, 98, 106),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(securityNote, 0, 8);
        layout.SetColumnSpan(securityNote, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty
        };
        var saveButton = CreateButton(
            "保存",
            Color.FromArgb(24, 121, 78),
            Color.White);
        saveButton.DialogResult = DialogResult.OK;
        saveButton.Size = new Size(96, 32);
        var cancelButton = CreateButton(
            "取消",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Size = new Size(88, 32);
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons, 0, 9);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);

        if (profile is not null)
        {
            SelectProvider(profile.Provider);
            _displayNameTextBox.Text = profile.DisplayName;
            _endpointTextBox.Text = profile.Endpoint;
            _bucketTextBox.Text = profile.BucketName;
            _regionTextBox.Text = profile.Region ?? string.Empty;
            _accessKeyIdTextBox.Text = profile.AccessKeyId;
        }
        else
        {
            SelectProvider(ObjectStorageProvider.AliyunOss);
            _displayNameTextBox.Text = "主备份";
            _endpointTextBox.Text = "oss-cn-hangzhou.aliyuncs.com";
            _regionTextBox.Text = "cn-hangzhou";
        }

        _providerComboBox.SelectedIndexChanged += (_, _) =>
            ApplyProviderDefaults();
    }

    public SaveObjectStorageProfileRequest CreateRequest()
    {
        return new SaveObjectStorageProfileRequest(
            _profileId,
            _displayNameTextBox.Text,
            _endpointTextBox.Text,
            _bucketTextBox.Text,
            _regionTextBox.Text,
            _useHttpsCheckBox.Checked,
            _accessKeyIdTextBox.Text,
            string.IsNullOrEmpty(_accessKeySecretTextBox.Text)
                ? null
                : _accessKeySecretTextBox.Text,
            SelectedProvider);
    }

    internal ObjectStorageProvider SelectedProvider =>
        _providerComboBox.SelectedItem is ProviderChoice choice
            ? choice.Provider
            : ObjectStorageProvider.AliyunOss;

    private static void AddField(
        TableLayoutPanel layout,
        int row,
        string labelText,
        Control control)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 8, 0, 8);
        if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }

        control.AccessibleName ??= labelText;

        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void SelectProvider(ObjectStorageProvider provider)
    {
        for (var index = 0; index < _providerComboBox.Items.Count; index++)
        {
            if (_providerComboBox.Items[index] is ProviderChoice choice &&
                choice.Provider == provider)
            {
                _providerComboBox.SelectedIndex = index;
                return;
            }
        }

        _providerComboBox.SelectedIndex = 0;
    }

    private void ApplyProviderDefaults()
    {
        if (SelectedProvider == ObjectStorageProvider.QiniuKodo)
        {
            if (string.IsNullOrWhiteSpace(_endpointTextBox.Text) ||
                _endpointTextBox.Text.Contains("aliyuncs.com", StringComparison.OrdinalIgnoreCase))
            {
                _endpointTextBox.Text = "s3.cn-east-1.qiniucs.com";
            }

            if (string.IsNullOrWhiteSpace(_regionTextBox.Text) ||
                _regionTextBox.Text.StartsWith("cn-hangzhou", StringComparison.OrdinalIgnoreCase))
            {
                _regionTextBox.Text = "cn-east-1";
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_endpointTextBox.Text) ||
            _endpointTextBox.Text.Contains("qiniucs.com", StringComparison.OrdinalIgnoreCase))
        {
            _endpointTextBox.Text = "oss-cn-hangzhou.aliyuncs.com";
        }

        if (string.IsNullOrWhiteSpace(_regionTextBox.Text) ||
            _regionTextBox.Text.StartsWith("cn-east-", StringComparison.OrdinalIgnoreCase))
        {
            _regionTextBox.Text = "cn-hangzhou";
        }
    }

    private sealed record ProviderChoice(
        string DisplayName,
        ObjectStorageProvider Provider)
    {
        public override string ToString() => DisplayName;
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        return new Button
        {
            Text = text,
            BackColor = background,
            ForeColor = foreground,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            Cursor = Cursors.Hand
        };
    }
}
