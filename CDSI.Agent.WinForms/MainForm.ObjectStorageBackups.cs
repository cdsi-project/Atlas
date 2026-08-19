using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private readonly TransferSpeedTracker _backupSpeedTracker = new();

    private async Task BackupSelectedAssetsAsync()
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

        IReadOnlyList<ConfiguredObjectStorageProfile> profiles;
        try
        {
            profiles = (await _storageService.ListAsync())
                .Where(profile => profile.HasStoredSecret)
                .ToArray();
        }
        catch (Exception exception)
        {
            ShowError("无法读取 OSS 配置", exception);
            return;
        }

        if (profiles.Count == 0)
        {
            MessageBox.Show(
                this,
                "尚未配置带有效凭据的 OSS。请先在“设置”中添加 OSS 配置。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var uniqueAssets = selected
            .GroupBy(asset => asset.AssetId)
            .Select(group => group.First())
            .ToArray();
        using var confirmation = new OssBackupConfirmationForm(
            profiles,
            uniqueAssets);
        if (confirmation.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _scanCancellation?.Dispose();
        _backupSpeedTracker.Reset();
        _scanCancellation = new CancellationTokenSource();
        var backupProgress =
            new Progress<ObjectStorageBackupProgress>(UpdateBackupProgress);

        SetBusy(true);
        _progressBar.MarqueeAnimationSpeed = 0;
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = 1_000;
        _progressBar.Value = 0;
        _statusLabel.Text = "正在备份到 OSS";

        try
        {
            var requests = uniqueAssets.Select(asset =>
                new ObjectStorageBackupRequest(
                    asset.AssetId,
                    asset.Path,
                    confirmation.SelectedObjectNames[asset.AssetId]))
                .ToArray();
            var result = await _objectStorageBackupService.BackupAsync(
                requests,
                confirmation.SelectedProfileId,
                backupProgress,
                _scanCancellation.Token);
            await RefreshAssetsAsync();
            ShowBackupResult(result);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "OSS 备份已取消";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "OSS 备份失败";
            ShowError("OSS 备份未能完成", exception);
        }
        finally
        {
            _progressBar.Style = ProgressBarStyle.Blocks;
            SetBusy(false);
        }
    }

    private void UpdateBackupProgress(ObjectStorageBackupProgress progress)
    {
        var bytesPerSecond = _backupSpeedTracker.Update(progress.NetworkTransferredBytes);
        var speedText = bytesPerSecond <= 0
            ? "--"
            : $"{FormatFileSize((long)bytesPerSecond)}/s";
        _progressLabel.Text =
            $"文件 {progress.ProcessedItems:N0}/{progress.TotalItems:N0} · {FormatFileSize(progress.UploadedBytes)}/{FormatFileSize(progress.TotalBytes)} · 速度 {speedText}";
        _currentPathLabel.Text = progress.Message is null
            ? progress.CurrentPath ?? string.Empty
            : $"{progress.Message} · {progress.CurrentPath}";
        _progressBar.Value = progress.TotalBytes == 0
            ? 0
            : (int)Math.Clamp(
                progress.UploadedBytes * 1_000d / progress.TotalBytes,
                0d,
                1_000d);
    }

    private void ShowBackupResult(ObjectStorageBackupResult result)
    {
        _statusLabel.Text = result.Status switch
        {
            UploadJobStatus.Completed =>
                $"OSS 备份完成，共 {result.CompletedItems:N0} 个资产",
            UploadJobStatus.Cancelled =>
                $"OSS 备份已取消，已完成 {result.CompletedItems:N0} 个资产",
            _ =>
                $"OSS 备份完成 {result.CompletedItems:N0} 个，失败 {result.FailedItems:N0} 个"
        };

        if (result.Status == UploadJobStatus.Completed)
        {
            MessageBox.Show(
                this,
                $"备份和完整性校验完成，共处理 {result.CompletedItems:N0} 个资产。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var errorLines = result.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ErrorMessage))
            .Take(8)
            .Select(item =>
                $"{item.SourcePath}{Environment.NewLine}{item.ErrorMessage}")
            .ToArray();
        var remaining = result.Items.Count(item =>
            !string.IsNullOrWhiteSpace(item.ErrorMessage)) - errorLines.Length;
        var details = string.Join(
            Environment.NewLine + Environment.NewLine,
            errorLines);
        if (remaining > 0)
        {
            details +=
                $"{Environment.NewLine}{Environment.NewLine}另有 {remaining:N0} 个错误，详情已写入本地上传审计。";
        }

        MessageBox.Show(
            this,
            string.IsNullOrWhiteSpace(details)
                ? _statusLabel.Text
                : $"{_statusLabel.Text}{Environment.NewLine}{Environment.NewLine}{details}",
            "CDSI Atlas",
            MessageBoxButtons.OK,
            result.Status == UploadJobStatus.Cancelled
                ? MessageBoxIcon.Information
                : MessageBoxIcon.Warning);
    }
}
