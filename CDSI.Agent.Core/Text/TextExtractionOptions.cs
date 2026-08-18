namespace CDSI.Agent.Core.Text;

public sealed record TextExtractionOptions
{
    public const int DefaultMaximumInputBytes = 4 * 1024 * 1024;
    public const int DefaultMaximumOutputCharacters = 200_000;
    public const int DefaultMaximumHeadings = 64;
    public const int DefaultMaximumHeadingCharacters = 256;

    public int MaximumInputBytes { get; init; } = DefaultMaximumInputBytes;

    public int MaximumOutputCharacters { get; init; } = DefaultMaximumOutputCharacters;

    public int MaximumHeadings { get; init; } = DefaultMaximumHeadings;

    public int MaximumHeadingCharacters { get; init; } =
        DefaultMaximumHeadingCharacters;

    public void Validate()
    {
        if (MaximumInputBytes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumInputBytes));
        }

        if (MaximumOutputCharacters < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumOutputCharacters));
        }

        if (MaximumHeadings < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumHeadings));
        }

        if (MaximumHeadingCharacters < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumHeadingCharacters));
        }
    }
}
