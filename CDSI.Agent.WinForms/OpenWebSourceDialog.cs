using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.WinForms;

public sealed class OpenWebSourceDialog : Form
{
    private readonly Guid? _sourceId;
    private readonly TextBox _displayNameTextBox = new();
    private readonly TextBox _originDomainTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _applicationPasswordTextBox = new();
    private readonly CheckBox _isDefaultCheckBox = new();

    public OpenWebSourceDialog(OpenWebSource? source = null)
    {
        _sourceId = source?.Id;
        Text = source is null ? "添加 OpenWeb 源站" : "编辑 OpenWeb 源站";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(610, 390);
        MinimumSize = new Size(560, 390);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(24),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        AddField(layout, 0, "源站名称", _displayNameTextBox);
        AddField(layout, 1, "源站域名", _originDomainTextBox);
        AddField(layout, 2, "WordPress 用户名", _usernameTextBox);
        AddField(layout, 3, "应用程序密码", _applicationPasswordTextBox);

        _applicationPasswordTextBox.UseSystemPasswordChar = true;
        _applicationPasswordTextBox.AccessibleName = "WordPress 应用程序密码";
        _applicationPasswordTextBox.PlaceholderText = source is null
            ? "必填"
            : "留空保留现有凭据";

        _isDefaultCheckBox.Text = "设为默认源站";
        _isDefaultCheckBox.Checked = source?.IsDefault ?? false;
        _isDefaultCheckBox.Enabled = source?.IsDefault != true;
        _isDefaultCheckBox.AutoSize = true;
        _isDefaultCheckBox.Dock = DockStyle.Fill;
        _isDefaultCheckBox.Margin = new Padding(0, 8, 0, 8);
        _isDefaultCheckBox.AccessibleName = "设为默认 OpenWeb 源站";
        layout.Controls.Add(_isDefaultCheckBox, 1, 4);

        var securityNote = new Label
        {
            Text = "应用程序密码仅保存到 Windows 凭据管理器，并按源站独立存储。",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(88, 98, 106),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        layout.Controls.Add(securityNote, 0, 5);
        layout.SetColumnSpan(securityNote, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Margin = Padding.Empty
        };
        var saveButton = CreateButton("保存", Color.FromArgb(24, 121, 78), Color.White);
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
        layout.Controls.Add(buttons, 0, 6);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);

        if (source is not null)
        {
            _displayNameTextBox.Text = source.DisplayName;
            _originDomainTextBox.Text = source.OriginDomain;
            _usernameTextBox.Text = source.WordPressUsername;
        }
    }

    public SaveOpenWebSourceRequest CreateRequest()
    {
        return new SaveOpenWebSourceRequest(
            _sourceId,
            _displayNameTextBox.Text,
            _originDomainTextBox.Text,
            _usernameTextBox.Text,
            string.IsNullOrEmpty(_applicationPasswordTextBox.Text)
                ? null
                : _applicationPasswordTextBox.Text,
            _isDefaultCheckBox.Checked);
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
