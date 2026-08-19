using CDSI.Agent.Application.Collections;
using CDSI.Agent.Core.Collections;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.Collections;

public sealed class AssetCollectionServiceTests
{
    [Fact]
    public async Task CreateAddRemoveAndPrepareSync_PreservesLocalAsset()
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
        Assert.Equal(1, await service.AddAssetsAsync(
            collection.Id,
            [registered.AssetId, registered.AssetId]));

        var plan = await service.PrepareSyncAsync(collection.Id);
        Assert.Single(plan.Members);
        Assert.Single(plan.Assets);
        Assert.Equal(0, plan.UnavailableAssetCount);

        Assert.Equal(1, await service.RemoveAssetsAsync(
            collection.Id,
            [registered.AssetId]));
        Assert.Empty(await service.GetMembersAsync(collection.Id));
        Assert.Single(await repository.ListAssetsAsync(100));

        SqliteConnection.ClearAllPools();
    }
}
