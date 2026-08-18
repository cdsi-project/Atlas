namespace CDSI.Agent.Core.Assets;

public sealed record AssetStatistics(
    long FileCount,
    long TotalSizeBytes,
    long VideoFileCount,
    long VideoDurationMilliseconds);
