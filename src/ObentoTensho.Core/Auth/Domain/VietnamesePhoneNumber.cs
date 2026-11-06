using System.Diagnostics.CodeAnalysis;

namespace ObentoTensho.Core.Auth.Domain;

// BR-AUTH-13: lưu và so sánh SĐT ở dạng quốc tế +84…
public static class VietnamesePhoneNumber
{
    private const string CountryCode = "84";

    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var digits = Strip(input.Trim());
        if (digits is null)
        {
            return false;
        }

        var national = ToNational(digits);
        if (national is null || !IsValidNational(national))
        {
            return false;
        }

        normalized = $"+{CountryCode}{national}";
        return true;
    }

    // Bỏ khoảng trắng, dấu chấm, gạch nối, ngoặc; dấu "+" chỉ được đứng đầu
    private static string? Strip(string input)
    {
        var hasPlus = input.StartsWith('+');
        var buffer = new System.Text.StringBuilder(input.Length);
        foreach (var c in hasPlus ? input[1..] : input)
        {
            if (char.IsAsciiDigit(c))
            {
                buffer.Append(c);
            }
            else if (c is not (' ' or '.' or '-' or '(' or ')'))
            {
                return null;
            }
        }

        return hasPlus ? "+" + buffer : buffer.ToString();
    }

    private static string? ToNational(string digits)
    {
        if (digits.StartsWith("+" + CountryCode, StringComparison.Ordinal))
        {
            return digits[3..];
        }

        if (digits.StartsWith('+'))
        {
            return null;
        }

        if (digits.StartsWith('0'))
        {
            return digits[1..];
        }

        // "849…" không có dấu +: chỉ coi là mã quốc gia khi đủ độ dài số quốc tế
        return digits.StartsWith(CountryCode, StringComparison.Ordinal) && digits.Length >= 11 ? digits[2..] : null;
    }

    // Di động: 9 chữ số bắt đầu 3/5/7/8/9. Cố định: 10 chữ số bắt đầu 2.
    private static bool IsValidNational(string national) => national.Length switch
    {
        9 => national[0] is '3' or '5' or '7' or '8' or '9',
        10 => national[0] == '2',
        _ => false,
    };
}
