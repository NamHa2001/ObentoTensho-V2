using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

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

    public Task RevokeAsync(long id, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken) =>
        Revoke(db.RefreshTokens.Where(t => t.Id == id), reason, nowUtc, cancellationToken);

    public Task RevokeFamilyAsync(Guid familyId, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken) =>
        Revoke(db.RefreshTokens.Where(t => t.FamilyId == familyId), reason, nowUtc, cancellationToken);

    public Task RevokeFamiliesAsync(IReadOnlyCollection<Guid> familyIds, RefreshTokenRevokedReason reason, DateTime nowUtc, CancellationToken cancellationToken) =>
        Revoke(db.RefreshTokens.Where(t => familyIds.Contains(t.FamilyId)), reason, nowUtc, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetActiveUserFamiliesAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > nowUtc)
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
