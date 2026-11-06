using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-03, FR-AUTH-04 cho nhân viên; hạn phiên theo SEC-74
public sealed class StaffSessionService(
    IStaffRepository staff,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    SessionFactory sessions,
    AuthSecurityEvents securityEvents,
    IClock clock)
{
    public async Task<Result<AuthSession>> RefreshAsync(string? refreshToken, ClientInfo client, CancellationToken cancellationToken)
    {
        var stored = string.IsNullOrEmpty(refreshToken)
            ? null
            : await refreshTokens.FindByHashAsync(sessions.HashRefreshToken(refreshToken), cancellationToken);
        if (stored?.StaffId is not { } staffId)
        {
            return Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);
        }

        var now = clock.UtcNow;
        if (stored.RevokedAtUtc is not null)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.ReuseDetected, now, cancellationToken);
            await securityEvents.RefreshTokenReuseDetectedAsync(stored.FamilyId, stored.UserId, stored.StaffId, cancellationToken);
            return Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);
        }

        if (stored.ExpiresAtUtc <= now)
        {
            return Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);
        }

        var member = await staff.FindByIdAsync(staffId, cancellationToken);
        if (member is null || !member.IsActive)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.AccountLocked, now, cancellationToken);
            return Result.Failure<AuthSession>(ErrorCodes.AccountLocked);
        }

        var next = sessions.NewRefreshToken();
        if (!await refreshTokens.TryMarkRotatedAsync(stored.Id, next.Hash, now, cancellationToken))
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, RefreshTokenRevokedReason.ReuseDetected, now, cancellationToken);
            await securityEvents.RefreshTokenReuseDetectedAsync(stored.FamilyId, stored.UserId, stored.StaffId, cancellationToken);
            return Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);
        }

        var startedAt = await refreshTokens.GetFamilyStartedAtAsync(stored.FamilyId, cancellationToken);
        var session = sessions.CreateForStaff(member, stored.FamilyId, startedAt, next, client);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(session);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return;
        }

        var stored = await refreshTokens.FindByHashAsync(sessions.HashRefreshToken(refreshToken), cancellationToken);
        if (stored?.StaffId is not null && stored.RevokedAtUtc is null)
        {
            await refreshTokens.RevokeAsync(stored.Id, RefreshTokenRevokedReason.Logout, clock.UtcNow, cancellationToken);
        }
    }
}
