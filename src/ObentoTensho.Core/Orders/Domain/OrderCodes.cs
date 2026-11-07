using System.Security.Cryptography;

namespace ObentoTensho.Core.Orders.Domain;

// BR-ORD-11, SEC-01: mã công khai ngẫu nhiên OBT-TK-XXXX-XXXX (Crockford Base32, 40 bit) — không đoán được, không tuần tự
public static class OrderCodes
{
    // Bỏ I, L, O, U để không nhầm khi đọc qua điện thoại
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const string Prefix = "OBT-TK-";

    public static string New()
    {
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return $"{Prefix}{chars[..4]}-{chars[4..]}";
    }

    // Khách gõ chữ thường, thiếu gạch, thiếu tiền tố, nhầm O/0, I/L/1 vẫn tìm đúng
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var raw = new string(code.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (raw.StartsWith("OBTTK", StringComparison.Ordinal))
        {
            raw = raw[5..];
        }

        raw = raw.Replace('O', '0').Replace('I', '1').Replace('L', '1');
        return raw.Length == 8 && raw.All(Alphabet.Contains) ? $"{Prefix}{raw[..4]}-{raw[4..]}" : string.Empty;
    }
}
