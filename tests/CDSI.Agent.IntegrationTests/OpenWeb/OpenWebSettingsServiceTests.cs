using CDSI.Agent.Application.OpenWeb;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.OpenWeb;

public sealed class OpenWebSettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_PersistsNormalizesAndClearsTheOriginDomain()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var secretStore = new InMemorySecretStore();
        var service = new OpenWebSettingsService(repository, secretStore);

        var initial = await service.GetAsync();
        var saved = await service.SaveAsync(
            "ORIGIN.Example.COM.",
            " editor ",
            "abcd efgh ijkl mnop");
        var reloaded = await new OpenWebSettingsService(
            repository,
            secretStore).GetAsync();
        var connection = await service.GetConnectionAsync();

        Assert.Null(initial.OriginDomain);
        Assert.False(initial.HasApplicationPassword);
        Assert.Equal("origin.example.com", saved.OriginDomain);
        Assert.Equal("editor", saved.WordPressUsername);
        Assert.True(saved.HasApplicationPassword);
        Assert.Equal("origin.example.com", reloaded.OriginDomain);
        Assert.Equal("editor", reloaded.WordPressUsername);
        Assert.True(reloaded.HasApplicationPassword);
        Assert.Equal("abcdefghijklmnop", connection.ApplicationPassword);
        Assert.DoesNotContain(
            connection.ApplicationPassword,
            connection.ToString(),
            StringComparison.Ordinal);
        Assert.NotNull(reloaded.UpdatedAt);

        await using (var sqliteConnection = new SqliteConnection(
                         $"Data Source={Path.Combine(directory.Path, "cdsi.db")}"))
        {
            await sqliteConnection.OpenAsync();
            await using var command = sqliteConnection.CreateCommand();
            command.CommandText = "SELECT group_concat(setting_value, '|') FROM agent_settings;";
            var storedValues = (string?)await command.ExecuteScalarAsync();
            Assert.DoesNotContain(
                "abcdefghijklmnop",
                storedValues ?? string.Empty,
                StringComparison.Ordinal);
        }

        await service.SaveAsync(" ", " ", " ");
        var cleared = await service.GetAsync();

        Assert.Null(cleared.OriginDomain);
        Assert.Null(cleared.WordPressUsername);
        Assert.False(cleared.HasApplicationPassword);
        Assert.Null(cleared.UpdatedAt);
        SqliteConnection.ClearAllPools();
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
