using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Infrastructure.FileSystem;

namespace CDSI.Agent.Infrastructure.Tests.FileSystem;

public sealed class FileSystemScannerTests
{
    [Fact]
    public async Task ScanAsync_DiscoversNestedFilesAndRespectsDefaultIgnores()
    {
        using var directory = new TestDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(directory.Path, "Nested"));
        var ignoredGit = Directory.CreateDirectory(Path.Combine(directory.Path, ".git"));
        var ignoredModules = Directory.CreateDirectory(Path.Combine(directory.Path, "node_modules"));

        await File.WriteAllTextAsync(Path.Combine(directory.Path, "root.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(nested.FullName, "child.md"), "child");
        await File.WriteAllTextAsync(Path.Combine(ignoredGit.FullName, "secret.txt"), "ignored");
        await File.WriteAllTextAsync(Path.Combine(ignoredModules.FullName, "package.js"), "ignored");

        var discovered = new List<DiscoveredFile>();
        var errors = new List<ScanError>();
        var scanner = new FileSystemScanner();

        await scanner.ScanAsync(
            directory.Path,
            (file, _) =>
            {
                discovered.Add(file);
                return ValueTask.CompletedTask;
            },
            (error, _) =>
            {
                errors.Add(error);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.Empty(errors);
        Assert.Equal(2, discovered.Count);
        Assert.Contains(discovered, file => file.OriginalFilename == "root.txt");
        Assert.Contains(discovered, file => file.OriginalFilename == "child.md");
        Assert.DoesNotContain(discovered, file => file.FullPath.Contains(".git"));
        Assert.DoesNotContain(discovered, file => file.FullPath.Contains("node_modules"));
    }

    [Fact]
    public async Task ScanAsync_ReportsKnownMimeTypesAndPreservesUnknownFiles()
    {
        using var directory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "notes.md"), "# Notes");
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "source.custom"), "data");

        var discovered = new List<DiscoveredFile>();
        var scanner = new FileSystemScanner();

        await scanner.ScanAsync(
            directory.Path,
            (file, _) =>
            {
                discovered.Add(file);
                return ValueTask.CompletedTask;
            },
            (_, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.Equal("text/markdown", discovered.Single(file => file.Extension == ".md").MimeType);
        Assert.Null(discovered.Single(file => file.Extension == ".custom").MimeType);
    }
}
