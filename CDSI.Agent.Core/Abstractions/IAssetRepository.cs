using CDSI.Agent.Core.Assets;
using CDSI.Agent.Core.Duplicates;
using CDSI.Agent.Core.Fingerprints;
using CDSI.Agent.Core.Metadata;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

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

    Task<IReadOnlyList<RegisteredLocalAsset>> RegisterLocalFilesAsync(
        string deviceId,
        IReadOnlyCollection<DiscoveredFile> files,
        DateTimeOffset discoveredAt,
        CancellationToken cancellationToken = default);

    Task<bool> SaveSha256Async(
        Guid assetId,
        long expectedSize,
        DateTimeOffset expectedModifiedAt,
        string sha256,
        CancellationToken cancellationToken = default);

    Task<FingerprintWorkSummary> GetFingerprintWorkSummaryAsync(
        FingerprintMode mode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FingerprintCandidate>> ListFingerprintCandidatesAsync(
        FingerprintMode mode,
        Guid? afterAssetId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<MetadataWorkSummary> GetMetadataWorkSummaryAsync(
        int pipelineVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetadataCandidate>> ListMetadataCandidatesAsync(
        int pipelineVersion,
        Guid? afterAssetId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> SaveMetadataAsync(
        AssetMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<AssetMetadata?> GetMetadataAsync(
        Guid assetId,
        CancellationToken cancellationToken = default);

    Task<TextWorkSummary> GetTextWorkSummaryAsync(
        int pipelineVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TextCandidate>> ListTextCandidatesAsync(
        int pipelineVersion,
        Guid? afterAssetId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> SaveTextAsync(
        AssetText text,
        CancellationToken cancellationToken = default);

    Task<AssetText?> GetTextAsync(
        Guid assetId,
        CancellationToken cancellationToken = default);

    Task<AssetStatistics> GetLocalAssetStatisticsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetListItem>> ListAssetsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExactDuplicateGroup>> ListExactDuplicateGroupsAsync(
        int limit,
        CancellationToken cancellationToken = default);
}
