namespace CDSI.Agent.Core.Scanning;

public sealed record ScanRoot(
    Guid Id,
    string Path,
    ScanRootMode Mode,
    bool Enabled,
    ScanRootStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastScannedAt,
    DateTimeOffset? RemovedAt,
    Guid? LocalVolumeId = null,
    string? VolumeRelativePath = null);

public enum ScanRootMode
{
    Readonly,
    Managed
}

public enum ScanRootStatus
{
    Active,
    Disabled,
    Unavailable,
    Offline,
    Error,
    Removed
}
