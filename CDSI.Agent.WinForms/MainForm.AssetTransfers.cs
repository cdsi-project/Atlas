using System.Diagnostics;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Duplicates;
using CDSI.Agent.Core.Transfers;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private readonly ToolStripMenuItem _openFileLocationMenuItem = new();
    private readonly ContextMenuStrip _duplicateContextMenu = new();
    private readonly ToolStripMenuItem _openDuplicateFileLocationMenuItem = new();

    private void ConfigureAssetContextMenu()
    {
        _openFileLocationMenuItem.Text = "打开文件位置";
        _addToCollectionMenuItem.Text = "加入资产清单";
        _copyToWorkspaceMenuItem.Text = "复制到 CDSI 工作目录";
        _moveToWorkspaceMenuItem.Text = "移动到 CDSI 工作目录";
        _backupToOssMenuItem.Text = "备份到 OSS";
        _publishToOpenWebMenuItem.Text = "发布到 OpenWeb";
        _openFileLocationMenuItem.Click += (_, _) => OpenCurrentAssetFileLocation();
        _copyToWorkspaceMenuItem.Click += async (_, _) =>
            await TransferSelectedAssetsAsync(ManagedAssetTransferAction.Copy);
        _moveToWorkspaceMenuItem.Click += async (_, _) =>
            await TransferSelectedAssetsAsync(ManagedAssetTransferAction.Move);
        _addToCollectionMenuItem.Click += async (_, _) =>
            await AddSelectedAssetsToCollectionAsync();
        _backupToOssMenuItem.Click += async (_, _) =>
            await BackupSelectedAssetsAsync();
        _publishToOpenWebMenuItem.Click += async (_, _) =>
            await PublishSelectedArticleAsync();

        _assetContextMenu.Items.AddRange(
            [
                _openFileLocationMenuItem,
                new ToolStripSeparator(),
                _addToCollectionMenuItem,
                _publishToOpenWebMenuItem,
                new ToolStripSeparator(),
                _copyToWorkspaceMenuItem,
                _moveToWorkspaceMenuItem,
                new ToolStripSeparator(),
                _backupToOssMenuItem
            ]);
        _assetContextMenu.Opening += (_, args) =>
        {
            var selected = GetSelectedAssets();
            var canOperate = selected.Count > 0 &&
                selected.All(asset =>
                    asset.LocationStatus == AssetLocationStatus.Available);
            args.Cancel = selected.Count == 0;
            _openFileLocationMenuItem.Enabled = _assetGrid.CurrentRow?.Tag is AssetListItem;
            _addToCollectionMenuItem.Enabled = selected.Count > 0;
            _copyToWorkspaceMenuItem.Enabled = canOperate;
            _moveToWorkspaceMenuItem.Enabled = canOperate;
            _backupToOssMenuItem.Enabled = canOperate;
            _publishToOpenWebMenuItem.Enabled =
                selected.Count == 1 &&
                canOperate &&
                _openWebPublishingService.Supports(selected[0].Path);
            _addToCollectionMenuItem.Text =
                $"加入资产清单 ({selected.Count:N0})";
            _copyToWorkspaceMenuItem.Text =
                $"复制到 CDSI 工作目录 ({selected.Count:N0})";
            _moveToWorkspaceMenuItem.Text =
                $"移动到 CDSI 工作目录 ({selected.Count:N0})";
            _backupToOssMenuItem.Text =
                $"备份到 OSS ({selected.Count:N0})";
        };
        _assetGrid.ContextMenuStrip = _assetContextMenu;
        _assetGrid.CellMouseDown += AssetGrid_CellMouseDown;
    }

    private void AssetGrid_CellMouseDown(
        object? sender,
        DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0)
        {
            return;
        }

        ApplyAssetGridRightClickSelection(
            _assetGrid,
            e.RowIndex,
            e.ColumnIndex,
            ModifierKeys);
    }

    private void ConfigureDuplicateContextMenu()
    {
        _openDuplicateFileLocationMenuItem.Text = "打开文件位置";
        _openDuplicateFileLocationMenuItem.Click += (_, _) =>
            OpenCurrentDuplicateFileLocation();
        _duplicateContextMenu.Items.Add(_openDuplicateFileLocationMenuItem);
        _duplicateContextMenu.Opening += (_, args) =>
            args.Cancel = GetDuplicateFilePath(_duplicateGrid.CurrentRow) is null;
        _duplicateGrid.ContextMenuStrip = _duplicateContextMenu;
        _duplicateGrid.CellMouseDown += (_, args) =>
        {
            if (args.Button == MouseButtons.Right && args.RowIndex >= 0)
            {
                ApplyAssetGridRightClickSelection(
                    _duplicateGrid,
                    args.RowIndex,
                    args.ColumnIndex,
                    Keys.None);
            }
        };
        _duplicateGrid.CellDoubleClick += (_, args) =>
        {
            if (args.RowIndex >= 0)
            {
                OpenCurrentDuplicateFileLocation();
            }
        };
    }

    internal static void ApplyAssetGridRightClickSelection(
        DataGridView grid,
        int rowIndex,
        int columnIndex,
        Keys modifiers)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count || grid.Columns.Count == 0)
        {
            return;
        }

        var targetRow = grid.Rows[rowIndex];
        var targetColumnIndex = columnIndex >= 0 && columnIndex < grid.Columns.Count
            ? columnIndex
            : 0;
        var anchorRowIndex = grid.CurrentCell?.RowIndex ?? rowIndex;
        var useShift = grid.MultiSelect && (modifiers & Keys.Shift) == Keys.Shift;
        var useControl = grid.MultiSelect && (modifiers & Keys.Control) == Keys.Control;

        if (useShift)
        {
            grid.ClearSelection();
            grid.CurrentCell = targetRow.Cells[targetColumnIndex];
            var firstRowIndex = Math.Min(anchorRowIndex, rowIndex);
            var lastRowIndex = Math.Max(anchorRowIndex, rowIndex);
            for (var index = firstRowIndex; index <= lastRowIndex; index++)
            {
                grid.Rows[index].Selected = true;
            }

            return;
        }

        if (useControl)
        {
            var selectedRowIndexes = grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.Index)
                .ToArray();
            grid.CurrentCell = targetRow.Cells[targetColumnIndex];
            foreach (var selectedRowIndex in selectedRowIndexes)
            {
                grid.Rows[selectedRowIndex].Selected = true;
            }

            targetRow.Selected = true;
            return;
        }

        if (!targetRow.Selected)
        {
            grid.ClearSelection();
            grid.CurrentCell = targetRow.Cells[targetColumnIndex];
            targetRow.Selected = true;
            return;
        }

        var preservedRowIndexes = grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(row => row.Index)
            .ToArray();
        grid.CurrentCell = targetRow.Cells[targetColumnIndex];
        foreach (var selectedRowIndex in preservedRowIndexes)
        {
            grid.Rows[selectedRowIndex].Selected = true;
        }
    }

    private void OpenCurrentAssetFileLocation()
    {
        if (_assetGrid.CurrentRow?.Tag is not AssetListItem asset)
        {
            return;
        }

        OpenFileLocation(asset.Path);
    }

    private void OpenCurrentDuplicateFileLocation()
    {
        var path = GetDuplicateFilePath(_duplicateGrid.CurrentRow);
        if (path is not null)
        {
            OpenFileLocation(path);
        }
    }

    internal static string? GetDuplicateFilePath(DataGridViewRow? row)
    {
        return (row?.Tag as DuplicateAssetItem)?.Path;
    }

    private void OpenFileLocation(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(
                this,
                $"文件当前位置不存在：{Environment.NewLine}{path}",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var process = Process.Start(CreateOpenFileLocationStartInfo(path));
        }
        catch (Exception exception)
        {
            ShowError("无法打开文件位置", exception);
        }
    }

    internal static ProcessStartInfo CreateOpenFileLocationStartInfo(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("/select,");
        startInfo.ArgumentList.Add(Path.GetFullPath(filePath));
        return startInfo;
    }

    private IReadOnlyList<AssetListItem> GetSelectedAssets()
    {
        return _assetGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.Tag as AssetListItem)
            .Where(asset => asset is not null)
            .Cast<AssetListItem>()
            .ToArray();
    }

    private async Task TransferSelectedAssetsAsync(
        ManagedAssetTransferAction action)
    {
        var selected = GetSelectedAssets();
        if (selected.Count == 0)
        {
            return;
        }

        if (selected.Any(asset =>
                asset.LocationStatus != AssetLocationStatus.Available))
        {
            MessageBox.Show(
                this,
                "选择中包含不可用的本地位置，请重新选择可用文件。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var confirmation = new AssetTransferConfirmationForm(action, selected);
        if (confirmation.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        var transferProgress =
            new Progress<ManagedAssetTransferProgress>(UpdateTransferProgress);
        var actionText = action == ManagedAssetTransferAction.Move
            ? "移动"
            : "复制";

        SetBusy(true);
        _progressBar.MarqueeAnimationSpeed = 0;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 1_000;
        _progressBar.Value = 0;
        _statusLabel.Text = $"正在{actionText}到 CDSI 工作目录";

        try
        {
            var requests = selected.Select(asset =>
                new ManagedAssetTransferRequest(
                    asset.AssetId,
                    asset.Path)).ToArray();
            var result = await _transferService.TransferAsync(
                requests,
                action,
                transferProgress,
                _scanCancellation.Token);
            await RefreshAssetsAsync();
            ShowTransferResult(result);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = $"{actionText}已取消";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"{actionText}失败";
            ShowError($"{actionText}未能完成", exception);
        }
        finally
        {
            _progressBar.Style = ProgressBarStyle.Blocks;
            SetBusy(false);
        }
    }

    private void UpdateTransferProgress(ManagedAssetTransferProgress progress)
    {
        _progressLabel.Text =
            $"文件 {progress.ProcessedItems:N0}/{progress.TotalItems:N0}  ·  {FormatFileSize(progress.ProcessedBytes)}/{FormatFileSize(progress.TotalBytes)}";
        _currentPathLabel.Text =
            progress.Message is null
                ? progress.CurrentPath ?? string.Empty
                : $"{progress.Message} · {progress.CurrentPath}";
        _progressBar.Value = progress.TotalBytes == 0
            ? 0
            : (int)Math.Clamp(
                progress.ProcessedBytes * 1_000d / progress.TotalBytes,
                0d,
                1_000d);
    }

    private void ShowTransferResult(ManagedAssetTransferResult result)
    {
        var actionText = result.Action == ManagedAssetTransferAction.Move
            ? "移动"
            : "复制";
        _statusLabel.Text = result.Status switch
        {
            FileOperationStatus.Completed =>
                $"{actionText}完成，共 {result.CompletedItems:N0} 个文件",
            FileOperationStatus.Cancelled =>
                $"{actionText}已取消，已完成 {result.CompletedItems:N0} 个文件",
            _ =>
                $"{actionText}完成 {result.CompletedItems:N0} 个，失败 {result.FailedItems:N0} 个"
        };

        if (result.Status == FileOperationStatus.Completed)
        {
            MessageBox.Show(
                this,
                $"{actionText}完成，共处理 {result.CompletedItems:N0} 个文件。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var errorLines = result.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ErrorMessage))
            .Take(8)
            .Select(item => $"{item.SourcePath}{Environment.NewLine}{item.ErrorMessage}")
            .ToArray();
        var remaining = result.Items.Count(item =>
            !string.IsNullOrWhiteSpace(item.ErrorMessage)) - errorLines.Length;
        var details = string.Join(
            Environment.NewLine + Environment.NewLine,
            errorLines);
        if (remaining > 0)
        {
            details +=
                $"{Environment.NewLine}{Environment.NewLine}另有 {remaining:N0} 个错误，详情已写入本地操作审计。";
        }

        MessageBox.Show(
            this,
            string.IsNullOrWhiteSpace(details)
                ? _statusLabel.Text
                : $"{_statusLabel.Text}{Environment.NewLine}{Environment.NewLine}{details}",
            "CDSI Atlas",
            MessageBoxButtons.OK,
            result.Status == FileOperationStatus.Cancelled
                ? MessageBoxIcon.Information
                : MessageBoxIcon.Warning);
    }
}
