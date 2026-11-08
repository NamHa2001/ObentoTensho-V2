using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

// Mọi đường thu hồi đều báo danh sách chặn để access token còn hạn của phiên đó hết dùng được ngay (SEC-50)
internal sealed class RefreshTokenRepository(AppDbContext db, ISessionDenylist denylist) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<bool> StaffHasSessionsAsync(Guid staffId, CancellationToken cancellationToken) =>
        db.RefreshTokens.AnyAsync(t => t.StaffId == staffId, cancellationToken);

    public Task<bool> StaffSeenClientAsync(Guid staffId, string? networkPrefix, string? userAgent, CancellationToken cancellationToken) =>
        db.RefreshTokens.AnyAsync(
            t => t.StaffId == staffId && t.UserAgent == userAgent && (networkPrefix == null ? t.CreatedByIp == null : t.CreatedByIp!.StartsWith(networkPrefix)),
            cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public async Task<bool> TryMarkRotatedAsync(long id, string replacedByTokenHash, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var updated = await db.RefreshTokens
            .Where(t => t.Id == id && t.RevokedAtUtc == null && t.ReplacedByTokenHash == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.ReplacedByTokenHash, replacedByTokenHash)
                    .SetProperty(t => t.RevokedAtUtc, nowUtc)
                    .SetProperty(t => t.RevokedReason, RefreshTokenRevokedReason.Rotated),
                cancellationToken);
        return updated == 1;
    }

    public async Task RevokeAsync(long id, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokens.AsNoTracking().Where(t => t.Id == id).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(cancellationToken);
        await Revoke(db.RefreshTokens.Where(t => t.Id == id), reason, nowUtc, cancellationToken);
        if (family is { } familyId)
        {
            denylist.RevokeSession(familyId);
        }
    }

    public async Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await Revoke(db.RefreshTokens.Where(t => t.FamilyId == familyId), reason, nowUtc, cancellationToken);
        denylist.RevokeSession(familyId);
    }

    public async Task RevokeFamiliesAsync(IReadOnlyCollection<Guid> familyIds, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(familyIds);
        await Revoke(db.RefreshTokens.Where(t => familyIds.Contains(t.FamilyId)), reason, nowUtc, cancellationToken);
        foreach (var familyId in familyIds)
        {
            denylist.RevokeSession(familyId);
        }
    }

    public Task<IReadOnlyList<Guid>> GetActiveUserFamiliesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        ActiveFamilies(db.RefreshTokens.Where(t => t.UserId == userId), nowUtc, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListActiveForUserAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > nowUtc)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<Guid>> GetActiveStaffFamiliesAsync(Guid staffId, DateTime nowUtc, CancellationToken cancellationToken) =>
        ActiveFamilies(db.RefreshTokens.Where(t => t.StaffId == staffId), nowUtc, cancellationToken);

    public Task<DateTime> GetFamilyStartedAtAsync(Guid familyId, CancellationToken cancellationToken) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId).MinAsync(t => t.CreatedAtUtc, cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await DenyRecentFamiliesAsync(db.RefreshTokens.Where(t => t.UserId == userId), nowUtc, cancellationToken);
        await Revoke(db.RefreshTokens.Where(t => t.UserId == userId), reason, nowUtc, cancellationToken);
    }

    public async Task RevokeAllForStaffAsync(Guid staffId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await DenyRecentFamiliesAsync(db.RefreshTokens.Where(t => t.StaffId == staffId), nowUtc, cancellationToken);
        await Revoke(db.RefreshTokens.Where(t => t.StaffId == staffId), reason, nowUtc, cancellationToken);
    }

    // Phiên còn có thể có access token sống: refresh token cấp trong khoảng đời access token gần nhất
    private async Task DenyRecentFamiliesAsync(IQueryable<RefreshToken> tokens, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var since = nowUtc - denylist.Lifetime;
        foreach (var familyId in await tokens.Where(t => t.CreatedAtUtc >= since).Select(t => t.FamilyId).Distinct().ToListAsync(cancellationToken))
        {
            denylist.RevokeSession(familyId);
        }
    }

    // Token đầu chuỗi còn hiệu lực của mỗi phiên, mới nhất trước
    private static async Task<IReadOnlyList<Guid>> ActiveFamilies(IQueryable<RefreshToken> tokens, DateTime nowUtc, CancellationToken cancellationToken) =>
        await tokens.AsNoTracking()
            .Where(t => t.RevokedAtUtc == null && t.ExpiresAtUtc > nowUtc)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => t.FamilyId)
            .ToListAsync(cancellationToken);

    // Chỉ chạm token còn hiệu lực, giữ nguyên lý do thu hồi cũ
    private static Task<int> Revoke(IQueryable<RefreshToken> tokens, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken) =>
        tokens.Where(t => t.RevokedAtUtc == null).ExecuteUpdateAsync(
            s => s.SetProperty(t => t.RevokedAtUtc, nowUtc).SetProperty(t => t.RevokedReason, reason),
            cancellationToken);
}
