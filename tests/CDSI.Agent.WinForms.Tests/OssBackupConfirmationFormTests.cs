using CDSI.Agent.Application.Storage;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Storage;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class OssBackupConfirmationFormTests
{
    [Fact]
    public void Form_PrefillsAndReturnsAnEditableObjectNameForEachAsset()
    {
        var now = DateTimeOffset.UtcNow;
        var assetId = Guid.NewGuid();
        var profile = new ConfiguredObjectStorageProfile(
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
        var asset = new AssetListItem(
            assetId,
            "原始视频.mp4",
            ".mp4",
            "video/mp4",
            42,
            now,
            @"D:\素材\原始视频.mp4",
            AssetLocationOwnership.External,
            AssetLocationStatus.Available,
            AssetStatus.Indexed,
            HasHealthyObjectStorageBackup: false);
        using var form = new OssBackupConfirmationForm([profile], [asset]);
        form.CreateControl();

        var grid = Descendants(form)
            .OfType<DataGridView>()
            .Single(control => control.AccessibleName == "OSS 备份文件名列表");
        var row = Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        var localPathColumn = Assert.IsType<DataGridViewTextBoxColumn>(
            grid.Columns["LocalPath"]);
        var objectNameColumn = Assert.IsType<DataGridViewTextBoxColumn>(
            grid.Columns["ObjectName"]);

        Assert.Equal(2, grid.Columns.Count);
        Assert.True(localPathColumn.ReadOnly);
        Assert.False(objectNameColumn.ReadOnly);
        Assert.Equal("原始视频.mp4", row.Cells["ObjectName"].Value);

        row.Cells["ObjectName"].Value = "发布版.mp4";
        Assert.True(form.TryCollectObjectNames(
            out var objectNames,
            out var errorMessage));
        Assert.Empty(errorMessage);
        Assert.Equal("发布版.mp4", objectNames[assetId]);
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
