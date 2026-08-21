using CDSI.Agent.Application.Git;
using CDSI.Agent.Core.Git;

namespace CDSI.Agent.WinForms;

public sealed class GitProfileDialog : Form
{
    internal static readonly IReadOnlyList<GitProviderOption> ProviderOptions =
    [
        new(GitHostingProvider.GitHub, "GitHub"),
        new(GitHostingProvider.Gitee, "Gitee（码云）")
    ];

    private readonly Guid? _profileId;
    private readonly TextBox _displayNameTextBox = new();
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _repositoryUrlTextBox = new();
    private readonly TextBox _accountNameTextBox = new();
    private readonly TextBox _defaultBranchTextBox = new();
    private readonly TextBox _accessTokenTextBox = new();
    private readonly CheckBox _isDefaultCheckBox = new();

    public GitProfileDialog(GitProfile? profile = null)
    {
        _profileId = profile?.Id;
        Text = profile is null ? "添加 Git 配置" : "编辑 Git 配置";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(650, 500);
        MinimumSize = new Size(600, 500);
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 7; row++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        AddField(layout, 0, "配置名称", _displayNameTextBox);
        AddProviderField(layout, 1);
        AddField(layout, 2, "仓库地址", _repositoryUrlTextBox);
        AddField(layout, 3, "账号", _accountNameTextBox);
        AddField(layout, 4, "默认分支", _defaultBranchTextBox);
        AddField(layout, 5, "访问令牌", _accessTokenTextBox);

        _accessTokenTextBox.UseSystemPasswordChar = true;
        _accessTokenTextBox.AccessibleName = "Git 访问令牌";
        _accessTokenTextBox.PlaceholderText = profile is null
            ? "可选；使用 SSH 或系统 Git 凭据时留空"
            : "留空保留现有凭据";

        _isDefaultCheckBox.Text = "设为默认 Git 配置";
        _isDefaultCheckBox.Checked = profile?.IsDefault ?? false;
        _isDefaultCheckBox.Enabled = profile?.IsDefault != true;
        _isDefaultCheckBox.AutoSize = true;
        _isDefaultCheckBox.Dock = DockStyle.Fill;
        _isDefaultCheckBox.Margin = new Padding(0, 8, 0, 8);
        _isDefaultCheckBox.AccessibleName = "设为默认 Git 配置";
        layout.Controls.Add(_isDefaultCheckBox, 1, 6);

        var securityNote = new Label
        {
            Text = "访问令牌仅保存到 Windows 凭据管理器，不会写入 CDSI 数据库或仓库地址。保存配置不会连接或修改仓库。",
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
        layout.Controls.Add(buttons, 0, 8);
        layout.SetColumnSpan(buttons, 2);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(layout);

        _providerComboBox.Items.AddRange(ProviderOptions.Cast<object>().ToArray());
        var provider = profile?.Provider ?? GitHostingProvider.GitHub;
        _providerComboBox.SelectedItem = ProviderOptions.Single(
            option => option.Value == provider);
        _providerComboBox.SelectedIndexChanged += (_, _) =>
            UpdateProviderPlaceholders();

        if (profile is not null)
        {
            _displayNameTextBox.Text = profile.DisplayName;
            _repositoryUrlTextBox.Text = profile.RepositoryUrl;
            _accountNameTextBox.Text = profile.AccountName;
            _defaultBranchTextBox.Text = profile.DefaultBranch;
        }
        else
        {
            _displayNameTextBox.Text = "主 Git 仓库";
            _defaultBranchTextBox.Text = "main";
        }

        UpdateProviderPlaceholders();
    }

    public SaveGitProfileRequest CreateRequest()
    {
        var provider = (_providerComboBox.SelectedItem as GitProviderOption)?.Value
            ?? GitHostingProvider.GitHub;
        return new SaveGitProfileRequest(
            _profileId,
            _displayNameTextBox.Text,
            provider,
            _repositoryUrlTextBox.Text,
            _accountNameTextBox.Text,
            _defaultBranchTextBox.Text,
            string.IsNullOrEmpty(_accessTokenTextBox.Text)
                ? null
                : _accessTokenTextBox.Text,
            _isDefaultCheckBox.Checked);
    }

    private void AddProviderField(TableLayoutPanel layout, int row)
    {
        var label = CreateFieldLabel("平台");
        _providerComboBox.Dock = DockStyle.Fill;
        _providerComboBox.Margin = new Padding(0, 8, 0, 8);
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.AccessibleName = "Git 托管平台";
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(_providerComboBox, 1, row);
    }

    private static void AddField(
        TableLayoutPanel layout,
        int row,
        string labelText,
        TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 8, 0, 8);
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.AccessibleName ??= labelText;
        layout.Controls.Add(CreateFieldLabel(labelText), 0, row);
        layout.Controls.Add(textBox, 1, row);
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(52, 61, 69)
        };
    }

    private void UpdateProviderPlaceholders()
    {
        var provider = (_providerComboBox.SelectedItem as GitProviderOption)?.Value
            ?? GitHostingProvider.GitHub;
        _repositoryUrlTextBox.PlaceholderText = provider == GitHostingProvider.GitHub
            ? "https://github.com/owner/repository.git"
            : "https://gitee.com/owner/repository.git";
        if (_profileId is null &&
            (_defaultBranchTextBox.Text is "main" or "master"))
        {
            _defaultBranchTextBox.Text = provider == GitHostingProvider.GitHub
                ? "main"
                : "master";
        }
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

    internal sealed record GitProviderOption(
        GitHostingProvider Value,
        string Label)
    {
        public override string ToString() => Label;
    }
}
