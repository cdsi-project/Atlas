using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Metadata;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Tests.Persistence;

public sealed class SqliteAssetRepositoryTests
{
    [Fact]
    public async Task RegisterLocalFilesAsync_IsIdempotentForTheSameDeviceAndPath()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var file = CreateFile(Path.Combine(directory.Path, "asset.txt"), "asset.txt");

        var first = await repository.RegisterLocalFilesAsync(
            deviceId,
            [file],
            DateTimeOffset.UtcNow);
        Assert.True(first[0].RequiresFingerprint);

        var saved = await repository.SaveSha256Async(
            first[0].AssetId,
            file.Size,
            file.ModifiedAt,
            new string('a', 64));
        var second = await repository.RegisterLocalFilesAsync(
            deviceId,
            [file],
            DateTimeOffset.UtcNow.AddSeconds(1));
        var assets = await repository.ListAssetsAsync(100);

        Assert.True(saved);
        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(first[0].AssetId, second[0].AssetId);
        Assert.False(second[0].RequiresFingerprint);
        Assert.Single(assets);

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task MarkMissingLocalLocationsAsync_MarksOnlyLocationsNotSeenByTheScan()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var root = Path.Combine(directory.Path, "Assets");
        Directory.CreateDirectory(root);

        var scanStartedAt = DateTimeOffset.UtcNow;
        var missingFile = CreateFile(Path.Combine(root, "missing.txt"), "missing.txt");
        var availableFile = CreateFile(Path.Combine(root, "available.txt"), "available.txt");

        await repository.RegisterLocalFilesAsync(
            deviceId,
            [missingFile],
            scanStartedAt.AddSeconds(-1));
        await repository.RegisterLocalFilesAsync(
            deviceId,
            [availableFile],
            scanStartedAt.AddSeconds(1));

        await repository.MarkMissingLocalLocationsAsync(deviceId, root, scanStartedAt);
        var assets = await repository.ListAssetsAsync(100);

        Assert.Equal(
            AssetLocationStatus.Missing,
            assets.Single(asset => asset.OriginalFilename == "missing.txt").LocationStatus);
        Assert.Equal(
            AssetLocationStatus.Available,
            assets.Single(asset => asset.OriginalFilename == "available.txt").LocationStatus);

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task ListExactDuplicateGroupsAsync_GroupsOnlyMatchingSha256Values()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();

        var firstFile = CreateFile(Path.Combine(directory.Path, "first.txt"), "first.txt");
        var secondFile = CreateFile(Path.Combine(directory.Path, "second.txt"), "second.txt");
        var differentFile = CreateFile(Path.Combine(directory.Path, "different.txt"), "different.txt");
        var registered = await repository.RegisterLocalFilesAsync(
            deviceId,
            [firstFile, secondFile, differentFile],
            DateTimeOffset.UtcNow);

        await repository.SaveSha256Async(
            registered[0].AssetId,
            firstFile.Size,
            firstFile.ModifiedAt,
            new string('a', 64));
        await repository.SaveSha256Async(
            registered[1].AssetId,
            secondFile.Size,
            secondFile.ModifiedAt,
            new string('a', 64));
        await repository.SaveSha256Async(
            registered[2].AssetId,
            differentFile.Size,
            differentFile.ModifiedAt,
            new string('b', 64));

        var groups = await repository.ListExactDuplicateGroupsAsync(100);

        var group = Assert.Single(groups);
        Assert.Equal(new string('a', 64), group.Sha256);
        Assert.Equal(2, group.Assets.Count);
        Assert.Contains(group.Assets, asset => asset.OriginalFilename == "first.txt");
        Assert.Contains(group.Assets, asset => asset.OriginalFilename == "second.txt");

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task SaveSha256Async_WhenMetadataChanged_DoesNotSaveAStaleHash()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var original = CreateFile(Path.Combine(directory.Path, "asset.txt"), "asset.txt");
        var registered = await repository.RegisterLocalFilesAsync(
            deviceId,
            [original],
            DateTimeOffset.UtcNow);
        var changed = original with
        {
            Size = original.Size + 1,
            ModifiedAt = original.ModifiedAt.AddSeconds(1)
        };
        await repository.RegisterLocalFilesAsync(
            deviceId,
            [changed],
            DateTimeOffset.UtcNow.AddSeconds(1));

        var saved = await repository.SaveSha256Async(
            registered[0].AssetId,
            original.Size,
            original.ModifiedAt,
            new string('a', 64));
        var current = await repository.RegisterLocalFilesAsync(
            deviceId,
            [changed],
            DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.False(saved);
        Assert.True(current[0].RequiresFingerprint);

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task InitializeAsync_UpgradesAVersionOneDatabase()
    {
        using var directory = new TestDirectory();
        var databasePath = Path.Combine(directory.Path, "cdsi.db");

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE schema_migrations (
                    version INTEGER NOT NULL PRIMARY KEY,
                    applied_at TEXT NOT NULL
                );
                CREATE TABLE assets (
                    id TEXT NOT NULL PRIMARY KEY
                );
                INSERT INTO schema_migrations(version, applied_at)
                VALUES (1, $applied_at);
                """;
            command.Parameters.AddWithValue("$applied_at", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteAssetRepository(databasePath);
        await repository.InitializeAsync();

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SELECT MAX(version) FROM schema_migrations;";
            var version = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());

            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'asset_metadata';
                """;
            var tableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync());

            Assert.Equal(2, version);
            Assert.Equal(1, tableCount);
        }

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task SaveMetadataAsync_CachesCurrentMetadataAndInvalidatesItAfterChange()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var original = CreateFile(Path.Combine(directory.Path, "photo.png"), "photo.png") with
        {
            MimeType = "image/png"
        };
        var registered = await repository.RegisterLocalFilesAsync(
            deviceId,
            [original],
            DateTimeOffset.UtcNow);

        var initialWork = await repository.GetMetadataWorkSummaryAsync(
            MetadataPipeline.CurrentVersion);
        var candidates = await repository.ListMetadataCandidatesAsync(
            MetadataPipeline.CurrentVersion,
            null,
            100);
        var metadata = new AssetMetadata(
            registered[0].AssetId,
            "test",
            MetadataPipeline.CurrentVersion,
            MetadataExtractionStatus.Extracted,
            original.Size,
            original.ModifiedAt,
            new AssetMetadataContent(AssetMediaKind.Image, Width: 1920, Height: 1080),
            DateTimeOffset.UtcNow,
            null);

        var saved = await repository.SaveMetadataAsync(metadata);
        var cachedWork = await repository.GetMetadataWorkSummaryAsync(
            MetadataPipeline.CurrentVersion);
        var loaded = await repository.GetMetadataAsync(registered[0].AssetId);
        var currentAssets = await repository.ListAssetsAsync(100);

        var changed = original with
        {
            Size = original.Size + 1,
            ModifiedAt = original.ModifiedAt.AddSeconds(1)
        };
        await repository.RegisterLocalFilesAsync(
            deviceId,
            [changed],
            DateTimeOffset.UtcNow.AddSeconds(1));
        var invalidatedWork = await repository.GetMetadataWorkSummaryAsync(
            MetadataPipeline.CurrentVersion);
        var changedAssets = await repository.ListAssetsAsync(100);

        Assert.Equal(1, initialWork.Files);
        Assert.Single(candidates);
        Assert.True(saved);
        Assert.Equal(0, cachedWork.Files);
        Assert.Equal(1920, loaded?.Content?.Width);
        Assert.NotNull(Assert.Single(currentAssets).Metadata);
        Assert.Equal(1, invalidatedWork.Files);
        Assert.Null(Assert.Single(changedAssets).Metadata);

        SqliteConnection.ClearAllPools();
    }

    private static DiscoveredFile CreateFile(string path, string filename)
    {
        return new DiscoveredFile(
            path,
            filename,
            Path.GetExtension(filename),
            "text/plain",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
