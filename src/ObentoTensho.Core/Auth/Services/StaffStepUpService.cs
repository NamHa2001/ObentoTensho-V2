using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// SEC-61: phiên bị chiếm chưa đủ để làm thao tác rủi ro cao. Mã gắn security stamp nên khóa tài khoản, đổi mật khẩu làm mã mất hiệu lực
public sealed class StaffStepUpService(
    IStaffRepository staff,
    IPasswordHasher passwordHasher,
    StaffTwoFactorService twoFactor,
    IStaffChallengeTokens tokens,
    AuthSecurityEvents securityEvents,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<StepUpResponse>> VerifyAsync(Guid staffId, StepUpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        if (await staff.FindByIdAsync(staffId, cancellationToken) is not { IsActive: true } member)
        {
            return Result.Failure<StepUpResponse>(ErrorCodes.Unauthenticated);
        }

        if (member.LockoutEndUtc > now)
        {
            return Result.Failure<StepUpResponse>(ErrorCodes.AccountLocked);
        }

        if (!await PassesAsync(member, request, cancellationToken))
        {
            await staff.RegisterFailedLoginAsync(member.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            await securityEvents.StaffLoginFailedAsync(member.Id, member.Email, "wrong_step_up", cancellationToken);
            return Result.Failure<StepUpResponse>(ErrorCodes.InvalidCredentials);
        }

        var expires = now + AuthPolicy.StepUpLifetime;
        var token = tokens.Protect(new StaffChallenge(member.Id, StaffChallengeKind.StepUp, member.SecurityStamp, expires, SecondFactorPassed: true));
        securityEvents.RecordStaffStepUp(member);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new StepUpResponse(token, expires));
    }

    public async Task<bool> IsValidAsync(Guid staffId, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token)
            || tokens.Unprotect(token) is not { Kind: StaffChallengeKind.StepUp } challenge
            || challenge.StaffId != staffId
            || challenge.ExpiresAtUtc <= clock.UtcNow)
        {
            return false;
        }

        return await staff.FindByIdAsync(staffId, cancellationToken) is { IsActive: true } member
            && string.Equals(member.SecurityStamp, challenge.SecurityStamp, StringComparison.Ordinal);
    }

    private async Task<bool> PassesAsync(Staff member, StepUpRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.Code))
        {
            return await twoFactor.TryConsumeTotpAsync(member, request.Code, cancellationToken) is not null;
        }

        return !string.IsNullOrEmpty(request.Password)
            && request.Password.Length <= AuthPolicy.PasswordMaxLength
            && member.PasswordHash is { } hash
            && passwordHasher.Verify(hash, request.Password) != PasswordCheckResult.Failed;
    }
}
