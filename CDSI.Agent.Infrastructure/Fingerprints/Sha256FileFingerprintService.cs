using System.Security.Cryptography;
using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Fingerprints;
using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.Infrastructure.Fingerprints;

public sealed class Sha256FileFingerprintService : IFileFingerprintService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<FileFingerprint> CalculateAsync(
        DiscoveredFile file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        cancellationToken.ThrowIfCancellationRequested();

        var before = ReadCurrentMetadata(file);
        EnsureUnchanged(file, before);

        await using var stream = new FileStream(
            file.FullPath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);

        var after = ReadCurrentMetadata(file);
        EnsureUnchanged(file, after);

        return new FileFingerprint(
            Convert.ToHexStringLower(hash),
            after.Length,
            new DateTimeOffset(after.LastWriteTimeUtc));
    }

    private static FileInfo ReadCurrentMetadata(DiscoveredFile file)
    {
        var info = new FileInfo(file.FullPath);
        info.Refresh();

        if (!info.Exists)
        {
            throw new FileNotFoundException("File disappeared before fingerprinting.", file.FullPath);
        }

        return info;
    }

    private static void EnsureUnchanged(DiscoveredFile expected, FileInfo actual)
    {
        if (actual.Length != expected.Size ||
            actual.LastWriteTimeUtc != expected.ModifiedAt.UtcDateTime)
        {
            throw new FileChangedDuringFingerprintException(expected);
        }
    }
}
