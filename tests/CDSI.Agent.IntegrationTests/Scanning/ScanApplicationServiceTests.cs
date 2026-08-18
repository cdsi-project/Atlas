using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.Scanning;

public sealed class ScanApplicationServiceTests
{
    [Fact]
    public async Task ScanDirectoryAsync_IndexesIdempotentlyWithoutChangingSourceFiles()
    {
        using var directory = new TestDirectory();
        var scanRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Assets"));
        var nested = Directory.CreateDirectory(Path.Combine(scanRoot.FullName, "Nested"));
        var ignored = Directory.CreateDirectory(Path.Combine(scanRoot.FullName, ".git"));
        var articlePath = Path.Combine(scanRoot.FullName, "article.md");
        var imagePath = Path.Combine(nested.FullName, "cover.jpg");
        const string originalArticle = "# Private draft";

        await File.WriteAllTextAsync(articlePath, originalArticle);
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);
        await File.WriteAllTextAsync(Path.Combine(ignored.FullName, "config"), "ignored");

        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "State", "cdsi.db"));
        var service = new ScanApplicationService(new FileSystemScanner(), repository);
        await service.InitializeAsync();

        var firstScan = await service.ScanDirectoryAsync(scanRoot.FullName);
        var firstAssets = await service.ListAssetsAsync();
        var secondScan = await service.ScanDirectoryAsync(scanRoot.FullName);
        var secondAssets = await service.ListAssetsAsync();

        Assert.Equal(ScanJobStatus.Completed, firstScan.Status);
        Assert.Equal(2, firstScan.FilesIndexed);
        Assert.Equal(ScanJobStatus.Completed, secondScan.Status);
        Assert.Equal(2, secondScan.FilesIndexed);
        Assert.Equal(2, firstAssets.Count);
        Assert.Equal(2, secondAssets.Count);
        Assert.Equal(
            firstAssets.OrderBy(asset => asset.Path).Select(asset => asset.AssetId),
            secondAssets.OrderBy(asset => asset.Path).Select(asset => asset.AssetId));
        Assert.Equal(originalArticle, await File.ReadAllTextAsync(articlePath));

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task ScanDirectoryAsync_WhenAFileDisappears_MarksItsLocationMissing()
    {
        using var directory = new TestDirectory();
        var scanRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Assets"));
        var retainedPath = Path.Combine(scanRoot.FullName, "retained.txt");
        var removedPath = Path.Combine(scanRoot.FullName, "removed.txt");
        await File.WriteAllTextAsync(retainedPath, "retained");
        await File.WriteAllTextAsync(removedPath, "removed");

        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "State", "cdsi.db"));
        var service = new ScanApplicationService(new FileSystemScanner(), repository);
        await service.InitializeAsync();
        await service.ScanDirectoryAsync(scanRoot.FullName);

        File.Delete(removedPath);
        await service.ScanDirectoryAsync(scanRoot.FullName);
        var assets = await service.ListAssetsAsync();

        Assert.Equal(
            AssetLocationStatus.Available,
            assets.Single(asset => asset.Path == retainedPath).LocationStatus);
        Assert.Equal(
            AssetLocationStatus.Missing,
            assets.Single(asset => asset.Path == removedPath).LocationStatus);
        Assert.False(File.Exists(removedPath));
        Assert.Equal("retained", await File.ReadAllTextAsync(retainedPath));

        SqliteConnection.ClearAllPools();
    }
}
