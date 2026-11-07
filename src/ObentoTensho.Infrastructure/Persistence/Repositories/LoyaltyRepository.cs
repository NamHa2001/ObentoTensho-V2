using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class LoyaltyRepository(AppDbContext db) : ILoyaltyRepository
{
    public async Task<User?> LockUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Giữ khóa cập nhật tới hết transaction: hai thao tác điểm của cùng khách chạy nối tiếp nhau
        await db.Database.ExecuteSqlAsync($"SELECT [Id] FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {userId}", cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null && db.Entry(user).State == EntityState.Unchanged)
        {
            // Bản đã nạp trước khi khóa có thể cũ; bản đang có thay đổi trong transaction này thì giữ nguyên
            await db.Entry(user).ReloadAsync(cancellationToken);
        }

        return user;
    }

    public Task<User?> FindUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public Task<bool> EntryExistsAsync(Guid userId, LoyaltyTransactionType type, string referenceId, CancellationToken cancellationToken) =>
        db.LoyaltyPoints.AnyAsync(e => e.UserId == userId && e.Type == type && e.ReferenceId == referenceId, cancellationToken);

    public Task<LoyaltyEntry?> FindEntryAsync(Guid userId, LoyaltyTransactionType type, string referenceId, CancellationToken cancellationToken) =>
        db.LoyaltyPoints.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == userId && e.Type == type && e.ReferenceId == referenceId, cancellationToken);

    public Task<int> SumPointsAsync(Guid userId, LoyaltyTransactionType type, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.LoyaltyPoints.Where(e => e.UserId == userId && e.Type == type && e.CreatedAtUtc >= sinceUtc).SumAsync(e => e.Points, cancellationToken);

    public void AddEntry(LoyaltyEntry entry) => db.LoyaltyPoints.Add(entry);

    public void AddLot(LoyaltyLot lot) => db.LoyaltyLots.Add(lot);

    public async Task<IReadOnlyList<LoyaltyLot>> ListOpenLotsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.LoyaltyLots
            .Where(l => l.UserId == userId && l.RemainingPoints > 0)
            .OrderBy(l => l.ExpiresAtUtc).ThenBy(l => l.LedgerId)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<LoyaltyEntry> Items, int Total)> ListEntriesAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.LoyaltyPoints.AsNoTracking().Where(e => e.UserId == userId);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(int Points, DateTime? NextExpiryUtc)> ExpiringAsync(Guid userId, DateTime untilUtc, CancellationToken cancellationToken)
    {
        var lots = await db.LoyaltyLots.AsNoTracking()
            .Where(l => l.UserId == userId && l.RemainingPoints > 0 && l.ExpiresAtUtc <= untilUtc)
            .Select(l => new { l.RemainingPoints, l.ExpiresAtUtc })
            .ToListAsync(cancellationToken);
        return (lots.Sum(l => l.RemainingPoints), lots.Count == 0 ? null : lots.Min(l => l.ExpiresAtUtc));
    }

    public async Task<IReadOnlyList<Guid>> UsersWithExpiredLotsAsync(DateTime nowUtc, int limit, CancellationToken cancellationToken) =>
        await db.LoyaltyLots.AsNoTracking()
            .Where(l => l.RemainingPoints > 0 && l.ExpiresAtUtc <= nowUtc)
            .Select(l => l.UserId).Distinct().Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> UsersToNotifyExpiryAsync(DateTime nowUtc, DateTime untilUtc, int limit, CancellationToken cancellationToken) =>
        await db.LoyaltyLots.AsNoTracking()
            .Where(l => l.RemainingPoints > 0 && l.ExpiresAtUtc > nowUtc && l.ExpiresAtUtc <= untilUtc && l.ExpiryNoticeSentAtUtc == null)
            .Select(l => l.UserId).Distinct().Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LoyaltyLot>> ListLotsToNotifyAsync(Guid userId, DateTime nowUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        await db.LoyaltyLots
            .Where(l => l.UserId == userId && l.RemainingPoints > 0 && l.ExpiresAtUtc > nowUtc && l.ExpiresAtUtc <= untilUtc && l.ExpiryNoticeSentAtUtc == null)
            .ToListAsync(cancellationToken);

    public void AddAdjustment(LoyaltyAdjustment adjustment) => db.LoyaltyAdjustments.Add(adjustment);

    public Task<LoyaltyAdjustment?> FindAdjustmentAsync(Guid id, CancellationToken cancellationToken) =>
        db.LoyaltyAdjustments.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LoyaltyAdjustment>> ListAdjustmentsAsync(ApprovalStatus? status, int limit, CancellationToken cancellationToken) =>
        await db.LoyaltyAdjustments.AsNoTracking()
            .Where(a => status == null || a.Status == status)
            .OrderByDescending(a => a.RequestedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
