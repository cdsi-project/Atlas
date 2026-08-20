using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.Core.Tests.Assets;

public sealed class AssetListFilterTests
{
    [Fact]
    public void Empty_HasNoActiveConditions()
    {
        Assert.True(AssetListFilter.Empty.IsEmpty);
    }

    [Fact]
    public void Constructor_RejectsAnInvalidCreationTimeRange()
    {
        var boundary = DateTimeOffset.Parse("2026-08-20T00:00:00Z");

        Assert.Throws<ArgumentException>(() => new AssetListFilter(
            AssetFileTypeFilter.All,
            boundary,
            boundary));
    }

    [Fact]
    public void Constructor_RecognizesCombinedConditions()
    {
        var filter = new AssetListFilter(
            AssetFileTypeFilter.Video,
            DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            DateTimeOffset.Parse("2026-09-01T00:00:00Z"));

        Assert.False(filter.IsEmpty);
        Assert.Equal(AssetFileTypeFilter.Video, filter.FileType);
    }
}
