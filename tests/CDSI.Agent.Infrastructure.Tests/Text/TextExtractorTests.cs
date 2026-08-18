using System.Text;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;
using CDSI.Agent.Infrastructure.Text;

namespace CDSI.Agent.Infrastructure.Tests.Text;

public sealed class TextExtractorTests
{
    [Fact]
    public async Task PlainTextExtractor_ReadsUtf16BomAndReportsInputTruncation()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "notes.txt");
        const string source = "标题\n第一段内容";
        await File.WriteAllTextAsync(
            path,
            source,
            new UnicodeEncoding(
                bigEndian: false,
                byteOrderMark: true,
                throwOnInvalidBytes: true));
        var file = CreateDiscoveredFile(path);
        var extractor = new PlainTextExtractor(new TextExtractionOptions
        {
            MaximumInputBytes = 10,
            MaximumOutputCharacters = 100
        });

        var result = await extractor.ExtractAsync(file);

        Assert.Equal(TextExtractionStatus.Extracted, result.Status);
        Assert.Equal("UTF-16 LE", result.Content?.EncodingName);
        Assert.True(result.Content?.IsTruncated);
        Assert.StartsWith("标题", result.Content?.PlainText);
        Assert.Equal(source, await File.ReadAllTextAsync(path, Encoding.Unicode));
    }

    [Fact]
    public async Task PlainTextExtractor_DetectsGb18030()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "legacy.txt");
        const string source = "中文素材目录";
        var encoding = Encoding.GetEncoding(54936);
        await File.WriteAllBytesAsync(path, encoding.GetBytes(source));
        var file = CreateDiscoveredFile(path);
        var extractor = new PlainTextExtractor();

        var result = await extractor.ExtractAsync(file);

        Assert.Equal(TextExtractionStatus.Extracted, result.Status);
        Assert.Equal("GB18030", result.Content?.EncodingName);
        Assert.Equal(source, result.Content?.PlainText);
        Assert.Equal(encoding.GetBytes(source), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task MarkdownExtractor_ProducesTitleHeadingsAndPlainTextWithoutChangingSource()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "draft.md");
        const string source =
            """
            # 私人草稿

            这里是 **重点内容**。

            ## 素材清单

            - 视频
            - 图片
            """;
        await File.WriteAllTextAsync(path, source, new UTF8Encoding(false));
        var file = CreateDiscoveredFile(path);
        var extractor = new MarkdownTextExtractor();

        var result = await extractor.ExtractAsync(file);

        Assert.True(extractor.Supports(file));
        Assert.Equal(TextExtractionStatus.Extracted, result.Status);
        var content = Assert.IsType<AssetTextContent>(result.Content);
        Assert.Equal("私人草稿", content.Title);
        Assert.Equal(["私人草稿", "素材清单"], content.Headings);
        Assert.Contains("重点内容", content.PlainText);
        Assert.DoesNotContain("**", content.PlainText);
        Assert.Equal(source, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task PlainTextExtractor_EnforcesOutputLimit()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "long.txt");
        await File.WriteAllTextAsync(path, new string('a', 200));
        var file = CreateDiscoveredFile(path);
        var extractor = new PlainTextExtractor(new TextExtractionOptions
        {
            MaximumInputBytes = 1_024,
            MaximumOutputCharacters = 32
        });

        var result = await extractor.ExtractAsync(file);

        Assert.Equal(32, result.Content?.PlainText.Length);
        Assert.True(result.Content?.IsTruncated);
        Assert.Equal(new string('a', 200), await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task GenericExtractor_ReturnsUnsupportedWithoutReadingOrChangingFile()
    {
        using var directory = new TestDirectory();
        var path = Path.Combine(directory.Path, "archive.bin");
        byte[] source = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, source);
        var file = CreateDiscoveredFile(path);
        var extractor = new GenericTextExtractor();

        var result = await extractor.ExtractAsync(file);

        Assert.True(extractor.Supports(file));
        Assert.Equal(TextExtractionStatus.Unsupported, result.Status);
        Assert.Null(result.Content);
        Assert.Equal(source, await File.ReadAllBytesAsync(path));
    }

    private static DiscoveredFile CreateDiscoveredFile(string path)
    {
        var info = new FileInfo(path);
        return new DiscoveredFile(
            info.FullName,
            info.Name,
            info.Extension.ToLowerInvariant(),
            "text/plain",
            info.Length,
            new DateTimeOffset(info.CreationTimeUtc),
            new DateTimeOffset(info.LastWriteTimeUtc));
    }
}
