using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-02 (đăng nhập), FR-AUTH-03 (làm mới phiên), FR-AUTH-04 (đăng xuất) cho khách
public sealed class CustomerSessionService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    SessionFactory sessions,
    IClock clock,
    IValidator<LoginRequest> loginValidator)
{
    private static readonly Result<AuthSession> InvalidCredentials = Result.Failure<AuthSession>(ErrorCodes.InvalidCredentials);
    private static readonly Result<AuthSession> Unauthenticated = Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);

    public async Task<Result<AuthSession>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AuthSession>();
        }

        var now = clock.UtcNow;
        var user = await users.FindByEmailAsync(EmailAddress.Normalize(request.Email), cancellationToken);

        // Không có tài khoản, đã xóa, chưa đặt mật khẩu hay đang bị khóa: cùng phản hồi, cùng thời gian (BR-AUTH-14)
        if (user?.PasswordHash is null || user.IsDeleted || user.LockoutEndUtc > now)
        {
            passwordHasher.SimulateVerify(request.Password);
            return InvalidCredentials;
        }

        var check = passwordHasher.Verify(user.PasswordHash, request.Password);
        if (check == PasswordCheckResult.Failed)
        {
            await users.RegisterFailedLoginAsync(user.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return InvalidCredentials;
        }

        await AfterSuccessfulPasswordAsync(user, check, request.Password, cancellationToken);
        await sessions.EnforceUserSessionLimitAsync(user.Id, cancellationToken);
        var session = sessions.CreateForUser(user, Guid.CreateVersion7(), sessions.NewRefreshToken(), client);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success<AuthSession>(session);
    }

    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthenticated;
        }

        var stored = await refreshTokens.FindByHashAsync(sessions.HashRefreshToken(refreshToken), cancellationToken);
        if (stored?.UserId is not { } userId)
        {
            return Unauthenticated;
        }

        var now = clock.UtcNow;

        // Token đã bị thay thế/thu hồi mà vẫn được gửi lại: coi là bị đánh cắp, hủy cả gia đình
        if (stored.RevokedAtUtc is not null)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.ReuseDetected, now, cancellationToken);
            return Unauthenticated;
        }

        if (stored.ExpiresAtUtc <= now)
        {
            return Unauthenticated;
        }

        // BR-AUTH-05: kiểm lại trạng thái tài khoản mỗi lần làm mới
        var user = await users.FindByIdAsync(userId, cancellationToken);
        if (user is null || user.IsDeleted || !user.IsActive)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.AccountLocked, now, cancellationToken);
            return Result.Failure<AuthSession>(ErrorCodes.AccountLocked);
        }

        var next = sessions.NewRefreshToken();
        if (!await refreshTokens.TryMarkRotatedAsync(stored.Id, next.Hash, now, cancellationToken))
        {
            // Yêu cầu khác vừa dùng cùng token trước
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.ReuseDetected, now, cancellationToken);
            return Unauthenticated;
        }

        var session = sessions.CreateForUser(user, stored.FamilyId, next, client);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success<AuthSession>(session);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var stored = await refreshTokens.FindByHashAsync(sessions.HashRefreshToken(refreshToken), cancellationToken);
        if (stored?.UserId is not null && stored.RevokedAtUtc is null)
        {
            await refreshTokens.RevokeAsync(stored.Id, RefreshTokenRevokedReason.Logout, clock.UtcNow, cancellationToken);
        }
    }

    private async Task AfterSuccessfulPasswordAsync(User user, PasswordCheckResult check, string password, CancellationToken cancellationToken)
    {
        if (user.FailedLoginCount > 0 || user.LockoutEndUtc is not null)
        {
            await users.ResetFailedLoginsAsync(user.Id, cancellationToken);
        }

        // Băm lại khi số vòng cấu hình tăng lên
        if (check == PasswordCheckResult.SuccessRehashNeeded && user.PasswordHash is not null)
        {
            await users.ReplacePasswordHashAsync(user.Id, user.PasswordHash, passwordHasher.Hash(password), cancellationToken);
        }
    }
}
