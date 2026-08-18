using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.Core.Abstractions;

public interface IAssetRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<string> GetOrCreateDeviceIdAsync(CancellationToken cancellationToken = default);

    Task<ScanRoot> GetOrCreateScanRootAsync(
        string path,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task CreateScanJobAsync(ScanJob job, CancellationToken cancellationToken = default);

    Task UpdateScanJobAsync(ScanJob job, CancellationToken cancellationToken = default);

    Task MarkScanRootCompletedAsync(
        Guid scanRootId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken = default);

    Task MarkMissingLocalLocationsAsync(
        string deviceId,
        string rootPath,
        DateTimeOffset scanStartedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetListItem>> RegisterLocalFilesAsync(
        string deviceId,
        IReadOnlyCollection<DiscoveredFile> files,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetListItem>> ListAssetsAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
