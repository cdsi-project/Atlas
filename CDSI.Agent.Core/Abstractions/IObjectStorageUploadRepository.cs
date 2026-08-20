using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.Core.Abstractions;

public interface IObjectStorageUploadRepository
{
    Task CreateUploadJobAsync(
        ObjectStorageUploadJob job,
        IReadOnlyCollection<ObjectStorageUploadItem> items,
        CancellationToken cancellationToken = default);

    Task SaveUploadItemAsync(
        ObjectStorageUploadItem item,
        CancellationToken cancellationToken = default);

    Task UpdateUploadJobAsync(
        ObjectStorageUploadJob job,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageUploadAudit?> GetUploadJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    Task<MultipartUploadSession?> GetMultipartUploadSessionAsync(
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task SaveMultipartUploadSessionAsync(
        MultipartUploadSession session,
        CancellationToken cancellationToken = default);

    Task DeleteMultipartUploadSessionAsync(
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task SaveObjectStorageLocationAsync(
        ObjectStorageLocation location,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageLocation?> GetObjectStorageLocationAsync(
        Guid assetId,
        Guid storageProfileId,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ObjectStorageRestoreSource>> ListObjectStorageRestoreSourcesAsync(
        Guid assetId,
        CancellationToken cancellationToken = default);
}
