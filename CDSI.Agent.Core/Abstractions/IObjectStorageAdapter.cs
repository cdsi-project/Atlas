using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.Core.Abstractions;

public interface IObjectStorageAdapter
{
    ObjectStorageProvider Provider { get; }

    Task<ObjectStorageObjectInfo?> StatAsync(
        ObjectStorageConnection connection,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageTransferResult> UploadAsync(
        ObjectStorageTransferRequest request,
        Func<MultipartUploadSession, CancellationToken, Task> saveCheckpoint,
        IProgress<ObjectStorageTransferProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageDownloadResult> DownloadAsync(
        ObjectStorageConnection connection,
        string objectKey,
        Stream destination,
        IProgress<ObjectStorageDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task AbortMultipartUploadAsync(
        ObjectStorageConnection connection,
        MultipartUploadSession session,
        CancellationToken cancellationToken = default);
}
