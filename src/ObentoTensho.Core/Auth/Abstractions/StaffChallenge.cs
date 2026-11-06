using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Core.Auth.Abstractions;

// Gắn security stamp: đổi mật khẩu/khóa tài khoản làm mọi mã tạm đang có mất hiệu lực.
// SecondFactorPassed giữ kết quả 2FA qua bước đổi mật khẩu bắt buộc.
public sealed record StaffChallenge(
    Guid StaffId,
    StaffChallengeKind Kind,
    string SecurityStamp,
    DateTime ExpiresAtUtc,
    bool SecondFactorPassed);
