using System.Net;
using System.Text;
using System.Text.Json;
using CDSI.Agent.Core.OpenWeb;
using CDSI.Agent.Infrastructure.OpenWeb;

namespace CDSI.Agent.Infrastructure.Tests.OpenWeb;

public sealed class WordPressArticlePublisherTests
{
    [Theory]
    [InlineData(null, "/wp-json/wp/v2/posts")]
    [InlineData(42L, "/wp-json/wp/v2/posts/42")]
    public async Task PublishAsync_CreatesOrUpdatesAWordPressPost(
        long? remotePostId,
        string expectedPath)
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var publisher = new WordPressArticlePublisher(httpClient);

        var result = await publisher.PublishAsync(
            new OpenWebConnection("example.com", "editor", "secret"),
            new OpenWebArticlePayload(
                "文章标题",
                "<p>正文</p>",
                OpenWebArticleStatus.Published),
            remotePostId);

        Assert.Equal(42, result.PostId);
        Assert.Equal(OpenWebArticleStatus.Published, result.Status);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Head, handler.Requests[0].Method);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal(expectedPath, post.Uri.AbsolutePath);
        Assert.Equal("id,link,status", GetQueryValue(post.Uri, "_fields"));
        Assert.Equal(
            Convert.ToBase64String(Encoding.UTF8.GetBytes("editor:secret")),
            post.AuthorizationParameter);

        using var body = JsonDocument.Parse(post.Body!);
        Assert.Equal("文章标题", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("<p>正文</p>", body.RootElement.GetProperty("content").GetString());
        Assert.Equal("publish", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PublishAsync_IdentifiesADeletedMappedPost()
    {
        var handler = new RecordingHandler("rest_post_invalid_id");
        using var httpClient = new HttpClient(handler);
        var publisher = new WordPressArticlePublisher(httpClient);

        var exception = await Assert.ThrowsAsync<OpenWebRemoteArticleNotFoundException>(() =>
            publisher.PublishAsync(
                new OpenWebConnection("example.com", "editor", "secret"),
                new OpenWebArticlePayload(
                    "文章标题",
                    "<p>正文</p>",
                    OpenWebArticleStatus.Draft),
                42));

        Assert.Equal(42, exception.RemoteArticleId);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/wp-json/wp/v2/posts/42", handler.Requests[1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task PublishAsync_DoesNotTreatAnUnrelated404AsADeletedPost()
    {
        var handler = new RecordingHandler("rest_no_route");
        using var httpClient = new HttpClient(handler);
        var publisher = new WordPressArticlePublisher(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(
                new OpenWebConnection("example.com", "editor", "secret"),
                new OpenWebArticlePayload(
                    "文章标题",
                    "<p>正文</p>",
                    OpenWebArticleStatus.Draft),
                42));

        Assert.Contains("HTTP 404", exception.Message);
        Assert.Contains("Invalid post ID", exception.Message);
        Assert.IsNotType<OpenWebRemoteArticleNotFoundException>(exception);
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .Where(parts => string.Equals(parts[0], key, StringComparison.Ordinal))
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .SingleOrDefault();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string? _postErrorCode;

        public RecordingHandler(string? postErrorCode = null)
        {
            _postErrorCode = postErrorCode;
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.Parameter,
                request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (request.Method == HttpMethod.Head)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.TryAddWithoutValidation(
                    "Link",
                    "<https://example.com/wp-json/>; rel=\"https://api.w.org/\"");
                return response;
            }

            if (_postErrorCode is not null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(
                        $"{{\"code\":\"{_postErrorCode}\",\"message\":\"Invalid post ID.\",\"data\":{{\"status\":404}}}}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":42,\"link\":\"https://example.com/article\",\"status\":\"publish\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? AuthorizationParameter,
        string? Body);
}
