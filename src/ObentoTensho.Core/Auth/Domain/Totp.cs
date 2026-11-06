using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace ObentoTensho.Core.Auth.Domain;

// RFC 6238 (TOTP) trên RFC 4226 (HOTP): HMAC-SHA1, bước 30 giây, 6 chữ số — tương thích Google Authenticator, Authy…
public static class Totp
{
    public const int SecretBytes = 20;
    public const int Digits = 6;
    public static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    // Cho phép lệch một bước mỗi phía vì đồng hồ điện thoại có thể sai vài giây
    private const int AllowedDrift = 1;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long GetStep(DateTime utc) => (long)Math.Floor((utc - DateTime.UnixEpoch).TotalSeconds / Step.TotalSeconds);

    public static string ComputeCode(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];

        // RFC 6238 mặc định HMAC-SHA1 và hầu hết ứng dụng xác thực chỉ hỗ trợ nó;
        // lỗ hổng va chạm của SHA-1 không ảnh hưởng đến HMAC
#pragma warning disable CA5350
        HMACSHA1.HashData(secret, counter, hash);
#pragma warning restore CA5350

        var offset = hash[^1] & 0x0F;
        var binary = BinaryPrimitives.ReadInt32BigEndian(hash.Slice(offset, 4)) & 0x7FFFFFFF;
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    // Trả bước khớp (để chống dùng lại) hoặc null; so sánh hằng thời gian
    public static long? FindMatchingStep(byte[] secret, string? code, DateTime nowUtc)
    {
        if (code is null || code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        var current = GetStep(nowUtc);
        long? match = null;
        for (var step = current - AllowedDrift; step <= current + AllowedDrift; step++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(ComputeCode(secret, step)),
                System.Text.Encoding.ASCII.GetBytes(code)))
            {
                match = step;
            }
        }

        return match;
    }

    public static string BuildUri(string issuer, string accountName, byte[] secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}" +
        $"?secret={Base32.Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={(int)Step.TotalSeconds}";
}
