using CDSI.Agent.Application.Collections;
using CDSI.Agent.Core.Collections;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private readonly AssetCollectionService _assetCollectionService;
    private readonly TabPage _collectionsTabPage = new("项目管理");
    private readonly DataGridView _collectionGrid = new();
    private readonly DataGridView _collectionMemberGrid = new();
    private readonly Button _createCollectionButton = new();
    private readonly Button _syncCollectionButton = new();
    private readonly ToolStripMenuItem _addToCollectionMenuItem = new();
    private readonly ContextMenuStrip _projectContextMenu = new();
    private readonly ToolStripMenuItem _syncProjectContextMenuItem = new();
    private readonly ToolStripMenuItem _deleteProjectContextMenuItem = new();
    private bool _isBusy;
    private bool _refreshingCollections;

    private void ConfigureAssetCollectionTab()
    {
        ConfigureGrid(_collectionGrid);
        _collectionGrid.AccessibleName = "项目列表";
        ConfigureGrid(_collectionMemberGrid);
        EnableAssetMultiSelection(_collectionMemberGrid);
        _collectionMemberGrid.AccessibleName = "项目内资产列表";
        ConfigureProjectManagementGridColumns(
            _collectionGrid,
            _collectionMemberGrid);

        ConfigureCollectionActionButton(
            _createCollectionButton,
            "新建项目",
            Color.FromArgb(24, 121, 78),
            Color.White);
        ConfigureCollectionActionButton(
            _syncCollectionButton,
            "同步到 OSS",
            Color.FromArgb(236, 239, 242),
            Color.FromArgb(31, 37, 43));

        _createCollectionButton.Click += async (_, _) => await CreateCollectionAsync();
        _syncCollectionButton.Click += async (_, _) => await SyncSelectedCollectionAsync();
        _collectionGrid.SelectionChanged += CollectionGrid_SelectionChanged;
        ConfigureProjectContextMenu(
            _projectContextMenu,
            _syncProjectContextMenuItem,
            _deleteProjectContextMenuItem);
        _syncProjectContextMenuItem.Click += async (_, _) =>
            await SyncSelectedCollectionAsync();
        _deleteProjectContextMenuItem.Click += async (_, _) =>
            await DeleteSelectedProjectAsync();
        _deleteProjectContextMenuItem.ShortcutKeyDisplayString = "Delete";
        _projectContextMenu.Opening += (_, args) =>
        {
            var hasProject = GetSelectedCollection() is not null;
            args.Cancel = !hasProject;
            _syncProjectContextMenuItem.Enabled = !_isBusy && hasProject;
            _deleteProjectContextMenuItem.Enabled = !_isBusy && hasProject;
        };
        _collectionGrid.ContextMenuStrip = _projectContextMenu;
        _collectionGrid.MouseDown += (_, args) =>
        {
            if (args.Button != MouseButtons.Right)
            {
                return;
            }

            var hit = _collectionGrid.HitTest(args.X, args.Y);
            if (hit.RowIndex >= 0)
            {
                ApplyAssetGridRightClickSelection(
                    _collectionGrid,
                    hit.RowIndex,
                    hit.ColumnIndex,
                    Keys.None);
            }
            else
            {
                _collectionGrid.ClearSelection();
                _collectionGrid.CurrentCell = null;
            }
        };

        _collectionsTabPage.Padding = Padding.Empty;
        _collectionsTabPage.BackColor = Color.White;
        _collectionsTabPage.Controls.Add(CreateAssetCollectionLayout(
            _collectionGrid,
            _collectionMemberGrid,
            _createCollectionButton,
            _syncCollectionButton));
        UpdateCollectionActionState();
    }

    internal static void ConfigureProjectContextMenu(
        ContextMenuStrip contextMenu,
        ToolStripMenuItem syncItem,
        ToolStripMenuItem deleteItem)
    {
        ArgumentNullException.ThrowIfNull(contextMenu);
        ArgumentNullException.ThrowIfNull(syncItem);
        ArgumentNullException.ThrowIfNull(deleteItem);
        syncItem.Text = "同步到 OSS";
        deleteItem.Text = "删除项目";
        contextMenu.Items.Clear();
        contextMenu.Items.Add(syncItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(deleteItem);
    }

    internal static void ConfigureProjectManagementGridColumns(
        DataGridView projectGrid,
        DataGridView memberGrid)
    {
        ArgumentNullException.ThrowIfNull(projectGrid);
        ArgumentNullException.ThrowIfNull(memberGrid);

        projectGrid.Columns.Add(CreateColumn(
            "名称",
            160,
            DataGridViewAutoSizeColumnMode.Fill,
            45,
            minimumWidth: 120));
        projectGrid.Columns.Add(CreateColumn("类型", 64));
        projectGrid.Columns.Add(CreateColumn("资产", 58));
        projectGrid.Columns.Add(CreateFileSizeColumn());
        projectGrid.Columns.Add(CreateColumn("已备份", 70));

        memberGrid.Columns.Add(CreateColumn(
            "文件",
            220,
            DataGridViewAutoSizeColumnMode.Fill,
            36,
            minimumWidth: 160));
        memberGrid.Columns.Add(CreateColumn("类型", 110));
        memberGrid.Columns.Add(CreateFileSizeColumn());
        memberGrid.Columns.Add(CreateColumn(
            "位置",
            280,
            DataGridViewAutoSizeColumnMode.Fill,
            48,
            minimumWidth: 200));
        memberGrid.Columns.Add(CreateObjectStorageStatusColumn());

        EnableFreeColumnResizing(projectGrid);
        EnableFreeColumnResizing(memberGrid);
    }

    internal static Control CreateAssetCollectionLayout(
        DataGridView collectionGrid,
        DataGridView memberGrid,
        Button createButton,
        Button syncButton)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(8, 10, 8, 8),
            BackColor = Color.White
        };
        toolbar.Controls.Add(createButton);
        toolbar.Controls.Add(syncButton);
        layout.Controls.Add(toolbar, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BorderStyle = BorderStyle.None,
            Size = new Size(900, 400)
        };
        split.SplitterDistance = 430;
        split.Panel1MinSize = 300;
        split.Panel2MinSize = 420;
        split.Panel1.Padding = new Padding(0, 0, 6, 0);
        split.Panel2.Padding = new Padding(6, 0, 0, 0);
        split.Panel1.Controls.Add(CreateCollectionPane("项目列表", collectionGrid));
        split.Panel2.Controls.Add(CreateCollectionPane("项目内资产", memberGrid));
        layout.Controls.Add(split, 0, 1);
        return layout;
    }

    private static Control CreateCollectionPane(string title, DataGridView grid)
    {
        var pane = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        pane.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(52, 61, 69),
            BackColor = Color.FromArgb(247, 248, 250)
        }, 0, 0);
        pane.Controls.Add(grid, 0, 1);
        return pane;
    }

    private static void ConfigureCollectionActionButton(
        Button button,
        string text,
        Color background,
        Color foreground)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(128, 32);
        button.Margin = new Padding(0, 0, 8, 0);
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
    }

    private async Task CreateCollectionAsync()
    {
        var collectionId = await CreateCollectionWithDialogAsync();
        if (collectionId is not null)
        {
            await RefreshAssetCollectionsAsync(collectionId);
        }
    }

    private async Task<Guid?> CreateCollectionWithDialogAsync()
    {
        using var dialog = new AssetCollectionDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return null;
        }

        try
        {
            var collection = await _assetCollectionService.CreateAsync(
                dialog.CollectionName,
                dialog.CollectionType);
            _statusLabel.Text = $"已创建资产清单：{collection.Name}";
            return collection.Id;
        }
        catch (Exception exception)
        {
            ShowError("无法创建资产清单", exception);
            return null;
        }
    }

    private async Task AddSelectedAssetsToCollectionAsync()
    {
        var selectedAssets = GetSelectedAssets();
        if (selectedAssets.Count == 0)
        {
            return;
        }

        try
        {
            var collections = await _assetCollectionService.ListAsync();
            Guid? collectionId;
            if (collections.Count == 0)
            {
                collectionId = await CreateCollectionWithDialogAsync();
            }
            else
            {
                using var selection = new AssetCollectionSelectionForm(
                    collections,
                    selectedAssets.Count);
                collectionId = selection.ShowDialog(this) == DialogResult.OK
                    ? selection.SelectedCollectionId
                    : null;
            }

            if (collectionId is null)
            {
                return;
            }

            var added = await _assetCollectionService.AddAssetsAsync(
                collectionId.Value,
                selectedAssets.Select(asset => asset.AssetId).ToArray());
            await RefreshAssetCollectionsAsync(collectionId);
            await RefreshAssetPageAsync();
            _statusLabel.Text = added == 0
                ? "所选资产已在该清单中"
                : $"已将 {added:N0} 个资产加入清单";
        }
        catch (Exception exception)
        {
            ShowError("无法将资产加入清单", exception);
        }
    }

    private async Task SyncSelectedCollectionAsync()
    {
        var selected = GetSelectedCollection();
        if (selected is null)
        {
            return;
        }

        try
        {
            var plan = await _assetCollectionService.PrepareSyncAsync(selected.Id);
            if (plan.Members.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "该资产清单还没有资产。",
                    "CDSI Atlas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (plan.UnavailableAssetCount > 0)
            {
                MessageBox.Show(
                    this,
                    $"清单中有 {plan.UnavailableAssetCount:N0} 个本地位置缺失的资产。请恢复这些文件后再同步整个清单。",
                    "CDSI Atlas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            await BackupAssetsAsync(
                plan.Assets,
                $"正在同步清单：{plan.Collection.Name}",
                objectDirectory: plan.Collection.Name);
        }
        catch (Exception exception)
        {
            ShowError("无法同步资产清单", exception);
        }
    }

    private async Task DeleteSelectedProjectAsync()
    {
        var selected = GetSelectedCollection();
        if (selected is null)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            CreateProjectDeletionConfirmation(selected),
            "删除项目",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.OK)
        {
            return;
        }

        try
        {
            var deleted = await _assetCollectionService.DeleteAsync(selected.Id);
            await RefreshAssetCollectionsAsync();
            await RefreshAssetPageAsync();
            _statusLabel.Text = $"已删除项目：{deleted.Name}；资产文件和 OSS 备份未更改";
        }
        catch (Exception exception)
        {
            ShowError("无法删除项目", exception);
        }
    }

    internal static string CreateProjectDeletionConfirmation(
        AssetCollectionSummary project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return
            $"确定删除项目“{project.Name}”吗？{Environment.NewLine}{Environment.NewLine}" +
            $"将移除该项目以及它与 {project.AssetCount:N0} 个资产的本地项目关系。" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "不会删除、移动或修改资产文件，也不会删除已有 OSS 备份。" +
            $"{Environment.NewLine}此操作无法撤销。";
    }

    private async void CollectionGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_refreshingCollections)
        {
            return;
        }

        try
        {
            await RefreshSelectedCollectionMembersAsync();
        }
        catch (Exception exception)
        {
            ShowError("无法读取资产清单", exception);
        }
    }

    private async Task RefreshAssetCollectionsAsync(Guid? selectedCollectionId = null)
    {
        var currentId = selectedCollectionId ?? GetSelectedCollection()?.Id;
        var collections = await _assetCollectionService.ListAsync();
        _refreshingCollections = true;
        try
        {
            _collectionGrid.Rows.Clear();
            foreach (var collection in collections)
            {
                var rowIndex = _collectionGrid.Rows.Add(
                    collection.Name,
                    FormatCollectionType(collection.Type),
                    collection.AssetCount,
                    collection.TotalSizeBytes,
                    $"{collection.BackedUpAssetCount:N0}/{collection.AssetCount:N0}");
                _collectionGrid.Rows[rowIndex].Tag = collection;
            }

            var rowToSelect = _collectionGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(row =>
                    (row.Tag as AssetCollectionSummary)?.Id == currentId)
                ?? _collectionGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault();
            if (rowToSelect is not null)
            {
                _collectionGrid.CurrentCell = rowToSelect.Cells[0];
                rowToSelect.Selected = true;
            }
        }
        finally
        {
            _refreshingCollections = false;
        }

        _collectionsTabPage.Text = $"项目管理 ({collections.Count:N0})";
        await RefreshSelectedCollectionMembersAsync();
    }

    private async Task RefreshSelectedCollectionMembersAsync()
    {
        var collection = GetSelectedCollection();
        if (collection is null)
        {
            _collectionMemberGrid.Rows.Clear();
            UpdateCollectionActionState();
            return;
        }

        var members = await _assetCollectionService.GetMembersAsync(collection.Id);
        if (GetSelectedCollection()?.Id != collection.Id)
        {
            return;
        }

        _collectionMemberGrid.Rows.Clear();
        foreach (var member in members)
        {
            var asset = member.Asset;
            var rowIndex = _collectionMemberGrid.Rows.Add(
                asset.OriginalFilename,
                asset.MimeType ?? "未知",
                asset.Size,
                asset.Path,
                asset.HasHealthyObjectStorageBackup ? "已备份" : "未备份");
            _collectionMemberGrid.Rows[rowIndex].Tag = member;
        }

        UpdateCollectionActionState();
    }

    private AssetCollectionSummary? GetSelectedCollection()
    {
        return _collectionGrid.CurrentRow?.Tag as AssetCollectionSummary;
    }

    private void UpdateCollectionActionState()
    {
        var hasCollection = GetSelectedCollection() is not null;
        _syncCollectionButton.Enabled = !_isBusy && hasCollection;
        _syncProjectContextMenuItem.Enabled = !_isBusy && hasCollection;
        _deleteProjectContextMenuItem.Enabled = !_isBusy && hasCollection;
    }

    internal static string FormatCollectionType(AssetCollectionType type)
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
