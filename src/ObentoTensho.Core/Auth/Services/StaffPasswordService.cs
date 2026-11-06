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

// SEC-44: nhân viên tự đặt mật khẩu qua liên kết mời; đổi mật khẩu bắt buộc khi MustChangePassword
public sealed class StaffPasswordService(
    IStaffRepository staff,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    StaffSignInFlow flow,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<StaffChangePasswordRequest> changeValidator,
    IValidator<AcceptInviteRequest> inviteValidator)
{
    private const string NewPasswordField = "newPassword";
    private const string PasswordField = "password";

    public async Task<Result<StaffSignInResult>> ChangePasswordAsync(StaffChangePasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var validation = await changeValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<StaffSignInResult>();
        }

        if (await flow.ResolveAsync(request.ChallengeToken, StaffChallengeKind.PasswordChange, cancellationToken) is not var (member, challenge)
            || member.PasswordHash is not { } currentHash)
        {
            return Result.Failure<StaffSignInResult>(ErrorCodes.Unauthenticated);
        }

        if (passwordHasher.Verify(currentHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            await staff.RegisterFailedLoginAsync(member.Id, clock.UtcNow + AuthPolicy.LockoutDuration, cancellationToken);
            return Result.Failure<StaffSignInResult>(ErrorCodes.InvalidCredentials);
        }

        if (PasswordRules.ContainsEmail(request.NewPassword, member.Email))
        {
            return Result.Failure<StaffSignInResult>(ErrorCodes.ValidationFailed, ContainsEmailError(NewPasswordField));
        }

        if (await staff.FindForUpdateAsync(member.Id, cancellationToken) is not { } tracked)
        {
            return Result.Failure<StaffSignInResult>(ErrorCodes.Unauthenticated);
        }

        await SetPasswordAsync(tracked, request.NewPassword, cancellationToken);
        return Result.Success(await flow.ContinueAsync(tracked, challenge.SecondFactorPassed, client, cancellationToken));
    }

    public async Task<Result> AcceptInviteAsync(AcceptInviteRequest request, CancellationToken cancellationToken)
    {
        var validation = await inviteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var tracked = await staff.FindByInviteTokenHashForUpdateAsync(secureTokens.Hash(request.Token), cancellationToken);
        if (tracked is null || !tracked.IsActive || tracked.InviteExpiryUtc is null || tracked.InviteExpiryUtc <= clock.UtcNow)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        if (PasswordRules.ContainsEmail(request.Password, tracked.Email))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ContainsEmailError(PasswordField));
        }

        await SetPasswordAsync(tracked, request.Password, cancellationToken);
        return Result.Success();
    }

    // Đổi mật khẩu ⇒ đổi security stamp và thu hồi mọi phiên (SEC-06); liên kết mời chỉ dùng một lần
    private async Task SetPasswordAsync(Staff tracked, string password, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        tracked.PasswordHash = passwordHasher.Hash(password);
        tracked.PasswordChangedAtUtc = now;
        tracked.MustChangePassword = false;
        tracked.InviteTokenHash = null;
        tracked.InviteExpiryUtc = null;
        tracked.SecurityStamp = secureTokens.NewSecurityStamp();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForStaffAsync(tracked.Id, RefreshTokenRevokedReason.PasswordChanged, now, cancellationToken);
    }

    private static Dictionary<string, string[]> ContainsEmailError(string field) =>
        new(StringComparer.Ordinal) { [field] = [FieldErrorCodes.PasswordContainsEmail] };
}
