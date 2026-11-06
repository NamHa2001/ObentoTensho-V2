using System.Security.Cryptography;

namespace ObentoTensho.Core.Auth.Domain;

// Mã khôi phục 2FA: 10 mã, mỗi mã 10 ký tự Base32 (50 bit), hiển thị dạng XXXXX-XXXXX
public static class RecoveryCodes
{
    public const int Count = 10;
    private const int CodeLength = 10;

    public static IReadOnlyList<string> Generate() =>
        Enumerable.Range(0, Count).Select(_ => Format(Base32.Encode(RandomNumberGenerator.GetBytes(7))[..CodeLength])).ToArray();

    // Người dùng có thể gõ chữ thường, thiếu hoặc thừa gạch nối/khoảng trắng
    public static string Normalize(string code) =>
        new(code.Where(c => c is not ('-' or ' ')).Select(char.ToUpperInvariant).ToArray());

    private static string Format(string raw) => $"{raw[..5]}-{raw[5..]}";
}
