using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Infrastructure.Text;

public sealed class GenericTextExtractor : IAssetTextExtractor
{
    public string Name => "generic";

    public bool Supports(DiscoveredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return true;
    }

    public Task<TextExtractionResult> ExtractAsync(
        DiscoveredFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new TextExtractionResult(
            TextExtractionStatus.Unsupported));
    }
}
