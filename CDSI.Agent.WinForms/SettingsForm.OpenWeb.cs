using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.OpenWeb;

namespace CDSI.Agent.WinForms;

public sealed partial class SettingsForm
{
    private readonly OpenWebSettingsService _openWebSettingsService;
    private readonly DataGridView _openWebSourcesGrid = new();
    private readonly Button _editOpenWebSourceButton = new();
    private readonly Button _defaultOpenWebSourceButton = new();
    private readonly Button _deleteOpenWebSourceButton = new();

    private TabPage CreateOpenWebPage()
    {
        var page = new TabPage("OpenWeb")
        {
            BackColor = Color.White,
            Padding = new Padding(16)
        };
        ConfigureOpenWebSourcesGrid();

        var addButton = CreateButton(
            "添加源站",
            Color.FromArgb(24, 121, 78),
            Color.White);
        addButton.Size = new Size(104, 32);
        addButton.AccessibleName = "添加 OpenWeb 源站";
        addButton.Click += AddOpenWebSourceButton_Click;

        _editOpenWebSourceButton.Text = "编辑";
        _editOpenWebSourceButton.Size = new Size(88, 32);
        _editOpenWebSourceButton.FlatStyle = FlatStyle.Flat;
        _editOpenWebSourceButton.Click += EditOpenWebSourceButton_Click;

        _defaultOpenWebSourceButton.Text = "设为默认";
        _defaultOpenWebSourceButton.Size = new Size(88, 32);
        _defaultOpenWebSourceButton.FlatStyle = FlatStyle.Flat;
        _defaultOpenWebSourceButton.Click += DefaultOpenWebSourceButton_Click;

        _deleteOpenWebSourceButton.Text = "删除";
        _deleteOpenWebSourceButton.Size = new Size(88, 32);
        _deleteOpenWebSourceButton.FlatStyle = FlatStyle.Flat;
        _deleteOpenWebSourceButton.ForeColor = Color.FromArgb(137, 49, 49);
        _deleteOpenWebSourceButton.Click += DeleteOpenWebSourceButton_Click;

        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 46,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 8)
        };
        commands.Controls.Add(addButton);
        commands.Controls.Add(_editOpenWebSourceButton);
        commands.Controls.Add(_defaultOpenWebSourceButton);
        commands.Controls.Add(_deleteOpenWebSourceButton);

        page.Controls.Add(_openWebSourcesGrid);
        page.Controls.Add(commands);
        return page;
    }

    private void ConfigureOpenWebSourcesGrid()
    {
        _openWebSourcesGrid.Dock = DockStyle.Fill;
        _openWebSourcesGrid.BackgroundColor = Color.White;
        _openWebSourcesGrid.BorderStyle = BorderStyle.FixedSingle;
        _openWebSourcesGrid.ReadOnly = true;
        _openWebSourcesGrid.AllowUserToAddRows = false;
        _openWebSourcesGrid.AllowUserToDeleteRows = false;
        _openWebSourcesGrid.AllowUserToResizeRows = false;
        _openWebSourcesGrid.AutoGenerateColumns = false;
        _openWebSourcesGrid.MultiSelect = false;
        _openWebSourcesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _openWebSourcesGrid.RowHeadersVisible = false;
        _openWebSourcesGrid.RowTemplate.Height = 30;
        _openWebSourcesGrid.ColumnHeadersHeight = 36;
        _openWebSourcesGrid.AccessibleName = "OpenWeb 源站列表";
        _openWebSourcesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "名称",
            Width = 150
        });
        _openWebSourcesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "源站域名",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 220,
            FillWeight = 100
        });
        _openWebSourcesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "WordPress 用户名",
            Width = 160
        });
        _openWebSourcesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "默认",
            Width = 72
        });
        _openWebSourcesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "凭据",
            Width = 82
        });
        _openWebSourcesGrid.SelectionChanged += (_, _) =>
            UpdateOpenWebSourceCommands();
    }

    private async Task RefreshOpenWebAsync()
    {
        var sources = await _openWebSettingsService.ListAsync();
        _openWebSourcesGrid.Rows.Clear();
        foreach (var configured in sources)
        {
            var source = configured.Source;
            var index = _openWebSourcesGrid.Rows.Add(
                source.DisplayName,
                source.OriginDomain,
                source.WordPressUsername,
                source.IsDefault ? "是" : string.Empty,
                configured.HasApplicationPassword ? "已保存" : "缺失");
            _openWebSourcesGrid.Rows[index].Tag = configured;
        }

        UpdateOpenWebSourceCommands();
    }

    private async void AddOpenWebSourceButton_Click(object? sender, EventArgs e)
    {
        await ShowOpenWebSourceDialogAsync(null);
    }

    private async void EditOpenWebSourceButton_Click(object? sender, EventArgs e)
    {
        if (_openWebSourcesGrid.CurrentRow?.Tag is ConfiguredOpenWebSource configured)
        {
            await ShowOpenWebSourceDialogAsync(configured.Source);
        }
    }

    private async Task ShowOpenWebSourceDialogAsync(OpenWebSource? source)
    {
        using var dialog = new OpenWebSourceDialog(source);
        while (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                await _openWebSettingsService.SaveAsync(dialog.CreateRequest());
                await RefreshOpenWebAsync();
                return;
            }
            catch (Exception exception)
            {
                ShowError("无法保存 OpenWeb 源站", exception);
                dialog.DialogResult = DialogResult.None;
            }
        }
    }

    private async void DefaultOpenWebSourceButton_Click(object? sender, EventArgs e)
    {
        if (_openWebSourcesGrid.CurrentRow?.Tag is not ConfiguredOpenWebSource configured ||
            configured.Source.IsDefault)
        {
            return;
        }

        try
        {
            await _openWebSettingsService.SetDefaultAsync(configured.Source.Id);
            await RefreshOpenWebAsync();
        }
        catch (Exception exception)
        {
            ShowError("无法设置默认 OpenWeb 源站", exception);
        }
    }

    private async void DeleteOpenWebSourceButton_Click(object? sender, EventArgs e)
    {
        if (_openWebSourcesGrid.CurrentRow?.Tag is not ConfiguredOpenWebSource configured ||
            MessageBox.Show(
                this,
                "将删除本机源站配置和对应的 Windows 凭据，不会删除 WordPress 中的文章。",
                "删除 OpenWeb 源站",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await _openWebSettingsService.DeleteAsync(configured.Source.Id);
            await RefreshOpenWebAsync();
        }
        catch (Exception exception)
        {
            ShowError("无法删除 OpenWeb 源站", exception);
        }
    }

    private void UpdateOpenWebSourceCommands()
    {
        var source = (_openWebSourcesGrid.CurrentRow?.Tag as ConfiguredOpenWebSource)?.Source;
        _editOpenWebSourceButton.Enabled = source is not null;
        _defaultOpenWebSourceButton.Enabled = source is not null && !source.IsDefault;
        _deleteOpenWebSourceButton.Enabled = source is not null;
    }
}
