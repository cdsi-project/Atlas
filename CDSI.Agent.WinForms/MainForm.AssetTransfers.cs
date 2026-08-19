using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Transfers;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private void ConfigureAssetContextMenu()
    {
        _copyToWorkspaceMenuItem.Text = "复制到 CDSI 工作目录";
        _moveToWorkspaceMenuItem.Text = "移动到 CDSI 工作目录";
        _copyToWorkspaceMenuItem.Click += async (_, _) =>
            await TransferSelectedAssetsAsync(ManagedAssetTransferAction.Copy);
        _moveToWorkspaceMenuItem.Click += async (_, _) =>
            await TransferSelectedAssetsAsync(ManagedAssetTransferAction.Move);

        _assetContextMenu.Items.AddRange(
            [_copyToWorkspaceMenuItem, _moveToWorkspaceMenuItem]);
        _assetContextMenu.Opening += (_, args) =>
        {
            var selected = GetSelectedAssets();
            var canOperate = selected.Count > 0 &&
                selected.All(asset =>
                    asset.LocationStatus == AssetLocationStatus.Available);
            args.Cancel = selected.Count == 0;
            _copyToWorkspaceMenuItem.Enabled = canOperate;
            _moveToWorkspaceMenuItem.Enabled = canOperate;
            _copyToWorkspaceMenuItem.Text =
                $"复制到 CDSI 工作目录 ({selected.Count:N0})";
            _moveToWorkspaceMenuItem.Text =
                $"移动到 CDSI 工作目录 ({selected.Count:N0})";
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

        var row = _assetGrid.Rows[e.RowIndex];
        if (!row.Selected)
        {
            _assetGrid.ClearSelection();
            row.Selected = true;
        }

        var columnIndex = e.ColumnIndex >= 0 ? e.ColumnIndex : 0;
        _assetGrid.CurrentCell = row.Cells[columnIndex];
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
                "选择中包含位置缺失的文件，请重新选择可用位置。",
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
