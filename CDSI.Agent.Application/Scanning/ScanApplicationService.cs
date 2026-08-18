using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Duplicates;
using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.Application.Scanning;

public sealed class ScanApplicationService
{
    private const int BatchSize = 200;

    private readonly IFileScanner _scanner;
    private readonly IAssetRepository _repository;

    public ScanApplicationService(
        IFileScanner scanner,
        IAssetRepository repository)
    {
        _scanner = scanner;
        _repository = repository;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return _repository.InitializeAsync(cancellationToken);
    }

    public Task<IReadOnlyList<AssetListItem>> ListAssetsAsync(
        int limit = 5_000,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return _repository.ListAssetsAsync(limit, cancellationToken);
    }

    public Task<IReadOnlyList<ExactDuplicateGroup>> ListExactDuplicateGroupsAsync(
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return _repository.ListExactDuplicateGroupsAsync(limit, cancellationToken);
    }

    public async Task<ScanSummary> ScanDirectoryAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRoot = Path.GetFullPath(rootPath);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException($"Scan root does not exist: {normalizedRoot}");
        }

        var startedAt = DateTimeOffset.UtcNow;
        progress?.Report(new ScanProgress(ScanStage.Initializing, 0, 0, 0, normalizedRoot));

        var scanRoot = await _repository.GetOrCreateScanRootAsync(
            normalizedRoot,
            startedAt,
            cancellationToken);
        var deviceId = await _repository.GetOrCreateDeviceIdAsync(cancellationToken);
        var job = new ScanJob(
            Guid.NewGuid(),
            scanRoot.Id,
            ScanJobStatus.Running,
            startedAt,
            null,
            0,
            0,
            0,
            null);

        await _repository.CreateScanJobAsync(job, cancellationToken);

        var buffer = new List<DiscoveredFile>(BatchSize);
        var discovered = 0;
        var indexed = 0;
        var errors = 0;

        void Report(ScanStage stage, string? path, string? message = null)
        {
            progress?.Report(new ScanProgress(
                stage,
                discovered,
                indexed,
                errors,
                path,
                message));
        }

        async Task RegisterAsync(
            IReadOnlyCollection<DiscoveredFile> files,
            CancellationToken token)
        {
            var registered = await _repository.RegisterLocalFilesAsync(
                deviceId,
                files,
                DateTimeOffset.UtcNow,
                token);
            indexed += registered.Count;
        }

        async Task FlushAsync(CancellationToken token)
        {
            if (buffer.Count == 0)
            {
                return;
            }

            var currentBatch = buffer.ToArray();
            buffer.Clear();

            try
            {
                await RegisterAsync(currentBatch, token);
            }
            catch (Exception batchException)
                when (currentBatch.Length > 1 && batchException is not OperationCanceledException)
            {
                foreach (var file in currentBatch)
                {
                    try
                    {
                        await RegisterAsync([file], token);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        errors++;
                        Report(ScanStage.Indexing, file.FullPath, exception.Message);
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                errors++;
                Report(ScanStage.Indexing, currentBatch[0].FullPath, exception.Message);
            }
        }

        try
        {
            await _scanner.ScanAsync(
                normalizedRoot,
                async (file, token) =>
                {
                    discovered++;
                    buffer.Add(file);

                    if (buffer.Count >= BatchSize)
                    {
                        Report(ScanStage.Indexing, file.FullPath);
                        await FlushAsync(token);
                    }
                    else if (discovered % 25 == 0)
                    {
                        Report(ScanStage.Discovering, file.FullPath);
                    }
                },
                (error, _) =>
                {
                    errors++;
                    Report(ScanStage.Discovering, error.Path, error.Message);
                    return ValueTask.CompletedTask;
                },
                cancellationToken);

            await FlushAsync(cancellationToken);
            await _repository.MarkMissingLocalLocationsAsync(
                deviceId,
                normalizedRoot,
                startedAt,
                cancellationToken);

            var completedAt = DateTimeOffset.UtcNow;
            job = job with
            {
                Status = ScanJobStatus.Completed,
                FinishedAt = completedAt,
                FilesDiscovered = discovered,
                FilesProcessed = indexed,
                Errors = errors
            };

            await _repository.UpdateScanJobAsync(job, cancellationToken);
            await _repository.MarkScanRootCompletedAsync(scanRoot.Id, completedAt, cancellationToken);
            Report(ScanStage.Completed, normalizedRoot);

            return new ScanSummary(
                job.Id,
                job.Status,
                discovered,
                indexed,
                errors);
        }
        catch (OperationCanceledException)
        {
            job = job with
            {
                Status = ScanJobStatus.Cancelled,
                FinishedAt = DateTimeOffset.UtcNow,
                FilesDiscovered = discovered,
                FilesProcessed = indexed,
                Errors = errors
            };
            await _repository.UpdateScanJobAsync(job, CancellationToken.None);
            Report(ScanStage.Cancelled, normalizedRoot);

            return new ScanSummary(
                job.Id,
                job.Status,
                discovered,
                indexed,
                errors);
        }
        catch (Exception exception)
        {
            job = job with
            {
                Status = ScanJobStatus.Failed,
                FinishedAt = DateTimeOffset.UtcNow,
                FilesDiscovered = discovered,
                FilesProcessed = indexed,
                Errors = errors + 1,
                ErrorMessage = exception.Message
            };
            await _repository.UpdateScanJobAsync(job, CancellationToken.None);
            errors++;
            Report(ScanStage.Failed, normalizedRoot, exception.Message);
            throw;
        }
    }
}
