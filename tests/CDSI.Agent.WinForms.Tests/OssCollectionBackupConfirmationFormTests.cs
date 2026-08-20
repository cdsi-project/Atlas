using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class OssCollectionBackupConfirmationFormTests
{
    [Fact]
    public void Form_CollectionModeKeepsOriginalFilenameAndShowsTargetDirectory()
    {
        var now = DateTimeOffset.UtcNow;
        var asset = CreateAsset(
            Guid.NewGuid(),
            @"D:\素材\原始视频.mp4",
            now);
        using var form = new OssBackupConfirmationForm(
            [CreateProfile(now)],
            [asset],
            "第一期视频");
        form.CreateControl();

        var grid = Descendants(form)
            .OfType<DataGridView>()
            .Single(control => control.AccessibleName == "OSS 备份文件名列表");
        var row = Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        var objectNameColumn = Assert.IsType<DataGridViewTextBoxColumn>(
            grid.Columns["ObjectName"]);

        Assert.True(objectNameColumn.ReadOnly);
        Assert.Contains("保持原名", objectNameColumn.HeaderText);
        Assert.Equal("原始视频.mp4", row.Cells["ObjectName"].Value);
        Assert.Contains(
            Descendants(form).OfType<Label>(),
            label => label.Text.Contains("第一期视频/", StringComparison.Ordinal));
        Assert.True(form.TryCollectObjectNames(
            out var objectNames,
            out var errorMessage));
        Assert.Empty(errorMessage);
        Assert.Equal("原始视频.mp4", objectNames[asset.AssetId]);
    }

    [Fact]
    public void Form_CollectionModeRejectsDuplicateOriginalFilenames()
    {
        var now = DateTimeOffset.UtcNow;
        var firstAsset = CreateAsset(
            Guid.NewGuid(),
            @"D:\素材A\clip.mp4",
            now);
        var secondAsset = CreateAsset(
            Guid.NewGuid(),
            @"D:\素材B\clip.mp4",
            now);
        using var form = new OssBackupConfirmationForm(
            [CreateProfile(now)],
            [firstAsset, secondAsset],
            "第一期视频");
        form.CreateControl();

        Assert.False(form.TryCollectObjectNames(
            out _,
            out var errorMessage));
        Assert.Contains("同名文件", errorMessage);
    }

    private static ConfiguredObjectStorageProfile CreateProfile(
        DateTimeOffset now)
    {
        return new ConfiguredObjectStorageProfile(
            new ObjectStorageProfile(
                Guid.NewGuid(),
                "主 OSS",
                ObjectStorageProvider.AliyunOss,
                "oss-cn-hangzhou.aliyuncs.com",
                "cdsi-assets",
                "cn-hangzhou",
                true,
                "test-access-key-id",
                now,
                now),
            HasStoredSecret: true);
    }

    private static AssetListItem CreateAsset(
        Guid assetId,
        string path,
        DateTimeOffset now)
    {
        return new AssetListItem(
            assetId,
            Path.GetFileName(path),
            Path.GetExtension(path),
            "video/mp4",
            42,
            now,
            now,
            path,
            AssetLocationOwnership.External,
            AssetLocationStatus.Available,
            AssetStatus.Indexed,
            HasHealthyObjectStorageBackup: false);
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
