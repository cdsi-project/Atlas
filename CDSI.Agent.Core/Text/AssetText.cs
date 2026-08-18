using CDSI.Agent.Core.Scanning;

namespace CDSI.Agent.Core.Text;

public static class TextPipeline
{
    public const int CurrentVersion = 1;
}

public enum TextExtractionStatus
{
    Extracted,
    Unsupported,
    Error
}

public sealed record AssetTextContent(
    string? Title,
    string PlainText,
    string[] Headings,
    string EncodingName,
    bool IsTruncated);

public sealed record AssetText(
    Guid AssetId,
    string ExtractorName,
    int PipelineVersion,
    TextExtractionStatus Status,
    long SourceSize,
    DateTimeOffset SourceModifiedAt,
    AssetTextContent? Content,
    DateTimeOffset ExtractedAt,
    string? ErrorMessage);

public sealed record TextCandidate(
    Guid AssetId,
    DiscoveredFile File);

public sealed record TextWorkSummary(int Files);

public sealed record TextExtractionResult(
    TextExtractionStatus Status,
    AssetTextContent? Content = null);

public sealed record TextProgress(
    int TotalFiles,
    int CompletedFiles,
    int ExtractedFiles,
    int UnsupportedFiles,
    int Errors,
    string? CurrentPath,
    string? Message = null);

public sealed record TextSummary(
    int TotalFiles,
    int ExtractedFiles,
    int UnsupportedFiles,
    int Errors,
    bool Cancelled);

public sealed class FileChangedDuringTextExtractionException : IOException
{
    public FileChangedDuringTextExtractionException(DiscoveredFile file)
        : base($"File changed during text extraction: {file.FullPath}")
    {
    }
}
