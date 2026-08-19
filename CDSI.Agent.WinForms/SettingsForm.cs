using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.WinForms;

public sealed class SettingsForm : Form
{
    private readonly WorkspaceApplicationService _workspaceService;
    private readonly ScanRootManagementService _scanRootService;
    private readonly TextBox _workspacePathTextBox = new();
    private readonly DataGridView _rootsGrid = new();
    private readonly Button _toggleRootButton = new();
    private readonly Button _removeRootButton = new();
    private readonly Label _workspaceStatusLabel = new();

    public SettingsForm(
        WorkspaceApplicationService workspaceService,
        ScanRootManagementService scanRootService)
    {
        _workspaceService = workspaceService;
        _scanRootService = scanRootService;

        Text = "CDSI Atlas 设置";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(720, 460);
        ShowInTaskbar = false;
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 5)
        };
        tabs.TabPages.Add(CreateWorkspacePage());
        tabs.TabPages.Add(CreateScanRootsPage());

        var closeButton = CreateButton(
            "关闭",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        closeButton.DialogResult = DialogResult.OK;
        closeButton.Anchor = AnchorStyles.Right;
        closeButton.Size = new Size(96, 32);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Color.White
        };
        footer.Controls.Add(closeButton);

        Controls.Add(tabs);
        Controls.Add(footer);
        Shown += SettingsForm_Shown;
    }

    private TabPage CreateWorkspacePage()
    {
        var page = new TabPage("工作目录")
        {
            BackColor = Color.White,
            Padding = new Padding(24)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            ColumnCount = 3,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

        var label = new Label
        {
            Text = "受管工作目录",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(31, 37, 43)
        };
        layout.Controls.Add(label, 0, 0);
        layout.SetColumnSpan(label, 3);

        _workspacePathTextBox.Dock = DockStyle.Fill;
        _workspacePathTextBox.Margin = new Padding(0, 5, 8, 5);
        _workspacePathTextBox.AccessibleName = "受管工作目录路径";
        layout.Controls.Add(_workspacePathTextBox, 0, 1);

        var browseButton = CreateButton(
            "浏览",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));
        browseButton.Margin = new Padding(4, 5, 4, 5);
        browseButton.Click += WorkspaceBrowseButton_Click;
        layout.Controls.Add(browseButton, 1, 1);

        var saveButton = CreateButton(
            "应用",
            Color.FromArgb(24, 121, 78),
            Color.White);
        saveButton.Margin = new Padding(4, 5, 0, 5);
        saveButton.Click += WorkspaceSaveButton_Click;
        layout.Controls.Add(saveButton, 2, 1);

        _workspaceStatusLabel.Dock = DockStyle.Fill;
        _workspaceStatusLabel.ForeColor = Color.FromArgb(88, 98, 106);
        _workspaceStatusLabel.Padding = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_workspaceStatusLabel, 0, 2);
        layout.SetColumnSpan(_workspaceStatusLabel, 3);

        page.Controls.Add(layout);
        return page;
    }

    private TabPage CreateScanRootsPage()
    {
        var page = new TabPage("扫描目录")
        {
            BackColor = Color.White,
            Padding = new Padding(16)
        };
        ConfigureRootsGrid();

        var addButton = CreateButton(
            "添加目录",
            Color.FromArgb(24, 121, 78),
            Color.White);
        addButton.Click += AddRootButton_Click;
        addButton.Size = new Size(104, 32);

        _toggleRootButton.Text = "停用";
        _toggleRootButton.Size = new Size(88, 32);
        _toggleRootButton.FlatStyle = FlatStyle.Flat;
        _toggleRootButton.Click += ToggleRootButton_Click;

        _removeRootButton.Text = "移除";
        _removeRootButton.Size = new Size(88, 32);
        _removeRootButton.FlatStyle = FlatStyle.Flat;
        _removeRootButton.ForeColor = Color.FromArgb(137, 49, 49);
        _removeRootButton.Click += RemoveRootButton_Click;

        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 8)
        };
        commands.Controls.Add(addButton);
        commands.Controls.Add(_toggleRootButton);
        commands.Controls.Add(_removeRootButton);

        page.Controls.Add(_rootsGrid);
        page.Controls.Add(commands);
        return page;
    }

    private void ConfigureRootsGrid()
    {
        _rootsGrid.Dock = DockStyle.Fill;
        _rootsGrid.BackgroundColor = Color.White;
        _rootsGrid.BorderStyle = BorderStyle.FixedSingle;
        _rootsGrid.ReadOnly = true;
        _rootsGrid.AllowUserToAddRows = false;
        _rootsGrid.AllowUserToDeleteRows = false;
        _rootsGrid.AllowUserToResizeRows = false;
        _rootsGrid.AutoGenerateColumns = false;
        _rootsGrid.MultiSelect = false;
        _rootsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _rootsGrid.RowHeadersVisible = false;
        _rootsGrid.RowTemplate.Height = 30;
        _rootsGrid.AccessibleName = "外部扫描目录列表";
        _rootsGrid.ColumnHeadersHeight = 36;
        _rootsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "目录",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 320,
            FillWeight = 100
        });
        _rootsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "状态",
            Width = 90
        });
        _rootsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "最近扫描",
            Width = 150
        });
        _rootsGrid.SelectionChanged += (_, _) => UpdateRootCommands();
    }

    private async void SettingsForm_Shown(object? sender, EventArgs e)
    {
        await RefreshWorkspaceAsync();
        await RefreshRootsAsync();
    }

    private async Task RefreshWorkspaceAsync()
    {
        var workspace = await _workspaceService.GetAsync();
        _workspacePathTextBox.Text =
            workspace?.Path ?? WorkspaceApplicationService.GetSuggestedDefaultPath();
        _workspaceStatusLabel.Text = workspace is null
            ? "尚未配置"
            : $"Inbox: {workspace.InboxPath}";
    }

    private async Task RefreshRootsAsync()
    {
        var roots = await _scanRootService.ListExternalAsync();
        _rootsGrid.Rows.Clear();
        foreach (var root in roots)
        {
            var index = _rootsGrid.Rows.Add(
                root.Path,
                FormatRootStatus(root),
                root.LastScannedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "尚未扫描");
            _rootsGrid.Rows[index].Tag = root;
        }

        UpdateRootCommands();
    }

    private void WorkspaceBrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择 CDSI 工作目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(_workspacePathTextBox.Text)
                ? _workspacePathTextBox.Text
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _workspacePathTextBox.Text = dialog.SelectedPath;
        }
    }

    private async void WorkspaceSaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var path = _workspacePathTextBox.Text.Trim();
            var current = await _workspaceService.GetAsync();
            if (current is not null &&
                !string.Equals(
                    Path.GetFullPath(current.Path),
                    Path.GetFullPath(path),
                    StringComparison.OrdinalIgnoreCase) &&
                MessageBox.Show(
                    this,
                    "切换后不会搬移或删除旧工作目录中的文件。继续吗？",
                    "更改工作目录",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            var result = await _workspaceService.ConfigureAsync(path);
            _workspacePathTextBox.Text = result.Workspace.Path;
            _workspaceStatusLabel.Text = $"Inbox: {result.Layout.InboxPath}";
        }
        catch (Exception exception)
        {
            ShowError("无法设置工作目录", exception);
        }
    }
    private async void AddRootButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "添加只读扫描目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var result = await _scanRootService.AddExternalAsync(dialog.SelectedPath);
            await RefreshRootsAsync();
            if (result.Warnings.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, result.Warnings),
                    "目录重叠",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
        catch (Exception exception)
        {
            ShowError("无法添加扫描目录", exception);
        }
    }

    private async void ToggleRootButton_Click(object? sender, EventArgs e)
    {
        if (_rootsGrid.CurrentRow?.Tag is not ScanRoot root)
        {
            return;
        }

        try
        {
            await _scanRootService.SetEnabledAsync(root.Id, !root.Enabled);
            await RefreshRootsAsync();
        }
        catch (Exception exception)
        {
            ShowError("无法更新扫描目录", exception);
        }
    }

    private async void RemoveRootButton_Click(object? sender, EventArgs e)
    {
        if (_rootsGrid.CurrentRow?.Tag is not ScanRoot root ||
            MessageBox.Show(
                this,
                "移除后停止扫描此目录，已有资产和位置记录会保留。",
                "移除扫描目录",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _scanRootService.RemoveAsync(root.Id);
            await RefreshRootsAsync();
        }
        catch (Exception exception)
        {
            ShowError("无法移除扫描目录", exception);
        }
    }

    private void UpdateRootCommands()
    {
        var root = _rootsGrid.CurrentRow?.Tag as ScanRoot;
        _toggleRootButton.Enabled = root is not null;
        _removeRootButton.Enabled = root is not null;
        _toggleRootButton.Text = root?.Enabled == true ? "停用" : "启用";
    }

    private static string FormatRootStatus(ScanRoot root)
    {
        if (!root.Enabled)
        {
            return "已停用";
        }

        return root.Status switch
        {
            ScanRootStatus.Active => "正常",
            ScanRootStatus.Unavailable => "不可用",
            ScanRootStatus.Error => "有错误",
            _ => root.Status.ToString()
        };
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

    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(
            this,
            exception.Message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
