using CDSI.Agent.Application.Git;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Git;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.Git;

public sealed class GitProfileServiceTests
{
    [Fact]
    public async Task Profiles_PersistWithoutTokensAndMaintainOneDefault()
    {
        using var directory = new TestDirectory();
        var databasePath = Path.Combine(directory.Path, "cdsi.db");
        var repository = new SqliteAssetRepository(databasePath);
        await repository.InitializeAsync();
        var secretStore = new InMemorySecretStore();
        var service = new GitProfileService(repository, secretStore);

        var github = await service.SaveAsync(new SaveGitProfileRequest(
            null,
            "主 GitHub",
            GitHostingProvider.GitHub,
            "https://github.com/cdsi-project/Atlas.git",
            "cdsi-project",
            "main",
            null,
            IsDefault: false));
        var gitee = await service.SaveAsync(new SaveGitProfileRequest(
            null,
            "码云镜像",
            GitHostingProvider.Gitee,
            "git@gitee.com:cdsi-project/atlas.git",
            "cdsi-project",
            "master",
            "gitee-private-token",
            IsDefault: true));

        var profiles = await service.ListAsync();
        Assert.Equal(2, profiles.Count);
        Assert.Equal(gitee.Profile.Id, Assert.Single(
            profiles,
            profile => profile.Profile.IsDefault).Profile.Id);
        Assert.False(profiles.Single(
            profile => profile.Profile.Id == github.Profile.Id).HasAccessToken);
        Assert.True(profiles.Single(
            profile => profile.Profile.Id == gitee.Profile.Id).HasAccessToken);
        Assert.Equal(
            "gitee-private-token",
            await service.GetAccessTokenAsync(gitee.Profile.Id));

        var updated = await service.SaveAsync(new SaveGitProfileRequest(
            gitee.Profile.Id,
            "码云镜像（更新）",
            GitHostingProvider.Gitee,
            gitee.Profile.RepositoryUrl,
            gitee.Profile.AccountName,
            gitee.Profile.DefaultBranch,
            null,
            IsDefault: false));
        Assert.True(updated.HasAccessToken);
        Assert.Equal(
            "gitee-private-token",
            await service.GetAccessTokenAsync(gitee.Profile.Id));

        await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
        {
            await connection.OpenAsync();
            await using var columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = "PRAGMA table_info(git_profiles);";
            var columns = new List<string>();
            await using (var reader = await columnsCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1));
                }
            }

            Assert.DoesNotContain(
                columns,
                column => column.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    column.Contains("secret", StringComparison.OrdinalIgnoreCase));

            await using var valuesCommand = connection.CreateCommand();
            valuesCommand.CommandText =
                "SELECT group_concat(display_name || repository_url || account_name, '|') FROM git_profiles;";
            var values = (string?)await valuesCommand.ExecuteScalarAsync();
            Assert.DoesNotContain("gitee-private-token", values ?? string.Empty);
        }

        await service.DeleteAsync(gitee.Profile.Id);
        var remaining = Assert.Single(await service.ListAsync());
        Assert.Equal(github.Profile.Id, remaining.Profile.Id);
        Assert.True(remaining.Profile.IsDefault);
        Assert.Null(await secretStore.RetrieveAsync(
            $"git-access-token-{gitee.Profile.Id:N}"));

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task SaveAsync_RejectsAProviderUrlMismatch()
    {
        using var directory = new TestDirectory();
        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "cdsi.db"));
        await repository.InitializeAsync();
        var service = new GitProfileService(repository, new InMemorySecretStore());

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new SaveGitProfileRequest(
                null,
                "错误配置",
                GitHostingProvider.GitHub,
                "https://gitee.com/owner/repository.git",
                "owner",
                "main",
                null,
                IsDefault: false)));

        Assert.Contains("github.com", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await service.ListAsync());
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
