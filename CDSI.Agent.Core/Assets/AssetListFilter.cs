namespace CDSI.Agent.Core.Assets;

public sealed record AssetListFilter
{
    public static AssetListFilter Empty { get; } = new();

    public AssetListFilter(
        AssetFileTypeFilter fileType = AssetFileTypeFilter.All,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdBefore = null)
    {
        if (!Enum.IsDefined(fileType))
        {
            throw new ArgumentOutOfRangeException(nameof(fileType));
        }

        if (createdFrom is not null &&
            createdBefore is not null &&
            createdFrom >= createdBefore)
        {
            throw new ArgumentException("创建时间的结束边界必须晚于开始边界。");
        }

        FileType = fileType;
        CreatedFrom = createdFrom;
        CreatedBefore = createdBefore;
    }

    public AssetFileTypeFilter FileType { get; }

    public DateTimeOffset? CreatedFrom { get; }

    public DateTimeOffset? CreatedBefore { get; }

    public bool IsEmpty =>
        FileType == AssetFileTypeFilter.All &&
        CreatedFrom is null &&
        CreatedBefore is null;
}

public enum AssetFileTypeFilter
{
    All,
    Video,
    Audio,
    Image,
    Document,
    Other
}
