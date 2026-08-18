namespace CDSI.Agent.Core.Assets;

public sealed record AssetLocation(
    Guid Id,
    Guid AssetId,
    AssetLocationType Type,
    string DeviceId,
    string Path,
    AssetLocationStatus Status,
    DateTimeOffset LastSeenAt,
    DateTimeOffset? LastVerifiedAt);

public enum AssetLocationType
{
    Local,
    ObjectStorage,
    NetworkStorage
}

public enum AssetLocationStatus
{
    Available,
    Missing,
    Unverified
}
