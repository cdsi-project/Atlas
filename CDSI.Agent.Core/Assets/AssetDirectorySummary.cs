namespace CDSI.Agent.Core.Assets;

public sealed record AssetDirectorySummary(
    string Path,
    long AssetCount,
    long AvailableAssetCount,
    long MissingAssetCount,
    long AvailableSizeBytes,
    DateTimeOffset LatestModifiedAt);
