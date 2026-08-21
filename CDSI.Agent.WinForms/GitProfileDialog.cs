using System.Diagnostics;
using CDSI.Agent.Application.Git;
using CDSI.Agent.Core.Git;

namespace CDSI.Agent.WinForms;

public sealed class GitProfileDialog : Form
{
    private const int UsernameRowIndex = 5;
    private const int PasswordRowIndex = 6;
    private const int FieldRowHeight = 46;

    internal static readonly IReadOnlyList<GitProviderOption> ProviderOptions =
    [
        new(GitHostingProvider.GitHub, "GitHub"),
        new(GitHostingProvider.Gitee, "Gitee（码云）")
    ];

    internal static readonly IReadOnlyList<GitAuthenticationOption>
        AuthenticationOptions =
        [
            new(GitAuthenticationMethod.Password, "密码"),
            new(GitAuthenticationMethod.Ssh, "SSH")
        ];

    private readonly Guid? _profileId;
    private readonly GitAuthenticationMethod? _originalAuthenticationMethod;
    private readonly TextBox _displayNameTextBox = new();
    private readonly ComboBox _providerComboBox = new();
    private readonly TextBox _repositoryUrlTextBox = new();
    private readonly TextBox _defaultBranchTextBox = new();
    private readonly ComboBox _authenticationComboBox = new();
    private readonly Label _usernameLabel = CreateFieldLabel("用户名");
    private readonly TextBox _usernameTextBox = new();
    private readonly Label _passwordLabel = CreateFieldLabel("密码");
    private readonly TextBox _passwordTextBox = new();
    private readonly Label _sshPublicKeyLabel = CreateFieldLabel("SSH 公钥");
    private readonly TextBox _sshPublicKeyTextBox = new();
    private readonly Button _selectSshKeyButton = new();
    private readonly Button _generateSshKeyButton = new();
    private readonly CheckBox _isDefaultCheckBox = new();
    private readonly TableLayoutPanel _layout = new();

    public GitProfileDialog(GitProfile? profile = null)
    {
        _profileId = profile?.Id;
        _originalAuthenticationMethod = profile?.AuthenticationMethod;
        Text = profile is null ? "添加 Git 配置" : "编辑 Git 配置";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 620);
        MinimumSize = new Size(700, 600);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        _layout.Dock = DockStyle.Fill;
        _layout.ColumnCount = 3;
        _layout.RowCount = 11;
        _layout.Padding = new Padding(24);
        _layout.BackColor = Color.White;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        for (var row = 0; row < 9; row++)
        {
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, FieldRowHeight));
        }

        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        AddTextField(_layout, 0, "配置名称", _displayNameTextBox);
        AddProviderField(_layout, 1);
        AddTextField(_layout, 2, "仓库地址", _repositoryUrlTextBox);
        AddTextField(_layout, 3, "默认分支", _defaultBranchTextBox);
        AddAuthenticationField(_layout, 4);
        AddTextField(_layout, UsernameRowIndex, _usernameLabel, _usernameTextBox);
        AddTextField(_layout, PasswordRowIndex, _passwordLabel, _passwordTextBox);
        AddSshKeyField(_layout, 7);

        _passwordTextBox.UseSystemPasswordChar = true;
        _passwordTextBox.AccessibleName = "Git 密码";
        _passwordTextBox.PlaceholderText = profile is null
            ? "必填；GitHub 可使用个人访问令牌作为密码"
            : "留空保留现有密码";

        _isDefaultCheckBox.Text = "设为默认 Git 配置";
        _isDefaultCheckBox.Checked = profile?.IsDefault ?? false;
        _isDefaultCheckBox.Enabled = profile?.IsDefault != true;
        _isDefaultCheckBox.AutoSize = true;
        _isDefaultCheckBox.Dock = DockStyle.Fill;
        _isDefaultCheckBox.Margin = new Padding(0, 8, 0, 8);
        _isDefaultCheckBox.AccessibleName = "设为默认 Git 配置";
        _layout.Controls.Add(_isDefaultCheckBox, 1, 8);
        _layout.SetColumnSpan(_isDefaultCheckBox, 2);

        var securityNote = new Label
        {
            Text = "密码仅保存到 Windows 凭据管理器。SSH 模式只记录公钥路径，Atlas 不读取私钥。保存配置不会连接或修改仓库。",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(88, 98, 106),
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        _layout.Controls.Add(securityNote, 0, 9);
        _layout.SetColumnSpan(securityNote, 3);

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
        _layout.Controls.Add(buttons, 0, 10);
        _layout.SetColumnSpan(buttons, 3);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        Controls.Add(_layout);

        InitializeSelections(profile);
        if (profile is not null)
        {
            _displayNameTextBox.Text = profile.DisplayName;
            _repositoryUrlTextBox.Text = profile.RepositoryUrl;
            _defaultBranchTextBox.Text = profile.DefaultBranch;
            _usernameTextBox.Text = profile.Username;
            _sshPublicKeyTextBox.Text = profile.SshPublicKeyPath ?? string.Empty;
        }
        else
        {
            _displayNameTextBox.Text = "主 Git 仓库";
            _defaultBranchTextBox.Text = "main";
        }

        _providerComboBox.SelectionChangeCommitted += (_, _) =>
            UpdateProviderDefaults();
        _authenticationComboBox.SelectionChangeCommitted += async (_, _) =>
        {
            UpdateAuthenticationControls();
            if (SelectedAuthenticationMethod == GitAuthenticationMethod.Ssh)
            {
                RefreshDefaultSshKey();
                await PromptToGenerateSshKeyIfMissingAsync();
            }
        };
        Shown += async (_, _) =>
        {
            if (SelectedAuthenticationMethod == GitAuthenticationMethod.Ssh)
            {
                RefreshDefaultSshKey();
                await PromptToGenerateSshKeyIfMissingAsync();
            }
        };

        UpdateProviderDefaults();
        UpdateAuthenticationControls();
    }

    public SaveGitProfileRequest CreateRequest()
    {
        return new SaveGitProfileRequest(
            _profileId,
            _displayNameTextBox.Text,
            SelectedProvider,
            _repositoryUrlTextBox.Text,
            _defaultBranchTextBox.Text,
            SelectedAuthenticationMethod,
            SelectedAuthenticationMethod == GitAuthenticationMethod.Password
                ? _usernameTextBox.Text
                : null,
            SelectedAuthenticationMethod == GitAuthenticationMethod.Password &&
            !string.IsNullOrEmpty(_passwordTextBox.Text)
                ? _passwordTextBox.Text
                : null,
            SelectedAuthenticationMethod == GitAuthenticationMethod.Ssh
                ? _sshPublicKeyTextBox.Text
                : null,
            _isDefaultCheckBox.Checked);
    }

    internal static string GetAuthenticationDisplayName(
        GitAuthenticationMethod authenticationMethod)
    {
        return authenticationMethod switch
        {
            GitAuthenticationMethod.Password => "密码",
            GitAuthenticationMethod.Ssh => "SSH",
            _ => authenticationMethod.ToString()
        };
    }

    private GitHostingProvider SelectedProvider =>
        (_providerComboBox.SelectedItem as GitProviderOption)?.Value
        ?? GitHostingProvider.GitHub;

    private GitAuthenticationMethod SelectedAuthenticationMethod =>
        (_authenticationComboBox.SelectedItem as GitAuthenticationOption)?.Value
        ?? GitAuthenticationMethod.Password;

    private void InitializeSelections(GitProfile? profile)
    {
        _providerComboBox.Items.AddRange(ProviderOptions.Cast<object>().ToArray());
        _providerComboBox.SelectedItem = ProviderOptions.Single(
            option => option.Value == (profile?.Provider ?? GitHostingProvider.GitHub));
        _authenticationComboBox.Items.AddRange(
            AuthenticationOptions.Cast<object>().ToArray());
        _authenticationComboBox.SelectedItem = AuthenticationOptions.Single(
            option => option.Value ==
                (profile?.AuthenticationMethod ?? GitAuthenticationMethod.Password));
    }

    private void AddProviderField(TableLayoutPanel layout, int row)
    {
        _providerComboBox.Dock = DockStyle.Fill;
        _providerComboBox.Margin = new Padding(0, 8, 8, 8);
        _providerComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _providerComboBox.AccessibleName = "Git 托管平台";
        var openWebsiteButton = CreateButton(
            "打开网站",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        openWebsiteButton.Dock = DockStyle.Fill;
        openWebsiteButton.Margin = new Padding(0, 7, 0, 7);
        openWebsiteButton.AccessibleName = "打开 Git 托管平台网站";
        openWebsiteButton.Click += (_, _) => OpenProviderWebsite();
        layout.Controls.Add(CreateFieldLabel("平台"), 0, row);
        layout.Controls.Add(_providerComboBox, 1, row);
        layout.Controls.Add(openWebsiteButton, 2, row);
    }

    private void AddAuthenticationField(TableLayoutPanel layout, int row)
    {
        _authenticationComboBox.Dock = DockStyle.Fill;
        _authenticationComboBox.Margin = new Padding(0, 8, 0, 8);
        _authenticationComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _authenticationComboBox.AccessibleName = "Git 访问方式";
        layout.Controls.Add(CreateFieldLabel("访问方式"), 0, row);
        layout.Controls.Add(_authenticationComboBox, 1, row);
        layout.SetColumnSpan(_authenticationComboBox, 2);
    }

    private void AddSshKeyField(TableLayoutPanel layout, int row)
    {
        _sshPublicKeyTextBox.Dock = DockStyle.Fill;
        _sshPublicKeyTextBox.Margin = new Padding(0, 8, 8, 8);
        _sshPublicKeyTextBox.BorderStyle = BorderStyle.FixedSingle;
        _sshPublicKeyTextBox.ReadOnly = true;
        _sshPublicKeyTextBox.AccessibleName = "SSH 公钥文件";

        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(0, 7, 0, 7)
        };
        ConfigureInlineButton(_selectSshKeyButton, "选择公钥", 76);
        ConfigureInlineButton(_generateSshKeyButton, "生成新密钥", 92);
        _selectSshKeyButton.Click += (_, _) => SelectSshPublicKey();
        _generateSshKeyButton.Click += async (_, _) =>
            await GenerateSshKeyAsync(confirm: true);
        commands.Controls.Add(_selectSshKeyButton);
        commands.Controls.Add(_generateSshKeyButton);

        layout.Controls.Add(_sshPublicKeyLabel, 0, row);
        layout.Controls.Add(_sshPublicKeyTextBox, 1, row);
        layout.Controls.Add(commands, 2, row);
    }

    private static void AddTextField(
        TableLayoutPanel layout,
        int row,
        string labelText,
        TextBox textBox)
    {
        AddTextField(layout, row, CreateFieldLabel(labelText), textBox);
    }

    private static void AddTextField(
        TableLayoutPanel layout,
        int row,
        Label label,
        TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 8, 0, 8);
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.AccessibleName ??= label.Text;
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(textBox, 1, row);
        layout.SetColumnSpan(textBox, 2);
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

    private static void ConfigureInlineButton(Button button, string text, int width)
    {
        button.Text = text;
        button.Size = new Size(width, 32);
        button.Margin = new Padding(0, 0, 6, 0);
        button.BackColor = Color.FromArgb(236, 239, 242);
        button.ForeColor = Color.FromArgb(31, 37, 43);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    private void UpdateProviderDefaults()
    {
        var isSsh = SelectedAuthenticationMethod == GitAuthenticationMethod.Ssh;
        _repositoryUrlTextBox.PlaceholderText = (SelectedProvider, isSsh) switch
        {
            (GitHostingProvider.GitHub, false) =>
                "https://github.com/owner/repository.git",
            (GitHostingProvider.Gitee, false) =>
                "https://gitee.com/owner/repository.git",
            (GitHostingProvider.GitHub, true) =>
                "git@github.com:owner/repository.git",
            _ => "git@gitee.com:owner/repository.git"
        };
        if (_profileId is null &&
            (_defaultBranchTextBox.Text is "main" or "master"))
        {
            _defaultBranchTextBox.Text = SelectedProvider == GitHostingProvider.GitHub
                ? "main"
                : "master";
        }
    }

    private void UpdateAuthenticationControls()
    {
        var passwordEnabled =
            SelectedAuthenticationMethod == GitAuthenticationMethod.Password;
        _usernameLabel.Enabled = passwordEnabled;
        _usernameTextBox.Enabled = passwordEnabled;
        _passwordLabel.Enabled = passwordEnabled;
        _passwordTextBox.Enabled = passwordEnabled;
        _usernameLabel.Visible = passwordEnabled;
        _usernameTextBox.Visible = passwordEnabled;
        _passwordLabel.Visible = passwordEnabled;
        _passwordTextBox.Visible = passwordEnabled;
        _layout.RowStyles[UsernameRowIndex].Height = passwordEnabled
            ? FieldRowHeight
            : 0;
        _layout.RowStyles[PasswordRowIndex].Height = passwordEnabled
            ? FieldRowHeight
            : 0;
        _sshPublicKeyLabel.Enabled = !passwordEnabled;
        _sshPublicKeyTextBox.Enabled = !passwordEnabled;
        _selectSshKeyButton.Enabled = !passwordEnabled;
        _generateSshKeyButton.Enabled = !passwordEnabled;
        _passwordTextBox.PlaceholderText = _profileId is null ||
            _originalAuthenticationMethod == GitAuthenticationMethod.Ssh
                ? "必填；GitHub 可使用个人访问令牌作为密码"
                : "留空保留现有密码";
        UpdateProviderDefaults();
    }

    private void RefreshDefaultSshKey()
    {
        if (IsUsablePublicKeyPath(_sshPublicKeyTextBox.Text))
        {
            return;
        }

        var sshDirectory = SshKeySupport.GetDefaultSshDirectory();
        var pair = SshKeySupport.FindDefaultKeyPair(sshDirectory);
        _sshPublicKeyTextBox.Text = pair?.PublicKeyPath ?? string.Empty;
        _sshPublicKeyTextBox.PlaceholderText = pair is null
            ? $"未在 {sshDirectory} 中找到公钥"
            : string.Empty;
    }

    private async Task PromptToGenerateSshKeyIfMissingAsync()
    {
        if (IsUsablePublicKeyPath(_sshPublicKeyTextBox.Text))
        {
            return;
        }

        var sshDirectory = SshKeySupport.GetDefaultSshDirectory();
        if (MessageBox.Show(
                this,
                $"未在以下目录找到可用的 SSH 公钥和私钥：{Environment.NewLine}{sshDirectory}{Environment.NewLine}{Environment.NewLine}是否打开系统 ssh-keygen，由您生成新的 Ed25519 密钥？",
                "需要 SSH 密钥",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information) == DialogResult.Yes)
        {
            await GenerateSshKeyAsync(confirm: false);
        }
    }

    private async Task GenerateSshKeyAsync(bool confirm)
    {
        var sshDirectory = SshKeySupport.GetDefaultSshDirectory();
        var keyPair = SshKeySupport.CreateUnusedKeyPairPaths(sshDirectory);
        if (confirm &&
            MessageBox.Show(
                this,
                $"Atlas 将打开系统 ssh-keygen，并在以下未占用位置生成新密钥：{Environment.NewLine}{keyPair.PrivateKeyPath}{Environment.NewLine}{Environment.NewLine}请由您设置密钥口令。Atlas 不会覆盖、读取或保存已有私钥。是否继续？",
                "生成新 SSH 密钥",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) != DialogResult.OK)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(sshDirectory);
            using var process = Process.Start(
                SshKeySupport.CreateSshKeyGenerationStartInfo(
                    _usernameTextBox.Text,
                    keyPair.PrivateKeyPath));
            if (process is null)
            {
                throw new InvalidOperationException("无法启动 ssh-keygen。");
            }

            Enabled = false;
            await process.WaitForExitAsync();
            Enabled = true;
            Activate();
            if (process.ExitCode == 0 &&
                IsUsablePublicKeyPath(keyPair.PublicKeyPath))
            {
                _sshPublicKeyTextBox.Text = keyPair.PublicKeyPath;
                _sshPublicKeyTextBox.PlaceholderText = string.Empty;
            }
            else
            {
                RefreshDefaultSshKey();
            }
            if (!IsUsablePublicKeyPath(_sshPublicKeyTextBox.Text))
            {
                MessageBox.Show(
                    this,
                    "尚未发现可用密钥。您可以再次生成，或点击“选择公钥”选择已有的 .pub 文件。",
                    "未发现 SSH 密钥",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            Enabled = true;
            MessageBox.Show(
                this,
                $"无法打开 ssh-keygen：{exception.Message}{Environment.NewLine}{Environment.NewLine}请确认已安装 Windows OpenSSH 客户端。",
                "生成新 SSH 密钥失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SelectSshPublicKey()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 SSH 公钥",
            Filter = "SSH 公钥 (*.pub)|*.pub|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(SshKeySupport.GetDefaultSshDirectory())
                ? SshKeySupport.GetDefaultSshDirectory()
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _sshPublicKeyTextBox.Text = dialog.FileName;
        }
    }

    private void OpenProviderWebsite()
    {
        try
        {
            using var process = Process.Start(
                SshKeySupport.CreateOpenWebsiteStartInfo(SelectedProvider));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "无法打开网站",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool IsUsablePublicKeyPath(string? publicKeyPath)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPath) ||
            !publicKeyPath.EndsWith(".pub", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(publicKeyPath))
        {
            return false;
        }

        return File.Exists(publicKeyPath[..^4]);
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

    internal sealed record GitAuthenticationOption(
        GitAuthenticationMethod Value,
        string Label)
    {
        public override string ToString() => Label;
    }
}
