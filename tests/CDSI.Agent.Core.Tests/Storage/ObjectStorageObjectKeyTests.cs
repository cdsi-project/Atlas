using CDSI.Agent.Core.Storage;

namespace CDSI.Agent.Core.Tests.Storage;

public sealed class ObjectStorageObjectKeyTests
{
    [Fact]
    public void TryCreateForAsset_PreservesTheRequestedUnicodeFilename()
    {
        var assetId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

        var success = ObjectStorageObjectKey.TryCreateForAsset(
            assetId,
            "  成片-最终版.mp4  ",
            out var objectKey,
            out var errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.Equal(
            "assets/00112233445566778899aabbccddeeff/  成片-最终版.mp4  ",
            objectKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("folder/file.mp4")]
    [InlineData("folder\\file.mp4")]
    [InlineData("line\nbreak.mp4")]
    public void TryCreateForAsset_RejectsValuesThatAreNotSingleFilenames(
        string filename)
    {
        var success = ObjectStorageObjectKey.TryCreateForAsset(
            Guid.NewGuid(),
            filename,
            out var objectKey,
            out var errorMessage);

        Assert.False(success);
        Assert.Empty(objectKey);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
    }

    [Fact]
    public void TryCreateForAsset_RejectsAnObjectKeyOverTheOssLimit()
    {
        var success = ObjectStorageObjectKey.TryCreateForAsset(
            Guid.NewGuid(),
            new string('界', 400),
            out _,
            out var errorMessage);

        Assert.False(success);
        Assert.Equal("OSS 文件名过长。", errorMessage);
    }
}
