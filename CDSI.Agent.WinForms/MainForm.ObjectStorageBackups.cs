using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.WinForms;

public sealed partial class MainForm
{
    private readonly TransferSpeedTracker _backupSpeedTracker = new();

    private async Task SyncSelectedAssetsToProjectAsync()
    {
        var selected = GetSelectedAssets();
        if (selected.Count == 0)
        {
            return;
        }

        var commonProjects = FindCommonProjects(_availableCollections, selected);
        if (commonProjects.Count == 0)
        {
            await AddSelectedAssetsToProjectAndSyncAsync();
            return;
        }

        Guid? projectId;
        if (commonProjects.Count == 1)
        {
            projectId = commonProjects[0].Id;
        }
        else
        {
            using var selection = new AssetCollectionSelectionForm(
                commonProjects,
                selected.Count,
                AssetCollectionSelectionPurpose.Sync);
            projectId = selection.ShowDialog(this) == DialogResult.OK
                ? selection.SelectedCollectionId
                : null;
        }

        if (projectId is not null)
        {
            await SyncSelectedAssetsToProjectAsync(projectId.Value);
        }
    }

    private async Task SyncSelectedAssetsToProjectAsync(Guid projectId)
    {
        var selected = GetSelectedAssets();
        if (selected.Count == 0)
        {
            return;
        }

        await SyncAssetsToProjectAsync(
            projectId,
            selected.Select(asset => asset.AssetId).ToArray());
    }

    private async Task AddSelectedAssetsToProjectAndSyncAsync()
    {
        var selected = GetSelectedAssets();
        if (selected.Count == 0)
        {
            return;
        }

        try
        {
            var projects = await _assetCollectionService.ListAsync();
            Guid? projectId;
            if (projects.Count == 0)
            {
                projectId = await CreateCollectionWithDialogAsync();
            }
            else
            {
                using var selection = new AssetCollectionSelectionForm(
                    projects,
                    selected.Count,
                    AssetCollectionSelectionPurpose.AddAndSync);
                if (selection.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                projectId = selection.CreateNewProject
                    ? await CreateCollectionWithDialogAsync()
                    : selection.SelectedCollectionId;
            }

            if (projectId is null)
            {
                return;
            }

            var assetIds = selected
                .Select(asset => asset.AssetId)
                .Distinct()
                .ToArray();
            var added = await _assetCollectionService.AddAssetsAsync(
                projectId.Value,
                assetIds);
            await RefreshAssetCollectionsAsync(projectId);
            await RefreshAssetPageAsync();
            _statusLabel.Text = added == 0
                ? "所选资产已在目标项目中，准备同步"
                : $"已将 {added:N0} 个资产加入项目，准备同步";
            await SyncAssetsToProjectAsync(projectId.Value, assetIds);
        }
        catch (Exception exception)
        {
            ShowError("无法加入项目并同步到 OSS", exception);
        }
    }

    private async Task SyncAssetsToProjectAsync(
        Guid projectId,
        IReadOnlyCollection<Guid> assetIds)
    {
        try
        {
            var plan = await _assetCollectionService.PrepareSelectedSyncAsync(
                projectId,
                assetIds);
            await BackupProjectAssetsAsync(
                plan.Assets,
                $"正在同步项目：{plan.Collection.Name}",
                plan.Collection.Name);
        }
        catch (Exception exception)
        {
            ShowError("无法同步项目资产到 OSS", exception);
        }
    }

    private async Task BackupProjectAssetsAsync(
        IReadOnlyCollection<AssetListItem> assets,
        string progressStatus,
        string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        if (assets.Any(asset =>
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

        IReadOnlyList<ConfiguredObjectStorageProfile> profiles;
        try
        {
            profiles = (await _storageService.ListAsync())
                .Where(profile => profile.HasStoredSecret)
                .ToArray();
        }
        catch (Exception exception)
        {
            ShowError("无法读取备份配置", exception);
            return;
        }

        if (profiles.Count == 0)
        {
            MessageBox.Show(
                this,
                "尚未配置带有效凭据的备份存储。请先在“设置”的“备份配置”中添加配置。",
                "CDSI Atlas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var uniqueAssets = assets
            .GroupBy(asset => asset.AssetId)
            .Select(group => group.First())
            .ToArray();
        using var confirmation = new OssBackupConfirmationForm(
            profiles,
            uniqueAssets,
            projectDirectory);
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
        _statusLabel.Text = progressStatus;

        try
        {
            var requests = uniqueAssets.Select(asset =>
                new ObjectStorageBackupRequest(
                    asset.AssetId,
                    asset.Path,
                    confirmation.SelectedObjectNames[asset.AssetId],
                    ObjectDirectory: projectDirectory))
                .ToArray();
            var result = await _objectStorageBackupService.BackupAsync(
                requests,
                confirmation.SelectedProfileId,
                backupProgress,
                _scanCancellation.Token);
            await RefreshAssetPageAsync();
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
