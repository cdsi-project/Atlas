using System.Net;
using Amazon.S3;
using CDSI.Agent.Core.Storage;
using CDSI.Agent.Infrastructure.Storage;

namespace CDSI.Agent.Infrastructure.Tests.Storage;

public sealed class QiniuKodoStorageAdapterTests
{
    [Fact]
    public void Provider_IsQiniuKodo()
    {
        Assert.Equal(
            ObjectStorageProvider.QiniuKodo,
            new QiniuKodoStorageAdapter().Provider);
    }

    [Fact]
    public void CreateServiceUrl_UsesTheConfiguredProtocol()
    {
        var profile = CreateProfile(useHttps: true);

        Assert.Equal(
            "https://s3.cn-east-1.qiniucs.com",
            QiniuKodoStorageAdapter.CreateServiceUrl(profile));
    }

    [Fact]
    public void MissingClassifiers_UnwrapNestedExceptions()
    {
        var missingObject = new IOException(
            "wrapped",
            new AmazonS3Exception("missing")
            {
                StatusCode = HttpStatusCode.NotFound,
                ErrorCode = "NoSuchKey"
            });
        var forbidden = new AmazonS3Exception("forbidden")
        {
            StatusCode = HttpStatusCode.Forbidden,
            ErrorCode = "AccessDenied"
        };

        Assert.True(QiniuKodoStorageAdapter.IsMissingObject(missingObject));
        Assert.True(QiniuKodoStorageAdapter.IsMissingUpload(missingObject));
        Assert.False(QiniuKodoStorageAdapter.IsMissingObject(forbidden));
        Assert.False(QiniuKodoStorageAdapter.IsMissingUpload(forbidden));
    }

    private static ObjectStorageProfile CreateProfile(bool useHttps)
    {
        var now = DateTimeOffset.UtcNow;
        return new ObjectStorageProfile(
            Guid.NewGuid(),
            "七牛备份",
            ObjectStorageProvider.QiniuKodo,
            "s3.cn-east-1.qiniucs.com",
            "cdsi-assets",
            "cn-east-1",
            useHttps,
            "access-key-id",
            now,
            now);
    }
}
