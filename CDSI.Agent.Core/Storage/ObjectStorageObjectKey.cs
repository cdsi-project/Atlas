using System.Text;

namespace CDSI.Agent.Core.Storage;

public static class ObjectStorageObjectKey
{
    // Aliyun OSS limits the complete UTF-8 object key to 1,023 bytes.
    private const int MaximumUtf8ByteCount = 1_023;

    public static bool TryCreateForAsset(
        Guid assetId,
        string? filename,
        out string objectKey,
        out string? errorMessage)
    {
        objectKey = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(filename))
        {
            errorMessage = "OSS 文件名不能为空。";
            return false;
        }

        if (filename is "." or "..")
        {
            errorMessage = "OSS 文件名不能是 . 或 ..。";
            return false;
        }

        if (filename.Any(character =>
                character is '/' or '\\' || char.IsControl(character)))
        {
            errorMessage = "OSS 文件名不能包含路径分隔符或控制字符。";
            return false;
        }

        var candidate = $"assets/{assetId:N}/{filename}";
        if (Encoding.UTF8.GetByteCount(candidate) > MaximumUtf8ByteCount)
        {
            errorMessage = "OSS 文件名过长。";
            return false;
        }

        objectKey = candidate;
        return true;
    }
}
