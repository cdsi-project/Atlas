using CDSI.Agent.Core.Scanning;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Infrastructure.Text;

internal static class TextExtractionUtilities
{
    public static AssetTextContent CreateContent(
        DiscoveredFile file,
        DecodedText decoded,
        string plainText,
        IEnumerable<string>? headings,
        TextExtractionOptions options)
    {
        var normalizedText = plainText.Trim();
        var outputTruncated = normalizedText.Length > options.MaximumOutputCharacters;
        if (outputTruncated)
        {
            normalizedText = normalizedText[..options.MaximumOutputCharacters].TrimEnd();
        }

        var normalizedHeadings = (headings ?? [])
            .Select(NormalizeSingleLine)
            .Where(value => value.Length > 0)
            .Select(value => value.Length <= options.MaximumHeadingCharacters
                ? value
                : value[..options.MaximumHeadingCharacters])
            .Take(options.MaximumHeadings)
            .ToArray();
        var title = normalizedHeadings.FirstOrDefault()
            ?? decoded.Text
                .Split('\n')
                .Select(NormalizeSingleLine)
                .FirstOrDefault(value => value.Length > 0)
            ?? Path.GetFileNameWithoutExtension(file.OriginalFilename);
        if (title.Length > options.MaximumHeadingCharacters)
        {
            title = title[..options.MaximumHeadingCharacters];
        }

        return new AssetTextContent(
            title,
            normalizedText,
            normalizedHeadings,
            decoded.EncodingName,
            decoded.IsTruncated || outputTruncated);
    }

    private static string NormalizeSingleLine(string value)
    {
        return string.Join(
            " ",
            value.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
