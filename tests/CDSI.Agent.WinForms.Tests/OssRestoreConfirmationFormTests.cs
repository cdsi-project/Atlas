using CDSI.Agent.Core.Storage;
using CDSI.Agent.WinForms;

namespace CDSI.Agent.WinForms.Tests;

public sealed class OssRestoreConfirmationFormTests
{
    [Fact]
    public void Form_DefaultsToManagedWorkspaceAndReturnsSelectedLocation()
    {
        var now = DateTimeOffset.UtcNow;
        var assetId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var profile = new ObjectStorageProfile(
            Guid.NewGuid(),
            "主 OSS",
            ObjectStorageProvider.AliyunOss,
            "oss-cn-hangzhou.aliyuncs.com",
            "cdsi-assets",
            "cn-hangzhou",
            true,
            "test-access-key-id",
            now,
            now);
        var location = new ObjectStorageLocation(
            locationId,
            assetId,
            profile.Id,
            $"assets/{assetId:N}/article.md",
            StorageVerificationStatus.Healthy,
            42,
            new string('a', 64),
            "etag",
            now,
            now,
            now);
        var source = new ObjectStorageRestoreSource(
            assetId,
            "article.md",
            42,
            now,
            new string('a', 64),
            location);
        var candidate = new ObjectStorageRestoreCandidate(
            assetId,
            "article.md",
            [new ConfiguredObjectStorageRestoreSource(source, profile, true)]);
        using var form = new OssRestoreConfirmationForm(
            [candidate],
            @"D:\cdsi_workspace");
        form.CreateControl();

        var grid = Descendants(form)
            .OfType<DataGridView>()
            .Single(control => control.AccessibleName == "OSS 取回来源列表");

        Assert.Single(grid.Rows.Cast<DataGridViewRow>());
        Assert.True(form.TryCollectSelections(out var requests, out var error));
        Assert.Empty(error);
        Assert.Equal(locationId, Assert.Single(requests).StorageLocationId);
        Assert.Equal(
            ObjectStorageRestoreDestinationKind.ManagedWorkspace,
            form.Destination.Kind);
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
