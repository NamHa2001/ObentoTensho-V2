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
    AuthSecurityEvents securityEvents,
    IAccountEmailOutbox emailOutbox,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<StaffChangePasswordRequest> changeValidator,
    IValidator<AcceptInviteRequest> inviteValidator,
    IValidator<ChangeOwnPasswordRequest> ownValidator)
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
            await securityEvents.StaffLoginFailedAsync(member.Id, member.Email, "wrong_current_password", cancellationToken);
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

        await SetPasswordAsync(tracked, request.NewPassword, securityEvents.RecordStaffPasswordChanged, cancellationToken);
        return Result.Success(await flow.ContinueAsync(tracked, challenge.SecondFactorPassed, client, cancellationToken));
    }

    // FR-AUTH-06: đang đăng nhập tự đổi; mọi phiên bị thu hồi nên phải đăng nhập lại
    public async Task<Result> ChangeOwnPasswordAsync(Guid staffId, ChangeOwnPasswordRequest request, CancellationToken cancellationToken)
    {
        var validation = await ownValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        if (await staff.FindForUpdateAsync(staffId, cancellationToken) is not { PasswordHash: { } currentHash } tracked)
        {
            return Result.Failure(ErrorCodes.Unauthenticated);
        }

        if (passwordHasher.Verify(currentHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            await staff.RegisterFailedLoginAsync(tracked.Id, clock.UtcNow + AuthPolicy.LockoutDuration, cancellationToken);
            await securityEvents.StaffLoginFailedAsync(tracked.Id, tracked.Email, "wrong_current_password", cancellationToken);
            return Result.Failure(ErrorCodes.InvalidCredentials);
        }

        if (PasswordRules.ContainsEmail(request.NewPassword, tracked.Email))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, ContainsEmailError(NewPasswordField));
        }

        emailOutbox.EnqueuePasswordChanged(new AccountRecipient(tracked.Id, tracked.Email, tracked.FullName, AuthPolicy.DefaultLanguage, IsStaff: true), clock.UtcNow);
        await SetPasswordAsync(tracked, request.NewPassword, securityEvents.RecordStaffPasswordChanged, cancellationToken);
        return Result.Success();
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

        await SetPasswordAsync(tracked, request.Password, securityEvents.RecordStaffInviteAccepted, cancellationToken);
        return Result.Success();
    }

    // Đổi mật khẩu ⇒ đổi security stamp và thu hồi mọi phiên (SEC-06); liên kết mời chỉ dùng một lần
    private async Task SetPasswordAsync(Staff tracked, string password, Action<Staff> recordEvent, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        tracked.PasswordHash = passwordHasher.Hash(password);
        tracked.PasswordChangedAtUtc = now;
        tracked.MustChangePassword = false;
        tracked.InviteTokenHash = null;
        tracked.InviteExpiryUtc = null;
        tracked.SecurityStamp = secureTokens.NewSecurityStamp();
        recordEvent(tracked);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForStaffAsync(tracked.Id, RefreshTokenRevokedReason.PasswordChanged, now, cancellationToken);
    }

    private static Dictionary<string, string[]> ContainsEmailError(string field) =>
        new(StringComparer.Ordinal) { [field] = [FieldErrorCodes.PasswordContainsEmail] };
}
