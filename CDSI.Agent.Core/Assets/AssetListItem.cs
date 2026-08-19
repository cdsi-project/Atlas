using CDSI.Agent.Core.Metadata;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Core.Assets;

public sealed record AssetListItem(
    Guid AssetId,
    string OriginalFilename,
    string Extension,
    string? MimeType,
    long Size,
    DateTimeOffset ModifiedAt,
    string Path,
    AssetLocationOwnership LocationOwnership,
    AssetLocationStatus LocationStatus,
    AssetStatus Status,
    AssetMetadata? Metadata = null,
    AssetText? Text = null);
