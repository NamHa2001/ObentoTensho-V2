namespace ObentoTensho.Infrastructure.Security;

public sealed class PasswordHashingOptions
{
    public const string SectionName = "Security:PasswordHashing";

    // Mức tối thiểu OWASP cho PBKDF2-HMAC-SHA512
    public const int MinimumIterations = 210_000;

    // Đo trên máy dev 06/11/2026: khoảng 180 ms mỗi lần băm
    public int Iterations { get; set; } = 300_000;
}
