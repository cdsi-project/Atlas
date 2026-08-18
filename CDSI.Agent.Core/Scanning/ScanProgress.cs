namespace CDSI.Agent.Core.Scanning;

public sealed record ScanProgress(
    ScanStage Stage,
    int FilesDiscovered,
    int FilesIndexed,
    int Errors,
    string? CurrentPath,
    string? Message = null,
    int FilesFingerprinted = 0);

public enum ScanStage
{
    Initializing,
    Discovering,
    Indexing,
    Fingerprinting,
    Completed,
    Cancelled,
    Failed
}

public sealed record ScanSummary(
    Guid JobId,
    ScanJobStatus Status,
    int FilesDiscovered,
    int FilesIndexed,
    int Errors,
    int FilesFingerprinted = 0);
