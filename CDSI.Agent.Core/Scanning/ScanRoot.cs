namespace CDSI.Agent.Core.Scanning;

public sealed record ScanRoot(
    Guid Id,
    string Path,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastScannedAt);
