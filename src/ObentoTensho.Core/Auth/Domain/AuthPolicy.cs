using ObentoTensho.Contracts.Enums;

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

    // BR-AUTH-07
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromMinutes(30);

    // BR-AUTH-08
    public static readonly TimeSpan EmailVerificationLifetime = TimeSpan.FromHours(24);

    // SEC-52
    public const int MaxSessionsPerAccount = 5;

    // SEC-74: phiên quản trị hết khi không hoạt động 30 phút, tối đa 12 giờ kể từ lúc đăng nhập
    public static readonly TimeSpan StaffIdleTimeout = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan StaffAbsoluteLifetime = TimeSpan.FromHours(12);

    // BR-AUTH-10: liên kết mời nhân viên
    public static readonly TimeSpan StaffInviteLifetime = TimeSpan.FromHours(72);

    // Mã tạm giữa bước mật khẩu và bước 2FA (docs/srs/10 §4.2)
    public static readonly TimeSpan StaffChallengeLifetime = TimeSpan.FromMinutes(5);

    // SEC-58: liên kết "Không phải tôi" dùng được 7 ngày, mất hiệu lực ngay khi mật khẩu đổi
    public static readonly TimeSpan NotMeLinkLifetime = TimeSpan.FromDays(7);

    // Khóa tới khi nhân viên đặt lại mật khẩu (đặt lại xóa khóa)
    public static readonly TimeSpan NotMeLockout = TimeSpan.FromDays(30);

    // BR-CUS-05
    public static readonly TimeSpan AccountDeletionGrace = TimeSpan.FromDays(7);

    public const string DeletedAccountName = "Khách đã xóa tài khoản";

    // BR-LOY-20: "60 ngày quanh ngày sinh" = 30 ngày mỗi phía
    public const int DateOfBirthLockDays = 30;

    // SEC-61
    public static readonly TimeSpan StepUpLifetime = TimeSpan.FromMinutes(5);

    public const string TotpIssuer = "Obento Tensho";

    // D-06: bắt buộc 2FA
    public static bool RequiresTwoFactor(StaffRole role) => role is StaffRole.SuperAdmin or StaffRole.Manager;

    public static readonly IReadOnlyList<string> SupportedLanguages = ["vi", "en", "ja"];

    public const string DefaultLanguage = "vi";
}
