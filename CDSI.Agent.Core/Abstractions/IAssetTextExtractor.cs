using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Core.Abstractions;

public interface IAssetTextExtractor
{
    string Name { get; }

    bool Supports(DiscoveredFile file);

    Task<TextExtractionResult> ExtractAsync(
        DiscoveredFile file,
        CancellationToken cancellationToken = default);
}
