using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-05 cho nhân viên. Đặt lại mật khẩu không bỏ qua 2FA: lần đăng nhập sau vẫn phải nhập mã.
public sealed class StaffPasswordResetService(
    IStaffRepository staff,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    AuthSecurityEvents securityEvents,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<ResetPasswordRequest> resetValidator)
{
    // Nhân viên chưa nhận lời mời (chưa có mật khẩu) dùng liên kết mời, không dùng quên mật khẩu
    public async Task ForgotPasswordAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailValidator.MaxLength
            || await staff.FindByEmailForUpdateAsync(EmailAddress.Normalize(email), cancellationToken) is not { IsActive: true, PasswordHash: not null } member)
        {
            return;
        }

        var token = secureTokens.Create();
        member.ResetPasswordTokenHash = token.Hash;
        member.ResetPasswordExpiryUtc = clock.UtcNow + AuthPolicy.PasswordResetLifetime;
        emailOutbox.EnqueuePasswordReset(Recipient(member), token.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var now = clock.UtcNow;
        var member = await staff.FindByResetTokenHashForUpdateAsync(secureTokens.Hash(request.Token), cancellationToken);
        if (member is null || !member.IsActive || member.ResetPasswordExpiryUtc is null || member.ResetPasswordExpiryUtc <= now)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        if (PasswordRules.ContainsEmail(request.NewPassword, member.Email))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = [FieldErrorCodes.PasswordContainsEmail],
            });
        }

        member.PasswordHash = passwordHasher.Hash(request.NewPassword);
        member.PasswordChangedAtUtc = now;
        member.MustChangePassword = false;
        member.ResetPasswordTokenHash = null;
        member.ResetPasswordExpiryUtc = null;
        member.FailedLoginCount = 0;
        member.LockoutEndUtc = null;
        member.SecurityStamp = secureTokens.NewSecurityStamp();
        securityEvents.RecordStaffPasswordChanged(member);
        emailOutbox.EnqueuePasswordChanged(Recipient(member), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForStaffAsync(member.Id, RefreshTokenRevokedReason.PasswordChanged, now, cancellationToken);
        return Result.Success();
    }

    private static AccountRecipient Recipient(Staff member) =>
        new(member.Id, member.Email, member.FullName, AuthPolicy.DefaultLanguage, IsStaff: true);
}
