using CDSI.Agent.Core.Assets;

namespace CDSI.Agent.Core.Scanning;

public sealed class ScanFileFilter
{
    public const int MaxExtensionWhitelistCount = 256;

    private readonly IReadOnlySet<string> _extensionSet;

    public ScanFileFilter(
        AssetFileTypeFilter fileTypeFilter = AssetFileTypeFilter.All,
        IEnumerable<string>? extensionWhitelist = null)
    {
        if (!Enum.IsDefined(fileTypeFilter))
        {
            throw new ArgumentOutOfRangeException(nameof(fileTypeFilter));
        }

        FileTypeFilter = fileTypeFilter;
        ExtensionWhitelist = NormalizeExtensions(extensionWhitelist);
        _extensionSet = new HashSet<string>(
            ExtensionWhitelist,
            StringComparer.OrdinalIgnoreCase);
    }

    public AssetFileTypeFilter FileTypeFilter { get; }

    public IReadOnlyList<string> ExtensionWhitelist { get; }

    public bool UsesExtensionWhitelist => ExtensionWhitelist.Count > 0;

    public bool Matches(string? extension, string? mimeType)
    {
        if (!UsesExtensionWhitelist)
        {
            return AssetFileTypeClassifier.Matches(
                extension,
                mimeType,
                FileTypeFilter);
        }

        return TryNormalizeExtension(extension, out var normalized) &&
            _extensionSet.Contains(normalized);
    }

    public bool HasSameConfiguration(ScanFileFilter other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return FileTypeFilter == other.FileTypeFilter &&
            ExtensionWhitelist.SequenceEqual(
                other.ExtensionWhitelist,
                StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> NormalizeExtensions(
        IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return Array.Empty<string>();
        }

        var normalized = extensions
            .Select(NormalizeExtension)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length > MaxExtensionWhitelistCount)
        {
            throw new ArgumentException(
                $"扩展名白名单最多允许 {MaxExtensionWhitelistCount} 项。",
                nameof(extensions));
        }

        return Array.AsReadOnly(normalized);
    }

    public static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        if (!TryNormalizeExtension(extension, out var normalized))
        {
            throw new ArgumentException(
                "文件扩展名格式无效。示例: .mp4",
                nameof(extension));
        }

        return normalized;
    }

    private static bool TryNormalizeExtension(
        string? extension,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var candidate = extension.Trim();
        if (candidate.StartsWith("*.", StringComparison.Ordinal))
        {
            candidate = candidate[1..];
        }
        else if (!candidate.StartsWith(".", StringComparison.Ordinal))
        {
            candidate = $".{candidate}";
        }

        if (candidate.Length is < 2 or > 32 ||
            candidate.AsSpan(1).Contains('.') ||
            candidate.Any(character =>
                char.IsWhiteSpace(character) ||
                "\\/:*?\"<>|".Contains(character)))
        {
            return false;
        }

        normalized = candidate.ToLowerInvariant();
        return true;
    }
}
