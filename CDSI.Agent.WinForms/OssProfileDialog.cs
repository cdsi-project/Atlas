using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

public sealed class OssProfileDialog : Form
{
    private readonly Guid? _profileId;
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

        Text = profile is null ? "添加 OSS 配置" : "编辑 OSS 配置";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(610, 470);
        MinimumSize = new Size(560, 470);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(24),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        AddField(layout, 0, "配置名称", _displayNameTextBox);
        AddField(layout, 1, "Endpoint", _endpointTextBox);
        AddField(layout, 2, "Bucket", _bucketTextBox);
        AddField(layout, 3, "地域", _regionTextBox);
        AddField(layout, 4, "AccessKey ID", _accessKeyIdTextBox);
        AddField(layout, 5, "AccessKey Secret", _accessKeySecretTextBox);

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
        layout.Controls.Add(_useHttpsCheckBox, 1, 6);

        var securityNote = new Label
        {
            Text = "AccessKey Secret 仅保存到 Windows 凭据管理器，不写入 CDSI 数据库。",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(88, 98, 106),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(securityNote, 0, 7);
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
        layout.Controls.Add(buttons, 0, 8);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);

        if (profile is not null)
        {
            _displayNameTextBox.Text = profile.DisplayName;
            _endpointTextBox.Text = profile.Endpoint;
            _bucketTextBox.Text = profile.BucketName;
            _regionTextBox.Text = profile.Region ?? string.Empty;
            _accessKeyIdTextBox.Text = profile.AccessKeyId;
        }
        else
        {
            _displayNameTextBox.Text = "主 OSS";
            _endpointTextBox.Text = "oss-cn-hangzhou.aliyuncs.com";
            _regionTextBox.Text = "cn-hangzhou";
        }
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
                : _accessKeySecretTextBox.Text);
    }

    private static void AddField(
        TableLayoutPanel layout,
        int row,
        string labelText,
        TextBox textBox)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        };
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 8, 0, 8);
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.AccessibleName ??= labelText;

        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(textBox, 1, row);
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
