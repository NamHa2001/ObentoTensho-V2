using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// SEC-58: nhân viên bấm "Không phải tôi" trong thư báo đăng nhập lạ ⇒ thu hồi mọi phiên, khóa đăng nhập bằng mật khẩu hiện tại
// (kẻ xấu đã biết mật khẩu) tới khi đặt lại qua email, gửi liên kết đặt lại và báo SuperAdmin. Liên kết gắn security stamp
// nên chỉ dùng được một lần và mất hiệu lực khi mật khẩu đã đổi
public sealed class StaffNotMeService(
    IStaffRepository staff,
    IStaffChallengeTokens challenges,
    IRefreshTokenRepository refreshTokens,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emails,
    IAdminEmailOutbox adminEmails,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> ReportAsync(string? token, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var challenge = string.IsNullOrEmpty(token) ? null : challenges.Unprotect(token);
        if (challenge is null || challenge.Kind != StaffChallengeKind.NotMe || challenge.ExpiresAtUtc <= now)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        // Không loại tài khoản đang bị khóa: kẻ xấu có thể vừa làm khóa tài khoản sau khi đã vào được
        var member = await staff.FindForUpdateAsync(challenge.StaffId, cancellationToken);
        if (member is null || !member.IsActive || !string.Equals(member.SecurityStamp, challenge.SecurityStamp, StringComparison.Ordinal))
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        var reset = secureTokens.Create();
        member.SecurityStamp = secureTokens.NewSecurityStamp();
        member.LockoutEndUtc = now + AuthPolicy.NotMeLockout;
        member.ResetPasswordTokenHash = reset.Hash;
        member.ResetPasswordExpiryUtc = now + AuthPolicy.PasswordResetLifetime;
        emails.EnqueuePasswordReset(new AccountRecipient(member.Id, member.Email, member.FullName, AuthPolicy.DefaultLanguage, IsStaff: true), reset.Value);
        audit.Record(new AuditEntry("auth.staff.not_me_reported", "staff", member.Id.ToString("D", CultureInfo.InvariantCulture)));
        var message = $"Nhân viên {member.Email} báo một lần đăng nhập không phải của mình. Mọi phiên đã bị thu hồi, tài khoản bị khóa tới khi đặt lại mật khẩu.";
        foreach (var email in (await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken))
            .Where(e => !string.Equals(e, member.Email, StringComparison.OrdinalIgnoreCase)))
        {
            adminEmails.EnqueueAlert(email, message, $"not-me:{member.Id:N}:{now.Ticks}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForStaffAsync(member.Id, RefreshTokenRevokedReason.AccountLocked, now, cancellationToken);
        return Result.Success();
    }
}
