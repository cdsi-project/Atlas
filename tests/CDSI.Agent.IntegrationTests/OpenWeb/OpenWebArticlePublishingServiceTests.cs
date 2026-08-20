using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.OpenWeb;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.OpenWeb;

public sealed class OpenWebArticlePublishingServiceTests
{
    [Fact]
    public async Task PublishAsync_UpdatesTheMappedWordPressPostOnRepeatedPublishing()
    {
        using var directory = new TestDirectory();
        var databasePath = Path.Combine(directory.Path, "cdsi.db");
        var sourcePath = Path.Combine(directory.Path, "article.md");
        await File.WriteAllTextAsync(sourcePath, "# Article");
        var fileInfo = new FileInfo(sourcePath);
        var repository = new SqliteAssetRepository(databasePath);
        await repository.InitializeAsync();
        var deviceId = await repository.GetOrCreateDeviceIdAsync();
        var registrations = await repository.RegisterLocalFilesAsync(
            deviceId,
            [new DiscoveredFile(
                sourcePath,
                fileInfo.Name,
                fileInfo.Extension,
                "text/markdown",
                fileInfo.Length,
                fileInfo.CreationTimeUtc,
                fileInfo.LastWriteTimeUtc)],
            DateTimeOffset.UtcNow);
        var assetId = Assert.Single(registrations).AssetId;
        var secretStore = new InMemorySecretStore();
        var settingsService = new OpenWebSettingsService(repository, secretStore);
        await settingsService.SaveAsync(
            "example.com",
            "editor",
            "application-password");
        var publisher = new RecordingPublisher();
        var service = new OpenWebArticlePublishingService(
            settingsService,
            repository,
            new StubContentReader(),
            publisher);
        var request = new OpenWebArticlePublishRequest(
            assetId,
            sourcePath,
            "Article",
            OpenWebArticleStatus.Draft);

        var created = await service.PublishAsync(request);
        var updated = await service.PublishAsync(
            request with { Status = OpenWebArticleStatus.Published });
        var saved = await repository.GetOpenWebPublicationAsync(
            assetId,
            OpenWebPublisher.WordPress,
            "example.com");

        Assert.True(created.WasCreated);
        Assert.False(updated.WasCreated);
        Assert.Equal([null, 42L], publisher.RemotePostIds);
        Assert.NotNull(saved);
        Assert.Equal(42, saved.RemotePostId);
        Assert.Equal(OpenWebArticleStatus.Published, saved.Status);
        Assert.Equal(64, saved.ContentSha256.Length);
        SqliteConnection.ClearAllPools();
    }

    private sealed class StubContentReader : IOpenWebArticleContentReader
    {
        public bool Supports(string path)
        {
            return true;
        }

        public Task<OpenWebArticleContent> ReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OpenWebArticleContent("<p>Article</p>"));
        }
    }

    private sealed class RecordingPublisher : IOpenWebArticlePublisher
    {
        public List<long?> RemotePostIds { get; } = [];

        public OpenWebPublisher Publisher => OpenWebPublisher.WordPress;

        public Task<OpenWebRemoteArticle> PublishAsync(
            OpenWebConnection connection,
            OpenWebArticlePayload article,
            long? remotePostId,
            CancellationToken cancellationToken = default)
        {
            RemotePostIds.Add(remotePostId);
            return Task.FromResult(new OpenWebRemoteArticle(
                42,
                "https://example.com/article",
                article.Status));
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _secrets = [];

        public Task StoreAsync(
            string key,
            string secret,
            CancellationToken cancellationToken = default)
        {
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_secrets.ContainsKey(key));
        }

        public Task<string?> RetrieveAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_secrets.GetValueOrDefault(key));
        }

        public Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }
}
