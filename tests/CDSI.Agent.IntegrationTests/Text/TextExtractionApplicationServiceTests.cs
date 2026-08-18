using CDSI.Agent.Application.Scanning;
using CDSI.Agent.Application.Text;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;
using CDSI.Agent.Infrastructure.FileSystem;
using CDSI.Agent.Infrastructure.Persistence;
using CDSI.Agent.Infrastructure.Text;
using Microsoft.Data.Sqlite;

namespace CDSI.Agent.IntegrationTests.Text;

public sealed class TextExtractionApplicationServiceTests
{
    [Fact]
    public async Task ProcessPendingAsync_WhenOneExtractorFails_RecordsErrorAndContinues()
    {
        using var directory = new TestDirectory();
        var scanRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Assets"));
        var badPath = Path.Combine(scanRoot.FullName, "broken.bad");
        var notesPath = Path.Combine(scanRoot.FullName, "notes.txt");
        var binaryPath = Path.Combine(scanRoot.FullName, "archive.bin");
        await File.WriteAllBytesAsync(badPath, [1, 2, 3]);
        await File.WriteAllTextAsync(notesPath, "private notes");
        await File.WriteAllBytesAsync(binaryPath, [4, 5, 6]);

        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "State", "cdsi.db"));
        var scanService = new ScanApplicationService(new FileSystemScanner(), repository);
        var textService = new TextExtractionApplicationService(
            [
                new FailingTextExtractor(),
                new PlainTextExtractor(),
                new GenericTextExtractor()
            ],
            repository);
        await scanService.InitializeAsync();
        await scanService.ScanDirectoryAsync(scanRoot.FullName);

        var summary = await textService.ProcessPendingAsync();
        var cached = await textService.ProcessPendingAsync();
        var assets = await scanService.ListAssetsAsync();
        var failed = assets.Single(asset => asset.Path == badPath).Text;
        var extracted = assets.Single(asset => asset.Path == notesPath).Text;
        var unsupported = assets.Single(asset => asset.Path == binaryPath).Text;

        Assert.Equal(3, summary.TotalFiles);
        Assert.Equal(1, summary.ExtractedFiles);
        Assert.Equal(1, summary.UnsupportedFiles);
        Assert.Equal(1, summary.Errors);
        Assert.False(summary.Cancelled);
        Assert.Equal(0, cached.TotalFiles);
        Assert.Equal(TextExtractionStatus.Error, failed?.Status);
        Assert.Contains("Expected text failure", failed?.ErrorMessage);
        Assert.Equal("private notes", extracted?.Content?.PlainText);
        Assert.Equal(TextExtractionStatus.Unsupported, unsupported?.Status);
        Assert.Equal("private notes", await File.ReadAllTextAsync(notesPath));

        SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task ProcessPendingAsync_WhenCancelled_ResumesFromCachedFiles()
    {
        using var directory = new TestDirectory();
        var scanRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "Assets"));
        await File.WriteAllTextAsync(Path.Combine(scanRoot.FullName, "one.txt"), "one");
        await File.WriteAllTextAsync(Path.Combine(scanRoot.FullName, "two.txt"), "two");
        await File.WriteAllTextAsync(Path.Combine(scanRoot.FullName, "three.txt"), "three");

        var repository = new SqliteAssetRepository(
            Path.Combine(directory.Path, "State", "cdsi.db"));
        var scanService = new ScanApplicationService(new FileSystemScanner(), repository);
        var textService = new TextExtractionApplicationService(
            [new PlainTextExtractor(), new GenericTextExtractor()],
            repository);
        await scanService.InitializeAsync();
        await scanService.ScanDirectoryAsync(scanRoot.FullName);

        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<TextProgress>(value =>
        {
            if (value.ExtractedFiles == 1)
            {
                cancellation.Cancel();
            }
        });

        var cancelled = await textService.ProcessPendingAsync(
            progress,
            cancellation.Token);
        var resumed = await textService.ProcessPendingAsync();
        var cached = await textService.ProcessPendingAsync();

        Assert.True(cancelled.Cancelled);
        Assert.Equal(1, cancelled.ExtractedFiles);
        Assert.Equal(2, resumed.TotalFiles);
        Assert.Equal(2, resumed.ExtractedFiles);
        Assert.False(resumed.Cancelled);
        Assert.Equal(0, cached.TotalFiles);

        SqliteConnection.ClearAllPools();
    }

    private sealed class FailingTextExtractor : IAssetTextExtractor
    {
        public string Name => "failing-test";

        public bool Supports(DiscoveredFile file)
        {
            return file.Extension.Equals(".bad", StringComparison.OrdinalIgnoreCase);
        }

        public Task<TextExtractionResult> ExtractAsync(
            DiscoveredFile file,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidDataException("Expected text failure.");
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        private readonly Action<T> _report = report;

        public void Report(T value)
        {
            _report(value);
        }
    }
}
