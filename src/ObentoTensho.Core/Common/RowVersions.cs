using System.Buffers.Text;

namespace ObentoTensho.Core.Common;

// RowVersion ⇄ chuỗi ETag gửi cho client (Base64Url, không dấu ngoặc kép)
public static class RowVersions
{
    public static string Encode(byte[] rowVersion) => Base64Url.EncodeToString(rowVersion);

    public static byte[]? Decode(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim().Trim('"');
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return Base64Url.DecodeFromChars(trimmed);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
