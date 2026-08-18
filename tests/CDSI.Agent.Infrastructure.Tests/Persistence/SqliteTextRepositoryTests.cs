using CDSI.Agent.Core.Text;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.Infrastructure.Tests.Persistence;

public sealed class SqliteTextRepositoryTests
{
    [Fact]
    public async Task SaveTextAsync_CachesCurrentTextAndInvalidatesItAfterChange()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var original = CreateFile(Path.Combine(directory.Path, "draft.md"), "draft.md");
        var registered = await repository.RegisterLocalFilesAsync(
            deviceId,
            [original],
            DateTimeOffset.UtcNow);

        var initialWork = await repository.GetTextWorkSummaryAsync(
            TextPipeline.CurrentVersion);
        var candidates = await repository.ListTextCandidatesAsync(
            TextPipeline.CurrentVersion,
            null,
            100);
        var text = new AssetText(
            registered[0].AssetId,
            "markdown",
            TextPipeline.CurrentVersion,
            TextExtractionStatus.Extracted,
            original.Size,
            original.ModifiedAt,
            new AssetTextContent(
                "草稿",
                "草稿正文",
                ["草稿", "素材"],
                "UTF-8",
                IsTruncated: false),
            DateTimeOffset.UtcNow,
            null);

        var saved = await repository.SaveTextAsync(text);
        var cachedWork = await repository.GetTextWorkSummaryAsync(
            TextPipeline.CurrentVersion);
        var loaded = await repository.GetTextAsync(registered[0].AssetId);
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
        var invalidatedWork = await repository.GetTextWorkSummaryAsync(
            TextPipeline.CurrentVersion);
        var changedAssets = await repository.ListAssetsAsync(100);
        var staleSave = await repository.SaveTextAsync(text);
        var loadedContent = Assert.IsType<AssetTextContent>(loaded?.Content);

        Assert.Equal(1, initialWork.Files);
        Assert.Single(candidates);
        Assert.True(saved);
        Assert.Equal(0, cachedWork.Files);
        Assert.Equal("草稿正文", loadedContent.PlainText);
        Assert.Equal(["草稿", "素材"], loadedContent.Headings);
        Assert.NotNull(Assert.Single(currentAssets).Text);
        Assert.Equal(1, invalidatedWork.Files);
        Assert.Null(Assert.Single(changedAssets).Text);
        Assert.False(staleSave);

        SqliteConnection.ClearAllPools();
    }

    private static DiscoveredFile CreateFile(string path, string filename)
    {
        return new DiscoveredFile(
            path,
            filename,
            Path.GetExtension(filename),
            "text/markdown",
            5,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }
}
