using System.Reflection;
using CDSI.Agent.Application.Collections;
using CDSI.Agent.Application.Metadata;
using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Application.Fingerprints;
using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Storage;
using CDSI.Agent.Application.Transfers;
using CDSI.Agent.Application.Workspaces;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Fingerprints;
using CDSI.Agent.Core.Metadata;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Transfers;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm : Form
{
    private readonly ScanApplicationService _scanService;
    private readonly WorkspaceApplicationService _workspaceService;
    private readonly ScanRootManagementService _scanRootService;
    private readonly ObjectStorageProfileService _storageService;
    private readonly OpenWebSettingsService _openWebSettingsService;
    private readonly OpenWebArticlePublishingService _openWebPublishingService;
    private readonly ObjectStorageBackupService _objectStorageBackupService;
    private readonly ManagedAssetTransferService _transferService;
    private readonly Label _scopeLabel = new();
    private readonly FingerprintApplicationService _fingerprintService;
    private readonly MetadataExtractionApplicationService _metadataService;
    private readonly CheckBox _fullVerificationCheckBox = new();
    private readonly Button _settingsButton = new();
    private readonly Button _scanButton = new();
    private readonly Button _cancelButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _progressLabel = new();
    private readonly Label _currentPathLabel = new();
    private readonly Label _fileCountValueLabel = new();
    private readonly Label _totalSizeValueLabel = new();
    private readonly Label _videoCountValueLabel = new();
    private readonly Label _videoDurationValueLabel = new();
    private readonly Label _assetDetailTitleLabel = new();
    private readonly Label _assetDetailSummaryLabel = new();
    private readonly DataGridView _assetGrid = new();
    private readonly DataGridView _duplicateGrid = new();
    private readonly ContextMenuStrip _assetContextMenu = new();
    private readonly ToolStripMenuItem _copyToWorkspaceMenuItem = new();
    private readonly ToolStripMenuItem _moveToWorkspaceMenuItem = new();
    private readonly ToolStripMenuItem _backupToOssMenuItem = new();
    private readonly TabPage _assetsTabPage = new("资产");
    private readonly TabPage _duplicatesTabPage = new("精确重复");
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly ToolStripStatusLabel _databaseStatusLabel = new();
    private CancellationTokenSource? _scanCancellation;

    public MainForm(
        ScanApplicationService scanService,
        FingerprintApplicationService fingerprintService,
        MetadataExtractionApplicationService metadataService,
        WorkspaceApplicationService workspaceService,
        ScanRootManagementService scanRootService,
        LocalVolumeReconciliationService volumeReconciliationService,
        ObjectStorageProfileService storageService,
        OpenWebSettingsService openWebSettingsService,
        OpenWebArticlePublishingService openWebPublishingService,
        ObjectStorageBackupService objectStorageBackupService,
        AssetCollectionService assetCollectionService,
        ManagedAssetTransferService transferService,
        string dataDirectory)
    {
        _scanService = scanService;
        _fingerprintService = fingerprintService;
        _metadataService = metadataService;
        _workspaceService = workspaceService;
        _scanRootService = scanRootService;
        _volumeReconciliationService = volumeReconciliationService;
        _storageService = storageService;
        _openWebSettingsService = openWebSettingsService;
        _openWebPublishingService = openWebPublishingService;
        _objectStorageBackupService = objectStorageBackupService;
        _assetCollectionService = assetCollectionService;
        _transferService = transferService;
        InitializeLayout(dataDirectory);

        Shown += MainForm_Shown;
        FormClosing += (_, _) =>
        {
            _scanCancellation?.Cancel();
            StopLocalVolumeMonitoring();
        };
    }

    private void InitializeLayout(string dataDirectory)
    {
        SuspendLayout();

        var applicationVersion = GetApplicationVersion();
        Text = $"CDSI Atlas v{applicationVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(920, 600);
        Size = new Size(1180, 760);
        BackColor = Color.FromArgb(247, 248, 250);
        Font = new Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            BackColor = BackColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(31, 37, 43),
            Padding = new Padding(28, 13, 28, 10)
        };
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "CDSI Atlas",
            Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = Color.White,
            Location = new Point(25, 11)
        });
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "本地资产索引",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(179, 190, 199),
            Location = new Point(28, 45)
        });
        header.Controls.Add(new Label
        {
            AutoSize = false,
            Dock = DockStyle.Right,
            Width = 96,
            Text = $"v{applicationVersion}",
            TextAlign = ContentAlignment.TopRight,
            Padding = new Padding(0, 6, 0, 0),
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(179, 190, 199),
            AccessibleName = "应用版本"
        });
        mainLayout.Controls.Add(header, 0, 0);

        var commandPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            Padding = new Padding(28, 14, 28, 10),
            BackColor = Color.White
        };
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        commandPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        _scopeLabel.Dock = DockStyle.Fill;
        _scopeLabel.Margin = new Padding(0, 0, 10, 0);
        _scopeLabel.Text = "正在读取工作目录和扫描目录";
        _scopeLabel.TextAlign = ContentAlignment.MiddleLeft;
        _scopeLabel.AutoEllipsis = true;
        _scopeLabel.ForeColor = Color.FromArgb(52, 61, 69);
        _scopeLabel.AccessibleName = "当前扫描范围";

        ConfigureCommandButton(_settingsButton, "设置", Color.FromArgb(236, 239, 242), Color.FromArgb(31, 37, 43));
        ConfigureCommandButton(_scanButton, "开始扫描", Color.FromArgb(24, 121, 78), Color.White);
        _fullVerificationCheckBox.Text = "完整校验";
        _fullVerificationCheckBox.AutoSize = true;
        _fullVerificationCheckBox.Dock = DockStyle.Fill;
        _fullVerificationCheckBox.Margin = new Padding(8, 0, 4, 0);
        _fullVerificationCheckBox.TextAlign = ContentAlignment.MiddleLeft;
        _fullVerificationCheckBox.ForeColor = Color.FromArgb(52, 61, 69);

        ConfigureCommandButton(_cancelButton, "取消", Color.FromArgb(236, 239, 242), Color.FromArgb(137, 49, 49));
        _cancelButton.Enabled = false;

        _settingsButton.Click += SettingsButton_Click;
        _scanButton.Click += ScanButton_Click;
        _cancelButton.Click += (_, _) => _scanCancellation?.Cancel();

        commandPanel.Controls.Add(_scopeLabel, 0, 0);
        commandPanel.Controls.Add(_settingsButton, 1, 0);
        commandPanel.Controls.Add(_fullVerificationCheckBox, 2, 0);
        commandPanel.Controls.Add(_scanButton, 3, 0);
        commandPanel.Controls.Add(_cancelButton, 4, 0);
        mainLayout.Controls.Add(commandPanel, 0, 1);

        var progressPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(28, 7, 28, 7),
            BackColor = Color.FromArgb(247, 248, 250)
        };
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        progressPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        _progressLabel.AutoSize = true;
        _progressLabel.Text = "就绪";
        _progressLabel.Font = new Font("Segoe UI Semibold", 9F);
        _progressLabel.ForeColor = Color.FromArgb(52, 61, 69);

        _currentPathLabel.Dock = DockStyle.Fill;
        _currentPathLabel.Text = "尚未扫描";
        _currentPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        _currentPathLabel.AutoEllipsis = true;
        _currentPathLabel.ForeColor = Color.FromArgb(101, 111, 120);

        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Height = 5;
        _progressBar.Style = ProgressBarStyle.Blocks;

        progressPanel.Controls.Add(_progressLabel, 0, 0);
        progressPanel.SetColumnSpan(_progressLabel, 2);
        progressPanel.Controls.Add(_progressBar, 0, 1);
        progressPanel.Controls.Add(_currentPathLabel, 1, 1);
        mainLayout.Controls.Add(progressPanel, 0, 2);

        ConfigureAssetGrid();
        ConfigureDuplicateGrid();
        ConfigureAssetDirectoryTab();
        ConfigureAssetCollectionTab();
        _assetGrid.SelectionChanged += AssetGrid_SelectionChanged;

        _assetsTabPage.Padding = new Padding(0);
        _assetsTabPage.BackColor = Color.White;
        var assetTabLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        assetTabLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        assetTabLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        assetTabLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        assetTabLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        assetTabLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        assetTabLayout.Controls.Add(
            CreateStatisticsPanel(
                _fileCountValueLabel,
                _totalSizeValueLabel,
                _videoCountValueLabel,
                _videoDurationValueLabel),
            0,
            0);
        assetTabLayout.Controls.Add(ConfigureAssetFilterPanel(), 0, 1);
        assetTabLayout.Controls.Add(_assetGrid, 0, 2);
        assetTabLayout.Controls.Add(ConfigureAssetPagination(), 0, 3);
        assetTabLayout.Controls.Add(
            CreateAssetDetailsPanel(
                _assetDetailTitleLabel,
                _assetDetailSummaryLabel),
            0,
            4);
        _assetsTabPage.Controls.Add(assetTabLayout);
        _duplicatesTabPage.Padding = new Padding(0);
        _duplicatesTabPage.BackColor = Color.White;
        _duplicatesTabPage.Controls.Add(_duplicateGrid);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 5)
        };
        tabs.TabPages.Add(_assetsTabPage);
        tabs.TabPages.Add(_assetDirectoriesTabPage);
        tabs.TabPages.Add(_duplicatesTabPage);
        tabs.TabPages.Add(_collectionsTabPage);

        var gridHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 8, 28, 18),
            BackColor = BackColor
        };
        gridHost.Controls.Add(tabs);
        mainLayout.Controls.Add(gridHost, 0, 3);

        var statusStrip = new StatusStrip
        {
            SizingGrip = false,
            BackColor = Color.White,
            Padding = new Padding(20, 0, 20, 0)
        };
        _statusLabel.Text = "正在初始化";
        _statusLabel.ForeColor = Color.FromArgb(72, 81, 89);
        _databaseStatusLabel.Text = $"数据目录: {dataDirectory}";
        _databaseStatusLabel.Spring = true;
        _databaseStatusLabel.TextAlign = ContentAlignment.MiddleRight;
        _databaseStatusLabel.ForeColor = Color.FromArgb(112, 121, 129);
        statusStrip.Items.Add(_statusLabel);
        statusStrip.Items.Add(_databaseStatusLabel);

        Controls.Add(mainLayout);
        Controls.Add(statusStrip);
        ResumeLayout();
    }

    private static void ConfigureCommandButton(
        Button button,
        string text,
        Color background,
        Color foreground)
    {
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(4, 0, 4, 0);
        button.Text = text;
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    internal static TableLayoutPanel CreateStatisticsPanel(
        Label fileCountValueLabel,
        Label totalSizeValueLabel,
        Label videoCountValueLabel,
        Label videoDurationValueLabel)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = Padding.Empty,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = Color.White
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        for (var column = 0; column < panel.ColumnCount; column++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        panel.Controls.Add(CreateStatisticItem("文件总数", fileCountValueLabel), 0, 0);
        panel.Controls.Add(CreateStatisticItem("占用空间", totalSizeValueLabel), 1, 0);
        panel.Controls.Add(CreateStatisticItem("视频文件", videoCountValueLabel), 2, 0);
        panel.Controls.Add(CreateStatisticItem("视频总时长", videoDurationValueLabel), 3, 0);
        return panel;
    }

    internal static TableLayoutPanel CreateAssetDetailsPanel(
        Label titleLabel,
        Label summaryLabel)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = Padding.Empty,
            Padding = new Padding(8, 6, 8, 8),
            BackColor = Color.White
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        panel.Controls.Add(CreateDetailHeader("资产详情"), 0, 0);

        var summaryPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        summaryPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        summaryPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        summaryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Margin = Padding.Empty;
        titleLabel.Text = "未选择资产";
        titleLabel.AutoEllipsis = true;
        titleLabel.Font = new Font("Segoe UI Semibold", 10F);
        titleLabel.ForeColor = Color.FromArgb(31, 37, 43);
        titleLabel.AccessibleName = "资产标题";

        summaryLabel.Dock = DockStyle.Fill;
        summaryLabel.Margin = Padding.Empty;
        summaryLabel.Text = string.Empty;
        summaryLabel.AutoEllipsis = true;
        summaryLabel.ForeColor = Color.FromArgb(88, 98, 106);
        summaryLabel.AccessibleName = "资产摘要";


        summaryPanel.Controls.Add(titleLabel, 0, 0);
        summaryPanel.Controls.Add(summaryLabel, 0, 1);
        panel.Controls.Add(summaryPanel, 0, 1);
        return panel;
    }

    private static Label CreateDetailHeader(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Color.FromArgb(112, 121, 129)
        };
    }
    private static TableLayoutPanel CreateStatisticItem(string title, Label valueLabel)
    {
        var item = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = new Padding(4, 0, 4, 0),
            Padding = new Padding(8, 1, 8, 1),
            BackColor = Color.White
        };
        item.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        item.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        item.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(112, 121, 129)
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Margin = Padding.Empty;
        valueLabel.Text = "0";
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        valueLabel.AutoEllipsis = true;
        valueLabel.Font = new Font("Segoe UI Semibold", 10.5F);
        valueLabel.ForeColor = Color.FromArgb(31, 37, 43);
        valueLabel.AccessibleName = title;

        item.Controls.Add(titleLabel, 0, 0);
        item.Controls.Add(valueLabel, 0, 1);
        return item;
    }

    private void ConfigureAssetGrid()
    {
        ConfigureGrid(_assetGrid);
        EnableAssetMultiSelection(_assetGrid);
        ConfigureAssetGridColumns(_assetGrid);
        EnableFreeColumnResizing(_assetGrid);
        _assetGrid.Sorted += (_, _) => UpdateAssetRowNumbers(
            _assetGrid,
            CalculateAssetPagination(
                _assetTotalItems,
                _assetPageSize,
                _assetPageIndex).FirstItem);
        ConfigureAssetContextMenu();
    }

    internal static void ConfigureAssetGridColumns(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.Columns.Add(CreateRowNumberColumn());
        grid.Columns.Add(CreateAssetIdColumn());
        grid.Columns.Add(CreateColumn("文件", 220, DataGridViewAutoSizeColumnMode.Fill, 24));
        grid.Columns.Add(CreateColumn("类型", 125));
        grid.Columns.Add(CreateFileSizeColumn());
        grid.Columns.Add(CreateColumn("修改时间", 145));
        var indexedAtColumn = CreateColumn("索引时间", 145);
        indexedAtColumn.Name = "IndexedAt";
        grid.Columns.Add(indexedAtColumn);
        grid.Columns.Add(CreateColumn(
            "位置",
            320,
            DataGridViewAutoSizeColumnMode.Fill,
            42));
        grid.Columns.Add(CreateColumn(
            "媒体信息",
            220,
            DataGridViewAutoSizeColumnMode.Fill,
            34,
            minimumWidth: 220));
        grid.Columns.Add(CreateObjectStorageStatusColumn());
        grid.Columns.Add(CreateColumn("状态", 80));
    }

    internal static void EnableAssetMultiSelection(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.MultiSelect = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
    }

    internal static void EnableFreeColumnResizing(DataGridView grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        grid.AllowUserToResizeColumns = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.ScrollBars = ScrollBars.Both;

        foreach (DataGridViewColumn column in grid.Columns)
        {
            var initialWidth = column.Width;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = 40;
            column.Width = Math.Max(initialWidth, column.MinimumWidth);
            column.Resizable = DataGridViewTriState.True;
        }
    }

    private void ConfigureDuplicateGrid()
    {
        ConfigureGrid(_duplicateGrid);
        _duplicateGrid.Columns.Add(CreateColumn("组", 60));
        _duplicateGrid.Columns.Add(CreateColumn("SHA-256", 125));
        _duplicateGrid.Columns.Add(CreateColumn("文件", 220, DataGridViewAutoSizeColumnMode.Fill, 24));
        _duplicateGrid.Columns.Add(CreateFileSizeColumn());
        _duplicateGrid.Columns.Add(CreateColumn("位置", 360, DataGridViewAutoSizeColumnMode.Fill, 48));
        _duplicateGrid.Columns.Add(CreateColumn("状态", 80));
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AutoGenerateColumns = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 30;
        grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(239, 242, 244);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(52, 61, 69);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 227);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 37, 43);
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.GridColor = Color.FromArgb(229, 232, 235);
        grid.CellFormatting += Grid_CellFormatting;
    }

    private static DataGridViewColumn CreateColumn(
        string title,
        int width,
        DataGridViewAutoSizeColumnMode sizeMode = DataGridViewAutoSizeColumnMode.None,
        float fillWeight = 100,
        int? minimumWidth = null)
    {
        return new DataGridViewTextBoxColumn
        {
            HeaderText = title,
            Width = width,
            MinimumWidth = minimumWidth ?? Math.Min(width, 80),
            AutoSizeMode = sizeMode,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.Automatic
        };
    }

    internal static DataGridViewColumn CreateFileSizeColumn()
    {
        var column = CreateColumn("大小", 90);
        column.Name = "FileSizeBytes";
        column.ValueType = typeof(long);
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return column;
    }

    internal static DataGridViewColumn CreateRowNumberColumn()
    {
        var column = CreateColumn("行号", 62, minimumWidth: 54);
        column.Name = "RowNumber";
        column.ValueType = typeof(long);
        column.SortMode = DataGridViewColumnSortMode.NotSortable;
        column.Frozen = true;
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return column;
    }

    internal static void UpdateAssetRowNumbers(
        DataGridView grid,
        long firstItemNumber)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (!grid.Columns.Contains("RowNumber"))
        {
            return;
        }

        var firstNumber = Math.Max(1, firstItemNumber);
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Cells["RowNumber"].Value = firstNumber + row.Index;
        }
    }

    internal static DataGridViewColumn CreateAssetIdColumn()
    {
        var column = CreateColumn("资产 ID", 236, minimumWidth: 180);
        column.Name = "AssetId";
        column.ValueType = typeof(string);
        return column;
    }

    internal static DataGridViewColumn CreateObjectStorageStatusColumn()
    {
        var column = CreateColumn("OSS", 82);
        column.Name = "ObjectStorageStatus";
        return column;
    }

    internal static void Grid_CellFormatting(
        object? sender,
        DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.ColumnIndex < 0 ||
            e.ColumnIndex >= grid.Columns.Count)
        {
            return;
        }

        var columnName = grid.Columns[e.ColumnIndex].Name;
        if (string.Equals(
                columnName,
                "FileSizeBytes",
                StringComparison.Ordinal) &&
            e.Value is long bytes)
        {
            e.Value = FormatFileSize(bytes);
            e.FormattingApplied = true;
            return;
        }

        if (string.Equals(
                columnName,
                "ObjectStorageStatus",
                StringComparison.Ordinal) &&
            string.Equals(
                e.Value as string,
                "已备份",
                StringComparison.Ordinal))
        {
            var healthyColor = Color.FromArgb(24, 121, 78);
            e.CellStyle.ForeColor = healthyColor;
            e.CellStyle.SelectionForeColor = healthyColor;
        }
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        SetBusy(true, allowCancel: false);
        try
        {
            await _scanService.InitializeAsync();
            var workspace = await _workspaceService.GetAsync();
            if (workspace is null)
            {
                using var setupForm = new FirstRunSetupForm();
                if (setupForm.ShowDialog(this) != DialogResult.OK)
                {
                    Close();
                    return;
                }

                await _workspaceService.ConfigureAsync(setupForm.SelectedPath);
            }

            var volumeResult = await _volumeReconciliationService.ReconcileAsync();
            EnableLocalVolumeMonitoring();
            await RefreshScanScopeAsync();
            await RefreshAssetsAsync();
            _statusLabel.Text = volumeResult.HasChanges
                ? FormatVolumeReconciliationStatus(volumeResult)
                : "就绪";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "初始化失败";
            ShowError("无法初始化应用", exception);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void SettingsButton_Click(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(
            _workspaceService,
            _scanRootService,
            _storageService,
            _openWebSettingsService);
        settingsForm.ShowDialog(this);
        await _volumeReconciliationService.ReconcileAsync();
        await RefreshScanScopeAsync();
    }

    private async Task RefreshScanScopeAsync()
    {
        var workspaceTask = _workspaceService.GetAsync();
        var rootsTask = _scanRootService.ListAllAsync();
        await Task.WhenAll(workspaceTask, rootsTask);

        var workspace = await workspaceTask;
        var roots = await rootsTask;
        var enabledRoots = roots.Count(root => root.Enabled);
        var externalRoots = roots.Count(root => root.Mode == ScanRootMode.Readonly);
        var offlineRoots = roots.Count(root => root.Status == ScanRootStatus.Offline);
        var offlineText = offlineRoots > 0
            ? $" · 离线 {offlineRoots:N0}"
            : string.Empty;
        _scopeLabel.Text = workspace is null
            ? $"未配置工作目录 · 外部目录 {externalRoots:N0}{offlineText}"
            : $"工作目录: {workspace.Path} · 已启用 {enabledRoots:N0} · 外部目录 {externalRoots:N0}{offlineText}";
    }

    private async void ScanButton_Click(object? sender, EventArgs e)
    {
        var configuredRoots = await _scanService.ListScanRootsAsync();
        if (!configuredRoots.Any(root => root.Enabled))
        {
            MessageBox.Show(
                this,
                "没有已启用的扫描目录。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        var scanProgress = new Progress<ScanProgress>(UpdateScanProgress);
        var fingerprintProgress = new Progress<FingerprintProgress>(UpdateFingerprintProgress);
        var metadataProgress = new Progress<MetadataProgress>(UpdateMetadataProgress);

        SetBusy(true);
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 24;
        _statusLabel.Text = "正在扫描";

        try
        {
            var scanSummary = await Task.Run(
                () => _scanService.ScanConfiguredRootsAsync(
                    scanProgress,
                    _scanCancellation.Token),
                _scanCancellation.Token);

            await RefreshAssetsAsync();
            await RefreshScanScopeAsync();
            if (scanSummary.Cancelled)
            {
                _statusLabel.Text = "扫描已取消";
                return;
            }

            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 1_000;
            _progressBar.Value = 0;
            _statusLabel.Text = "正在提取元数据";

            var metadataSummary = await Task.Run(
                () => _metadataService.ProcessPendingAsync(
                    metadataProgress,
                    _scanCancellation.Token),
                _scanCancellation.Token);

            await RefreshAssetsAsync();
            if (metadataSummary.Cancelled)
            {
                _statusLabel.Text =
                    $"元数据提取已取消，已完成 {metadataSummary.ExtractedFiles:N0} 个文件";
                return;
            }

            var mode = _fullVerificationCheckBox.Checked
                ? FingerprintMode.Complete
                : FingerprintMode.DuplicateCandidates;
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 1_000;
            _progressBar.Value = 0;
            _statusLabel.Text = mode == FingerprintMode.Complete
                ? "正在进行完整校验"
                : "正在检查重复候选";

            var fingerprintSummary = await Task.Run(
                () => _fingerprintService.ProcessPendingAsync(
                    mode,
                    fingerprintProgress,
                    _scanCancellation.Token),
                _scanCancellation.Token);

            await RefreshAssetsAsync();
            _statusLabel.Text = fingerprintSummary.Cancelled
                ? $"哈希已取消，已完成 {fingerprintSummary.FingerprintedFiles:N0} 个文件"
                : $"扫描完成，目录 {scanSummary.RootsScanned:N0}/{scanSummary.RootsConfigured:N0}，已索引 {scanSummary.FilesIndexed:N0} 个文件，元数据 {metadataSummary.ExtractedFiles:N0}，哈希 {fingerprintSummary.FingerprintedFiles:N0}";
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "操作已取消";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _statusLabel.Text = "扫描失败";
            ShowError("扫描未能完成", exception);
        }
        finally
        {
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Blocks;
            SetBusy(false);
        }
    }

    private void UpdateScanProgress(ScanProgress progress)
    {
        _progressLabel.Text =
            $"发现 {progress.FilesDiscovered:N0}  ·  已索引 {progress.FilesIndexed:N0}  ·  错误 {progress.Errors:N0}";
        _currentPathLabel.Text = progress.CurrentPath ?? progress.Message ?? string.Empty;

        if (progress.Stage == ScanStage.Failed)
        {
            _currentPathLabel.Text = progress.Message ?? "扫描失败";
        }
    }

    private void UpdateMetadataProgress(MetadataProgress progress)
    {
        _progressLabel.Text =
            $"元数据 {progress.CompletedFiles:N0}/{progress.TotalFiles:N0}  ·  已提取 {progress.ExtractedFiles:N0}  ·  不支持 {progress.UnsupportedFiles:N0}  ·  错误 {progress.Errors:N0}";
        _currentPathLabel.Text = progress.Message ?? progress.CurrentPath ?? string.Empty;

        _progressBar.Value = progress.TotalFiles == 0
            ? 0
            : (int)Math.Clamp(
                progress.CompletedFiles * 1_000d / progress.TotalFiles,
                0d,
                1_000d);
    }

    private void UpdateFingerprintProgress(FingerprintProgress progress)
    {
        var modeText = progress.Mode == FingerprintMode.Complete
            ? "完整校验"
            : "重复候选";
        _progressLabel.Text =
            $"{modeText} {progress.CompletedFiles:N0}/{progress.TotalFiles:N0}  ·  已哈希 {progress.FingerprintedFiles:N0}  ·  {FormatFileSize(progress.ProcessedBytes)}/{FormatFileSize(progress.TotalBytes)}  ·  {FormatFileSize((long)progress.BytesPerSecond)}/s  ·  错误 {progress.Errors:N0}";
        _currentPathLabel.Text = progress.Message ?? progress.CurrentPath ?? string.Empty;

        _progressBar.Value = progress.TotalBytes == 0
            ? 0
            : (int)Math.Clamp(
                progress.ProcessedBytes * 1_000d / progress.TotalBytes,
                0d,
                1_000d);
    }

    private async Task RefreshAssetsAsync()
    {
        var filter = _assetListFilter;
        var assetCountTask = _scanService.GetAssetListCountAsync(filter);
        var totalAssetCountTask = filter.IsEmpty
            ? assetCountTask
            : _scanService.GetAssetListCountAsync();
        var duplicateGroupsTask = _scanService.ListExactDuplicateGroupsAsync();
        var statisticsTask = _scanService.GetLocalAssetStatisticsAsync();
        var assetDirectoriesTask = _scanService.ListAssetDirectoriesAsync();
        var selectedFileType = _assetFileTypeFilterComboBox.SelectedItem is
            AssetFileTypeFilterChoice selectedType
                ? selectedType.Value
                : filter.FileType;
        var assetExtensionsTask = _scanService.ListAssetExtensionsAsync(
            selectedFileType);
        await Task.WhenAll(
            assetCountTask,
            totalAssetCountTask,
            duplicateGroupsTask,
            statisticsTask,
            assetDirectoriesTask,
            assetExtensionsTask);

        var assetCount = await assetCountTask;
        var totalAssetCount = await totalAssetCountTask;
        var pagination = CalculateAssetPagination(
            assetCount,
            _assetPageSize,
            _assetPageIndex);
        _assetPageIndex = pagination.PageIndex;
        var assets = await _scanService.ListAssetsAsync(
            filter,
            _assetPageSize,
            pagination.Offset);
        var duplicateGroups = await duplicateGroupsTask;
        var statistics = await statisticsTask;
        var assetDirectories = await assetDirectoriesTask;
        var assetExtensions = await assetExtensionsTask;
        _assetGrid.Rows.Clear();
        _duplicateGrid.Rows.Clear();

        foreach (var asset in assets)
        {
            var rowIndex = _assetGrid.Rows.Add(
                pagination.FirstItem + _assetGrid.Rows.Count,
                asset.AssetId.ToString("D"),
                asset.OriginalFilename,
                asset.MimeType ?? "未知",
                asset.Size,
                asset.ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                asset.DiscoveredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                asset.Path,
                FormatMetadata(asset.Metadata),
                asset.HasHealthyObjectStorageBackup ? "已备份" : "未备份",
                FormatStatus(asset));
            _assetGrid.Rows[rowIndex].Tag = asset;
        }

        if (_assetGrid.Rows.Count > 0)
        {
            _assetGrid.CurrentCell = _assetGrid.Rows[0].Cells[0];
            _assetGrid.Rows[0].Selected = true;
            UpdateAssetDetails(_assetGrid.Rows[0].Tag as AssetListItem);
        }
        else
        {
            UpdateAssetDetails(null);
        }

        var groupNumber = 0;
        foreach (var group in duplicateGroups)
        {
            groupNumber++;
            foreach (var asset in group.Assets)
            {
                _duplicateGrid.Rows.Add(
                    groupNumber,
                    group.Sha256[..12],
                    asset.OriginalFilename,
                    group.Size,
                    asset.Path,
                    FormatLocationStatus(asset.LocationStatus));
            }
        }

        _fileCountValueLabel.Text = statistics.FileCount.ToString("N0");
        _totalSizeValueLabel.Text = FormatFileSize(statistics.TotalSizeBytes);
        _videoCountValueLabel.Text = statistics.VideoFileCount.ToString("N0");
        _videoDurationValueLabel.Text =
            FormatTotalDuration(statistics.VideoDurationMilliseconds);

        UpdateAssetPaginationControls(assetCount);
        UpdateAssetFilterResult(assetCount, totalAssetCount);
        if (_assetFileTypeFilterComboBox.SelectedItem is
                AssetFileTypeFilterChoice currentFileType &&
            currentFileType.Value == selectedFileType)
        {
            RefreshAssetExtensionChoices(
                _assetExtensionFilterComboBox,
                assetExtensions,
                includeUnavailableSelection:
                    selectedFileType == filter.FileType &&
                    filter.Extension is not null);
        }
        RefreshAssetDirectories(assetDirectories);
        _assetsTabPage.Text = filter.IsEmpty
            ? $"资产 ({assetCount:N0})"
            : $"资产 ({assetCount:N0}/{totalAssetCount:N0})";
        await RefreshAssetCollectionsAsync();
        _duplicatesTabPage.Text = $"精确重复 ({duplicateGroups.Count:N0})";
        var assetCountStatus = filter.IsEmpty
            ? $"资产位置 {assetCount:N0}"
            : $"筛选资产 {assetCount:N0}/{totalAssetCount:N0}";
        _statusLabel.Text =
            $"{assetCountStatus}  ·  当前页 {assets.Count:N0}  ·  可用文件 {statistics.FileCount:N0}  ·  重复组 {duplicateGroups.Count:N0}";
    }

    private void AssetGrid_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateAssetDetails(_assetGrid.CurrentRow?.Tag as AssetListItem);
    }

    private void UpdateAssetDetails(AssetListItem? asset)
    {
        if (asset is null)
        {
            _assetDetailTitleLabel.Text = "未选择资产";
            _assetDetailSummaryLabel.Text = string.Empty;
            return;
        }

        _assetDetailTitleLabel.Text = asset.OriginalFilename;
        _assetDetailSummaryLabel.Text = string.Join(
            Environment.NewLine,
            $"{asset.MimeType ?? "未知类型"} · {FormatFileSize(asset.Size)} · 修改 {asset.ModifiedAt.ToLocalTime():yyyy-MM-dd HH:mm} · 索引 {asset.DiscoveredAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            asset.Path);
    }
    private void SetBusy(bool busy, bool allowCancel = true)
    {
        _isBusy = busy;
        _scopeLabel.Enabled = !busy;
        _settingsButton.Enabled = !busy;
        _scanButton.Enabled = !busy;
        _cancelButton.Enabled = busy && allowCancel;
        _fullVerificationCheckBox.Enabled = !busy;
        _assetGrid.Enabled = !busy;
        _assetContextMenu.Enabled = !busy;
        UpdateAssetPaginationControls(_assetTotalItems);
        _collectionGrid.Enabled = !busy;
        _collectionMemberGrid.Enabled = !busy;
        _createCollectionButton.Enabled = !busy;
        _assetDirectoryGrid.Enabled = !busy;
        UpdateCollectionActionState();
        UpdateAssetDirectoryActionState();
        UpdateAssetFilterControlState();
        UseWaitCursor = busy && !allowCancel;
    }

    private static string FormatStatus(AssetListItem asset)
    {
        if (asset.LocationStatus == AssetLocationStatus.Offline)
        {
            return "设备离线";
        }

        if (asset.LocationStatus == AssetLocationStatus.Unverified)
        {
            return "位置待确认";
        }

        if (asset.LocationStatus == AssetLocationStatus.Missing)
        {
            return "位置缺失";
        }

        if (asset.LocationOwnership == AssetLocationOwnership.Managed)
        {
            return "工作目录";
        }

        return asset.Status switch
        {
            AssetStatus.Indexed => "已索引",
            AssetStatus.Discovered => "已发现",
            AssetStatus.Error => "错误",
            _ => asset.Status.ToString()
        };
    }

    private static string FormatLocationStatus(AssetLocationStatus status)
    {
        return status switch
        {
            AssetLocationStatus.Available => "可用",
            AssetLocationStatus.Missing => "位置缺失",
            AssetLocationStatus.Offline => "设备离线",
            AssetLocationStatus.Unverified => "位置待确认",
            _ => status.ToString()
        };
    }

    private static string FormatMetadata(AssetMetadata? metadata)
    {
        if (metadata is null)
        {
            return "待提取";
        }

        if (metadata.Status == MetadataExtractionStatus.Unsupported)
        {
            return "无专用元数据";
        }

        if (metadata.Status == MetadataExtractionStatus.Error)
        {
            return "提取失败";
        }

        var content = metadata.Content;
        if (content is null)
        {
            return "已提取";
        }

        var parts = new List<string>();
        if (content.Width is not null && content.Height is not null)
        {
            parts.Add($"{content.Width}×{content.Height}");
        }

        if (content.DurationMilliseconds is not null)
        {
            parts.Add(FormatDuration(content.DurationMilliseconds.Value));
        }

        if (!string.IsNullOrWhiteSpace(content.VideoCodec))
        {
            parts.Add(content.VideoCodec);
        }
        else if (!string.IsNullOrWhiteSpace(content.AudioCodec))
        {
            parts.Add(content.AudioCodec);
        }

        return parts.Count == 0
            ? content.Kind switch
            {
                AssetMediaKind.Image => "图片",
                AssetMediaKind.Audio => "音频",
                AssetMediaKind.Video => "视频",
                _ => "已提取"
            }
            : string.Join(" · ", parts);
    }

    private static string FormatDuration(long milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes}:{duration.Seconds:00}";
    }

    private static string FormatTotalDuration(long milliseconds)
    {
        if (milliseconds <= 0)
        {
            return "0:00";
        }

        var totalSeconds = milliseconds / 1_000;
        var totalHours = totalSeconds / 3_600;
        var minutes = totalSeconds % 3_600 / 60;
        var seconds = totalSeconds % 60;
        return totalHours > 0
            ? $"{totalHours:N0}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    private static string GetApplicationVersion()
    {
        var informationalVersion = typeof(MainForm).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion)
            ? System.Windows.Forms.Application.ProductVersion
            : informationalVersion.Split('+', 2)[0];
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes:N0} {units[unit]}"
            : $"{value:N1} {units[unit]}";
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
