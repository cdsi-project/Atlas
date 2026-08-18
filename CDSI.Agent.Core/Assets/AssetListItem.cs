namespace CDSI.Agent.Core.Assets;

public sealed record AssetListItem(
    Guid AssetId,
    string OriginalFilename,
    string Extension,
    string? MimeType,
    long Size,
    DateTimeOffset ModifiedAt,
    string Path,
    AssetLocationStatus LocationStatus,
    AssetStatus Status);
