using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;
using Markdig;
using Markdig.Syntax;

namespace CDSI.Agent.Infrastructure.Text;

public sealed class MarkdownTextExtractor : IAssetTextExtractor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md", ".markdown", ".mdown"
        };

    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().Build();

    private readonly TextExtractionOptions _options;

    public MarkdownTextExtractor(TextExtractionOptions? options = null)
    {
        _options = options ?? new TextExtractionOptions();
        _options.Validate();
    }

    public string Name => "markdown";

    public bool Supports(DiscoveredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return SupportedExtensions.Contains(file.Extension);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        DiscoveredFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var decoded = await TextFileReader.ReadAsync(
            file.FullPath,
            _options,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var document = Markdown.Parse(decoded.Text, Pipeline);
        var headings = document
            .Descendants<HeadingBlock>()
            .Select(heading => ExtractHeading(decoded.Text, heading))
            .Where(heading => !string.IsNullOrWhiteSpace(heading));
        var plainText = Markdown.ToPlainText(decoded.Text, Pipeline);
        var content = TextExtractionUtilities.CreateContent(
            file,
            decoded,
            plainText,
            headings,
            _options);
        return new TextExtractionResult(
            TextExtractionStatus.Extracted,
            content);
    }

    private static string ExtractHeading(string markdown, HeadingBlock heading)
    {
        var start = heading.Span.Start;
        var end = heading.Span.End;
        if (start < 0 || end < start || end >= markdown.Length)
        {
            return string.Empty;
        }

        var source = markdown.Substring(start, end - start + 1);
        return Markdown.ToPlainText(source, Pipeline).Trim();
    }
}
