using CDSI.Agent.Core.Abstractions;
using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Application.Text;

public sealed class TextExtractionApplicationService
{
    private const int PageSize = 128;
    private const int MaximumErrorLength = 1_024;

    private readonly IReadOnlyList<IAssetTextExtractor> _extractors;
    private readonly IAssetRepository _repository;

    public TextExtractionApplicationService(
        IEnumerable<IAssetTextExtractor> extractors,
        IAssetRepository repository)
    {
        ArgumentNullException.ThrowIfNull(extractors);
        _extractors = extractors.ToArray();
        if (_extractors.Count == 0)
        {
            throw new ArgumentException(
                "At least one text extractor is required.",
                nameof(extractors));
        }

        _repository = repository;
    }

    public async Task<TextSummary> ProcessPendingAsync(
        IProgress<TextProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var work = await _repository.GetTextWorkSummaryAsync(
            TextPipeline.CurrentVersion,
            cancellationToken);
        var completedFiles = 0;
        var extractedFiles = 0;
        var unsupportedFiles = 0;
        var errors = 0;
        Guid? afterAssetId = null;

        void Report(string? currentPath, string? message = null)
        {
            progress?.Report(new TextProgress(
                work.Files,
                completedFiles,
                extractedFiles,
                unsupportedFiles,
                errors,
                currentPath,
                message));
        }

        Report(null);

        try
        {
            while (true)
            {
                var candidates = await _repository.ListTextCandidatesAsync(
                    TextPipeline.CurrentVersion,
                    afterAssetId,
                    PageSize,
                    cancellationToken);
                if (candidates.Count == 0)
                {
                    break;
                }

                foreach (var candidate in candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Report(candidate.File.FullPath);
                    var extractorName = "registry";

                    try
                    {
                        var extractor = SelectExtractor(candidate.File);
                        extractorName = extractor.Name;
                        EnsureFileUnchanged(candidate.File);
                        var result = await extractor.ExtractAsync(
                            candidate.File,
                            cancellationToken);
                        EnsureFileUnchanged(candidate.File);

                        var text = new AssetText(
                            candidate.AssetId,
                            extractor.Name,
                            TextPipeline.CurrentVersion,
                            result.Status,
                            candidate.File.Size,
                            candidate.File.ModifiedAt,
                            result.Content,
                            DateTimeOffset.UtcNow,
                            null);
                        var saved = await _repository.SaveTextAsync(
                            text,
                            cancellationToken);

                        if (!saved)
                        {
                            errors++;
                            Report(
                                candidate.File.FullPath,
                                "File metadata changed before extracted text could be saved.");
                        }
                        else if (result.Status == TextExtractionStatus.Extracted)
                        {
                            extractedFiles++;
                        }
                        else if (result.Status == TextExtractionStatus.Unsupported)
                        {
                            unsupportedFiles++;
                        }
                        else
                        {
                            errors++;
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        errors++;
                        var message = NormalizeError(exception.Message);
                        var failedText = new AssetText(
                            candidate.AssetId,
                            extractorName,
                            TextPipeline.CurrentVersion,
                            TextExtractionStatus.Error,
                            candidate.File.Size,
                            candidate.File.ModifiedAt,
                            null,
                            DateTimeOffset.UtcNow,
                            message);

                        try
                        {
                            await _repository.SaveTextAsync(
                                failedText,
                                cancellationToken);
                        }
                        catch (Exception saveException)
                            when (saveException is not OperationCanceledException)
                        {
                            message = NormalizeError(
                                $"{message} Text state could not be saved: {saveException.Message}");
                        }

                        Report(candidate.File.FullPath, message);
                    }

                    completedFiles++;
                    afterAssetId = candidate.AssetId;
                    Report(candidate.File.FullPath);
                }
            }

            Report(null);
            return new TextSummary(
                work.Files,
                extractedFiles,
                unsupportedFiles,
                errors,
                Cancelled: false);
        }
        catch (OperationCanceledException)
        {
            Report(null, "Text extraction cancelled.");
            return new TextSummary(
                work.Files,
                extractedFiles,
                unsupportedFiles,
                errors,
                Cancelled: true);
        }
    }

    private IAssetTextExtractor SelectExtractor(DiscoveredFile file)
    {
        return _extractors.FirstOrDefault(extractor => extractor.Supports(file))
            ?? throw new InvalidOperationException(
                $"No text extractor supports: {file.FullPath}");
    }

    private static void EnsureFileUnchanged(DiscoveredFile expected)
    {
        var actual = new FileInfo(expected.FullPath);
        actual.Refresh();
        if (!actual.Exists)
        {
            throw new FileNotFoundException(
                "File disappeared before text extraction.",
                expected.FullPath);
        }

        if (actual.Length != expected.Size ||
            actual.LastWriteTimeUtc != expected.ModifiedAt.UtcDateTime)
        {
            throw new FileChangedDuringTextExtractionException(expected);
        }
    }

    private static string NormalizeError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Text extraction failed.";
        }

        var normalized = message.Trim();
        return normalized.Length <= MaximumErrorLength
            ? normalized
            : normalized[..MaximumErrorLength];
    }
}
