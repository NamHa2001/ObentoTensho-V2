namespace ObentoTensho.Core.Auth.Domain;

// Thông số nghiệp vụ xác thực (docs/srs/10 §6, docs/srs/03)
public static class AuthPolicy
{
    public const string CustomerRole = "customer";

    // BR-AUTH-01
    public const int PasswordMinLength = 10;

    // Chặn chuỗi khổng lồ làm tốn CPU khi băm
    public const int PasswordMaxLength = 128;

    // BR-AUTH-03
    public const int MaxFailedLogins = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // BR-AUTH-02
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // BR-AUTH-08
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(24);

    // SEC-52
    public const int MaxSessionsPerAccount = 5;

    public static readonly IReadOnlyList<string> SupportedLanguages = ["vi", "en", "ja"];

    public const string DefaultLanguage = "vi";
}
