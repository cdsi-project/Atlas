using CDSI.Agent.Core.Collections;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Storage;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Tests.Persistence;

public sealed class SqliteAssetCollectionRepositoryTests
{
    [Fact]
    public async Task CollectionBackupBinding_PersistsAndClearsWithItsProfile()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var now = DateTimeOffset.UtcNow;
        var profile = new ObjectStorageProfile(
            Guid.NewGuid(),
            "腾讯归档",
            ObjectStorageProvider.TencentCos,
            "https://cos.ap-beijing.myqcloud.com",
            "atlas-assets",
            "ap-beijing",
            UseHttps: true,
            "secret-id",
            now,
            now);
        await repository.SaveStorageProfileAsync(profile);
        var collection = new AssetCollection(
            Guid.NewGuid(),
            "项目 A",
            AssetCollectionType.Mixed,
            now,
            now,
            profile.Id);

        Assert.True(await repository.CreateAssetCollectionAsync(collection));

        var loaded = await repository.GetAssetCollectionAsync(collection.Id);
        var summary = Assert.Single(await repository.ListAssetCollectionsAsync());
        Assert.Equal(profile.Id, loaded?.BackupProfileId);
        Assert.Equal(profile.Id, summary.BackupProfileId);
        Assert.Equal("腾讯归档", summary.BackupProfileName);
        Assert.Equal(ObjectStorageProvider.TencentCos, summary.BackupProvider);

        Assert.True(await repository.DeleteStorageProfileAsync(profile.Id));

        loaded = await repository.GetAssetCollectionAsync(collection.Id);
        summary = Assert.Single(await repository.ListAssetCollectionsAsync());
        Assert.Null(loaded?.BackupProfileId);
        Assert.Null(summary.BackupProfileId);
        Assert.Null(summary.BackupProfileName);
        Assert.Null(summary.BackupProvider);
        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task CollectionMembership_IsIdempotentAndDoesNotDeleteAssets()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var first = CreateFile(Path.Combine(directory.Path, "first.mp4"), "first.mp4", 12);
        var second = CreateFile(Path.Combine(directory.Path, "cover.jpg"), "cover.jpg", 5);
        var registered = await repository.RegisterLocalFilesAsync(
            deviceId,
            [first, second],
            DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var collection = new AssetCollection(
            Guid.NewGuid(),
            "Episode 01",
            AssetCollectionType.Video,
            now,
            now);

        Assert.True(await repository.CreateAssetCollectionAsync(collection));
        Assert.False(await repository.CreateAssetCollectionAsync(
            collection with { Id = Guid.NewGuid(), Name = "episode 01" }));

        var assetIds = registered.Select(asset => asset.AssetId).ToArray();
        Assert.Equal(2, await repository.AddAssetsToCollectionAsync(
            collection.Id,
            assetIds,
            now.AddMinutes(1)));
        Assert.Equal(0, await repository.AddAssetsToCollectionAsync(
            collection.Id,
            assetIds,
            now.AddMinutes(2)));

        var summary = Assert.Single(await repository.ListAssetCollectionsAsync());
        var members = await repository.ListAssetCollectionMembersAsync(collection.Id);
        Assert.Equal(2, summary.AssetCount);
        Assert.Equal(17, summary.TotalSizeBytes);
        Assert.Equal(0, summary.BackedUpAssetCount);
        Assert.Equal(now, summary.CreatedAt);
        Assert.Equal(2, members.Count);
        Assert.All(members, member =>
            Assert.Equal(now.AddMinutes(1), member.AddedAt));
        Assert.Contains(members, member => member.Asset.OriginalFilename == "first.mp4");
        Assert.Contains(members, member => member.Asset.OriginalFilename == "cover.jpg");

        Assert.Equal(1, await repository.RemoveAssetsFromCollectionAsync(
            collection.Id,
            [assetIds[0]],
            now.AddMinutes(3)));
        Assert.Single(await repository.ListAssetCollectionMembersAsync(collection.Id));
        Assert.Equal(2, (await repository.ListAssetsAsync(100)).Count);

        SqliteConnection.ClearAllPools();
    }

    private static DiscoveredFile CreateFile(string path, string filename, long size)
    {
        return new DiscoveredFile(
            path,
            filename,
            Path.GetExtension(filename),
            filename.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                ? "video/mp4"
                : "image/jpeg",
            size,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
