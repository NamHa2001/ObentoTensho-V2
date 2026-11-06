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

// FR-AUTH-05 (quên/đặt lại mật khẩu), FR-AUTH-07 (xác thực email, gửi lại) cho khách
public sealed class CustomerAccountService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<ResetPasswordRequest> resetValidator)
{
    public async Task<Result> VerifyEmailAsync(string? token, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(token)
            ? null
            : await users.FindByEmailVerifyTokenHashForUpdateAsync(secureTokens.Hash(token), cancellationToken);
        if (user is null || user.IsDeleted || user.EmailVerifyExpiryUtc is null || user.EmailVerifyExpiryUtc <= clock.UtcNow)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        user.IsEmailVerified = true;
        user.EmailVerifyTokenHash = null;
        user.EmailVerifyExpiryUtc = null;
        user.UpdatedAtUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Luôn trả thành công để không lộ email nào có tài khoản; giới hạn số thư theo địa chỉ nhận chống spam (SEC-45)
    public async Task ResendVerificationAsync(string? email, CancellationToken cancellationToken)
    {
        if (await FindActiveAsync(email, cancellationToken) is not { IsEmailVerified: false } user)
        {
            return;
        }

        var token = secureTokens.Create();
        user.EmailVerifyTokenHash = token.Hash;
        user.EmailVerifyExpiryUtc = clock.UtcNow + AuthPolicy.EmailVerificationLifetime;
        emailOutbox.EnqueueEmailVerification(user, token.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ForgotPasswordAsync(string? email, CancellationToken cancellationToken)
    {
        if (await FindActiveAsync(email, cancellationToken) is not { } user)
        {
            return;
        }

        var token = secureTokens.Create();
        user.ResetPasswordTokenHash = token.Hash;
        user.ResetPasswordExpiryUtc = clock.UtcNow + AuthPolicy.PasswordResetLifetime;
        emailOutbox.EnqueuePasswordReset(Recipient(user), token.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // SEC-06: đặt lại thành công ⇒ đổi security stamp, thu hồi mọi phiên, gỡ khóa đăng nhập, báo chủ tài khoản
    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var now = clock.UtcNow;
        var user = await users.FindByResetTokenHashForUpdateAsync(secureTokens.Hash(request.Token), cancellationToken);
        if (user is null || user.IsDeleted || !user.IsActive || user.ResetPasswordExpiryUtc is null || user.ResetPasswordExpiryUtc <= now)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        if (PasswordRules.ContainsEmail(request.NewPassword, user.Email))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = [FieldErrorCodes.PasswordContainsEmail],
            });
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.PasswordChangedAtUtc = now;
        user.ResetPasswordTokenHash = null;
        user.ResetPasswordExpiryUtc = null;
        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.SecurityStamp = secureTokens.NewSecurityStamp();
        user.UpdatedAtUtc = now;
        emailOutbox.EnqueuePasswordChanged(Recipient(user), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokedReason.PasswordChanged, now, cancellationToken);
        return Result.Success();
    }

    private async Task<User?> FindActiveAsync(string? email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailValidator.MaxLength)
        {
            return null;
        }

        var user = await users.FindByEmailForUpdateAsync(EmailAddress.Normalize(email), cancellationToken);
        return user is { IsDeleted: false, IsActive: true } ? user : null;
    }

    private static AccountRecipient Recipient(User user) =>
        new(user.Id, user.Email, user.FullName, user.PreferredLang, IsStaff: false);
}
