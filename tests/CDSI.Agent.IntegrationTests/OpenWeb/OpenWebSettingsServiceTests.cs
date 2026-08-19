using CDSI.Agent.Application.OpenWeb;
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
        var service = new OpenWebSettingsService(repository);

        var initial = await service.GetAsync();
        var saved = await service.SaveAsync("ORIGIN.Example.COM.");
        var reloaded = await new OpenWebSettingsService(repository).GetAsync();

        Assert.Null(initial.OriginDomain);
        Assert.Equal("origin.example.com", saved.OriginDomain);
        Assert.Equal("origin.example.com", reloaded.OriginDomain);
        Assert.NotNull(reloaded.UpdatedAt);

        await service.SaveAsync(" ");
        var cleared = await service.GetAsync();

        Assert.Null(cleared.OriginDomain);
        Assert.Null(cleared.UpdatedAt);
        SqliteConnection.ClearAllPools();
    }
}
