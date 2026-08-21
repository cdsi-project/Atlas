using CDSI.Agent.Application.Collections;
using CDSI.Agent.Core.Collections;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.Collections;

public sealed class AssetCollectionServiceTests
{
    [Fact]
    public async Task CreateAddRemoveDeleteAndPrepareSync_PreservesLocalAsset()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var file = new DiscoveredFile(
            Path.Combine(directory.Path, "episode.mp4"),
            "episode.mp4",
            ".mp4",
            "video/mp4",
            128,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var registered = Assert.Single(await repository.RegisterLocalFilesAsync(
            deviceId,
            [file],
            DateTimeOffset.UtcNow));
        var service = new AssetCollectionService(repository);

        var collection = await service.CreateAsync("  Episode 01  ", AssetCollectionType.Video);
        Assert.Equal("Episode 01", collection.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("episode 01", AssetCollectionType.Mixed));
        var membershipError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrepareSelectedSyncAsync(collection.Id, [registered.AssetId]));
        Assert.Contains("只有项目内资产可以同步到 OSS", membershipError.Message);
        Assert.Equal(1, await service.AddAssetsAsync(
            collection.Id,
            [registered.AssetId, registered.AssetId]));

        var plan = await service.PrepareSyncAsync(collection.Id);
        Assert.Single(plan.Members);
        Assert.Single(plan.Assets);
        Assert.Equal(0, plan.UnavailableAssetCount);
        var selectedPlan = await service.PrepareSelectedSyncAsync(
            collection.Id,
            [registered.AssetId, registered.AssetId]);
        Assert.Single(selectedPlan.Assets);
        Assert.Equal(collection.Id, selectedPlan.Collection.Id);

        Assert.Equal(1, await service.RemoveAssetsAsync(
            collection.Id,
            [registered.AssetId]));
        Assert.Empty(await service.GetMembersAsync(collection.Id));
        Assert.Equal(1, await service.AddAssetsAsync(
            collection.Id,
            [registered.AssetId]));

        var deleted = await service.DeleteAsync(collection.Id);

        Assert.Equal(collection.Id, deleted.Id);
        Assert.Equal(collection.Name, deleted.Name);
        Assert.Equal(collection.Type, deleted.Type);
        Assert.Empty(await service.ListAsync());
        Assert.Single(await repository.ListAssetsAsync(100));

        await using (var auditConnection = new SqliteConnection(
            $"Data Source={Path.Combine(directory.Path, "cdsi.db")};Pooling=False"))
        {
            await auditConnection.OpenAsync();
            await using (var auditCommand = auditConnection.CreateCommand())
            {
                auditCommand.CommandText =
                    """
                    SELECT collection_id, name, asset_count
                    FROM asset_collection_deletion_audit;
                    """;
                await using (var auditReader =
                    await auditCommand.ExecuteReaderAsync())
                {
                    Assert.True(await auditReader.ReadAsync());
                    Assert.Equal(collection.Id.ToString("D"), auditReader.GetString(0));
                    Assert.Equal("Episode 01", auditReader.GetString(1));
                    Assert.Equal(1, auditReader.GetInt32(2));
                    Assert.False(await auditReader.ReadAsync());
                }
            }

            await auditConnection.CloseAsync();
        }

        SqliteConnection.ClearAllPools();
    }
}
