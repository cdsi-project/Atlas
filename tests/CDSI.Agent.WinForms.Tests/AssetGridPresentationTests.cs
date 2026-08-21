using CDSI.Agent.Core.Assets;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class AssetGridPresentationTests
{
    [Fact]
    public void GridCellFormatting_UsesGreenForHealthyBackups()
    {
        using var grid = new DataGridView();
        grid.Columns.Add(MainForm.CreateBackupStatusColumn());
        var style = new DataGridViewCellStyle();
        var args = new DataGridViewCellFormattingEventArgs(
            0,
            0,
            "OSS、S3",
            typeof(string),
            style);

        MainForm.Grid_CellFormatting(grid, args);

        var expected = Color.FromArgb(24, 121, 78);
        Assert.Equal(expected, style.ForeColor);
        Assert.Equal(expected, style.SelectionForeColor);
    }

    [Fact]
    public void BackupStatus_UsesProviderLabels()
    {
        Assert.Equal(
            "未备份",
            MainForm.FormatBackupStatus(false, []));
        Assert.Equal(
            "已备份",
            MainForm.FormatBackupStatus(true, []));
        Assert.Equal(
            "OSS",
            MainForm.FormatBackupStatus(true, ["AliyunOss"]));
        Assert.Equal(
            "OSS、七牛、COS、S3",
            MainForm.FormatBackupStatus(
                true,
                ["AliyunOss", "Qiniu", "TencentCos", "S3", "AliyunOss"]));
    }

    [Fact]
    public void BackupTime_UsesDashOrLocalTimestamp()
    {
        var value = DateTimeOffset.Parse("2026-08-21T08:30:00+08:00");

        Assert.Equal("-", MainForm.FormatBackupTime(null));
        Assert.Equal(
            value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            MainForm.FormatBackupTime(value));
    }

    [Fact]
    public void AssetDetails_ShowHealthyBackupProviderAndTime()
    {
        var backupTime = DateTimeOffset.Parse("2026-08-21T08:30:00+08:00");
        var asset = new AssetListItem(
            Guid.NewGuid(),
            "video.mp4",
            ".mp4",
            "video/mp4",
            1_024,
            null,
            backupTime,
            backupTime,
            @"D:\Creator\video.mp4",
            AssetLocationOwnership.External,
            AssetLocationStatus.Available,
            AssetStatus.Indexed,
            HasHealthyObjectStorageBackup: true)
        {
            HealthyBackupProviders = ["AliyunOss"],
            LatestHealthyBackupAt = backupTime
        };

        var summary = MainForm.FormatAssetDetailSummary(asset);

        Assert.Contains("备份：OSS", summary);
        Assert.Contains(
            $"时间 {backupTime.ToLocalTime():yyyy-MM-dd HH:mm}",
            summary);
    }

    [Fact]
    public void TransferSpeedTracker_UsesTransferredByteDeltas()
    {
        var tracker = new TransferSpeedTracker(timestampFrequency: 1_000);

        Assert.Equal(0, tracker.Update(0, timestamp: 0));
        Assert.Equal(
            4d * 1024 * 1024,
            tracker.Update(1L * 1024 * 1024, timestamp: 250));
        Assert.Equal(
            2d * 1024 * 1024,
            tracker.Update(3L * 1024 * 1024, timestamp: 1_250));
        Assert.Equal(
            0,
            tracker.Update(3L * 1024 * 1024, timestamp: 2_250));

        tracker.Reset();
        Assert.Equal(0, tracker.BytesPerSecond);
    }
}
