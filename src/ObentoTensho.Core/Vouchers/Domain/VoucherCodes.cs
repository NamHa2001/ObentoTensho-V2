using System.Security.Cryptography;

namespace ObentoTensho.Core.Vouchers.Domain;

// BR-VCH-05: mã không phân biệt hoa thường (lưu chữ hoa); BR-VCH-18: mã sinh tự động 10 ký tự Crockford Base32 (≈ 50 bit)
public static class VoucherCodes
{
    public const int RandomLength = 10;
    public const int MaxLength = 50;
    public const int MaxPrefixLength = 10;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string New(string? prefix = null)
    {
        Span<char> chars = stackalloc char[RandomLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        var random = new string(chars);
        return string.IsNullOrWhiteSpace(prefix) ? random : $"{Normalize(prefix)}-{random}";
    }

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    // Chữ hoa, số và gạch nối; 4..50 ký tự
    public static bool IsValid(string code) =>
        code.Length is >= 4 and <= MaxLength && code.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '-');
}
