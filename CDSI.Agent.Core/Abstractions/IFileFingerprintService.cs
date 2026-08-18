using CDSI.Agent.Core.Fingerprints;
using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.Core.Abstractions;

public interface IFileFingerprintService
{
    Task<FileFingerprint> CalculateAsync(
        DiscoveredFile file,
        CancellationToken cancellationToken = default);
}
