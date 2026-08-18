using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Infrastructure.Text;

public sealed class PlainTextExtractor : IAssetTextExtractor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt"
        };

    private readonly TextExtractionOptions _options;

    public PlainTextExtractor(TextExtractionOptions? options = null)
    {
        _options = options ?? new TextExtractionOptions();
        _options.Validate();
    }

    public string Name => "plain-text";

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
        var content = TextExtractionUtilities.CreateContent(
            file,
            decoded,
            decoded.Text,
            headings: null,
            _options);
        return new TextExtractionResult(
            TextExtractionStatus.Extracted,
            content);
    }
}
