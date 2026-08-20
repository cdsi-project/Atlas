namespace CDSI.Agent.Core.OpenWeb;

public enum OpenWebPublisher
{
    WordPress
}

public enum OpenWebArticleStatus
{
    Draft,
    Published
}

public sealed record OpenWebPublication(
    Guid AssetId,
    OpenWebPublisher Publisher,
    string OriginDomain,
    long RemotePostId,
    string RemoteUrl,
    OpenWebArticleStatus Status,
    string ContentSha256,
    DateTimeOffset SynchronizedAt);

public sealed record OpenWebArticleContent(string Html);

public sealed record OpenWebArticlePayload(
    string Title,
    string Html,
    OpenWebArticleStatus Status);

public sealed record OpenWebRemoteArticle(
    long PostId,
    string Url,
    OpenWebArticleStatus Status);
