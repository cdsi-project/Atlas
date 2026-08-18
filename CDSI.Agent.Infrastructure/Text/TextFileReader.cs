using System.Text;
using CDSI.Agent.Core.Text;

namespace CDSI.Agent.Infrastructure.Text;

internal static class TextFileReader
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    static TextFileReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static async Task<DecodedText> ReadAsync(
        string path,
        TextExtractionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytesToRead = (int)Math.Min(stream.Length, options.MaximumInputBytes);
        var bytes = GC.AllocateUninitializedArray<byte>(bytesToRead);
        var offset = 0;

        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(
                bytes.AsMemory(offset, bytes.Length - offset),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        if (offset != bytes.Length)
        {
            Array.Resize(ref bytes, offset);
        }

        var inputTruncated = stream.Length > offset;
        var decoded = Decode(bytes, inputTruncated);
        var normalized = NormalizeLineEndings(decoded.Text);
        var outputTruncated = normalized.Length > options.MaximumOutputCharacters;
        if (outputTruncated)
        {
            normalized = normalized[..options.MaximumOutputCharacters];
        }

        return new DecodedText(
            normalized.Trim(),
            decoded.EncodingName,
            inputTruncated || outputTruncated);
    }

    private static DecodedText Decode(byte[] bytes, bool inputTruncated)
    {
        var span = bytes.AsSpan();

        if (span.StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
        {
            return DecodeKnownEncoding(
                span[4..],
                new UTF32Encoding(true, false, true),
                new UTF32Encoding(true, false, false),
                "UTF-32 BE",
                inputTruncated);
        }

        if (span.StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
        {
            return DecodeKnownEncoding(
                span[4..],
                new UTF32Encoding(false, false, true),
                new UTF32Encoding(false, false, false),
                "UTF-32 LE",
                inputTruncated);
        }

        if (span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return DecodeKnownEncoding(
                span[3..],
                StrictUtf8,
                new UTF8Encoding(false, false),
                "UTF-8",
                inputTruncated);
        }

        if (span.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return DecodeKnownEncoding(
                span[2..],
                new UnicodeEncoding(true, false, true),
                new UnicodeEncoding(true, false, false),
                "UTF-16 BE",
                inputTruncated);
        }

        if (span.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return DecodeKnownEncoding(
                span[2..],
                new UnicodeEncoding(false, false, true),
                new UnicodeEncoding(false, false, false),
                "UTF-16 LE",
                inputTruncated);
        }

        if (TryDecode(StrictUtf8, span, inputTruncated, out var utf8))
        {
            return new DecodedText(utf8, "UTF-8", inputTruncated);
        }

        var strictGb18030 = Encoding.GetEncoding(
            54936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        if (TryDecode(strictGb18030, span, inputTruncated, out var gb18030))
        {
            return new DecodedText(gb18030, "GB18030", inputTruncated);
        }

        var windows1252 = Encoding.GetEncoding(1252);
        return new DecodedText(
            windows1252.GetString(span),
            "Windows-1252",
            inputTruncated);
    }

    private static DecodedText DecodeKnownEncoding(
        ReadOnlySpan<byte> bytes,
        Encoding strictEncoding,
        Encoding lenientEncoding,
        string name,
        bool inputTruncated)
    {
        try
        {
            return new DecodedText(
                strictEncoding.GetString(bytes),
                name,
                inputTruncated);
        }
        catch (DecoderFallbackException)
        {
            return new DecodedText(lenientEncoding.GetString(bytes), name, true);
        }
    }

    private static bool TryDecode(
        Encoding encoding,
        ReadOnlySpan<byte> bytes,
        bool inputTruncated,
        out string text)
    {
        try
        {
            text = encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException) when (inputTruncated)
        {
            var maximumTrim = Math.Min(4, bytes.Length);
            for (var trim = 1; trim <= maximumTrim; trim++)
            {
                try
                {
                    text = encoding.GetString(bytes[..^trim]);
                    return true;
                }
                catch (DecoderFallbackException)
                {
                }
            }
        }
        catch (DecoderFallbackException)
        {
        }

        text = string.Empty;
        return false;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }
}

internal sealed record DecodedText(
    string Text,
    string EncodingName,
    bool IsTruncated);
