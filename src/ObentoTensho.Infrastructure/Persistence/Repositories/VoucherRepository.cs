using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class VoucherRepository(AppDbContext db) : IVoucherRepository
{
    public Task<Voucher?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        db.Vouchers.AsNoTracking().SingleOrDefaultAsync(v => v.Code == code, cancellationToken);

    public Task<Voucher?> FindAsync(int id, CancellationToken cancellationToken) =>
        db.Vouchers.SingleOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Voucher>> FindBatchAsync(Guid batchId, CancellationToken cancellationToken) =>
        await db.Vouchers.Where(v => v.BatchId == batchId).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Voucher> Items, int Total)> ListAsync(VoucherQuery query, CancellationToken cancellationToken)
    {
        var source = Filter(query);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(v => v.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Voucher>> ListOwnedAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Vouchers.AsNoTracking().Where(v => v.OwnerUserId == userId).OrderByDescending(v => v.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Voucher>> ListForExportAsync(VoucherQuery query, int limit, CancellationToken cancellationToken) =>
        await Filter(query).OrderByDescending(v => v.Id).Take(limit).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<User>> ListBirthdayDueAsync(int month, int day, bool includeLeapDay, int year, Guid? afterId, int limit, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted && u.IsEmailVerified && u.DateOfBirth != null
                && ((u.DateOfBirth.Value.Month == month && u.DateOfBirth.Value.Day == day)
                    || (includeLeapDay && u.DateOfBirth.Value.Month == 2 && u.DateOfBirth.Value.Day == 29))
                && !db.Vouchers.Any(v => v.OwnerUserId == u.Id && v.BirthdayYear == year)
                && (afterId == null || u.Id.CompareTo(afterId.Value) > 0))
            .OrderBy(u => u.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<bool> PromotionExistsAsync(int promotionId, CancellationToken cancellationToken) =>
        db.Promotions.AnyAsync(p => p.Id == promotionId && p.DeletedAtUtc == null, cancellationToken);

    public async Task<IReadOnlySet<string>> ExistingCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in codes.Chunk(1000))
        {
            found.UnionWith(await db.Vouchers.Where(v => chunk.Contains(v.Code)).Select(v => v.Code).ToListAsync(cancellationToken));
        }

        return found;
    }

    public async Task<int> UsedByUserAsync(int voucherId, Guid userId, CancellationToken cancellationToken) =>
        await db.UserVouchers.Where(u => u.VoucherId == voucherId && u.UserId == userId).Select(u => u.UsedCount).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> HasUsageAsync(int voucherId, CancellationToken cancellationToken) =>
        await db.UserVouchers.AnyAsync(u => u.VoucherId == voucherId, cancellationToken)
        || await db.Orders.AnyAsync(o => o.VoucherId == voucherId, cancellationToken);

    private IQueryable<Voucher> Filter(VoucherQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.Vouchers.AsNoTracking()
            .Where(v => (query.Source == null || v.Source == query.Source)
                && (query.ApprovalStatus == null || v.ApprovalStatus == query.ApprovalStatus)
                && (query.BatchId == null || v.BatchId == query.BatchId));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = VoucherCodes.Normalize(query.Search);
            source = source.Where(v => v.Code.Contains(term));
        }

        return source;
    }

    public void Add(Voucher voucher) => db.Vouchers.Add(voucher);

    public void AddRange(IEnumerable<Voucher> vouchers) => db.Vouchers.AddRange(vouchers);

    public void Remove(Voucher voucher) => db.Vouchers.Remove(voucher);

    public async Task<bool> TryUseAsync(int voucherId, CancellationToken cancellationToken) =>
        await db.Vouchers
            .Where(v => v.Id == voucherId && v.IsActive && v.ApprovalStatus == ApprovalStatus.Approved
                && (v.MaxUsageTotal == null || v.UsedCount < v.MaxUsageTotal))
            .ExecuteUpdateAsync(set => set.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), cancellationToken) == 1;

    public async Task<bool> TryUseForUserAsync(int voucherId, Guid userId, int maxPerUser, DateTime nowUtc, CancellationToken cancellationToken)
    {
        // Tạo dòng đếm nếu chưa có (khóa phạm vi để hai đơn cùng lúc không cùng chèn), rồi tăng có điều kiện
        await db.Database.ExecuteSqlAsync($"""
            IF NOT EXISTS (SELECT 1 FROM [UserVouchers] WITH (UPDLOCK, HOLDLOCK) WHERE [VoucherId] = {voucherId} AND [UserId] = {userId})
                INSERT INTO [UserVouchers] ([UserId], [VoucherId], [UsedCount], [CreatedAtUtc]) VALUES ({userId}, {voucherId}, 0, {nowUtc});
            """, cancellationToken);
        return await db.UserVouchers
            .Where(u => u.VoucherId == voucherId && u.UserId == userId && u.UsedCount < maxPerUser)
            .ExecuteUpdateAsync(
                set => set.SetProperty(u => u.UsedCount, u => u.UsedCount + 1).SetProperty(u => u.LastUsedAtUtc, nowUtc),
                cancellationToken) == 1;
    }

    public async Task ReleaseAsync(int voucherId, Guid? userId, CancellationToken cancellationToken)
    {
        await db.Vouchers.Where(v => v.Id == voucherId && v.UsedCount > 0)
            .ExecuteUpdateAsync(set => set.SetProperty(v => v.UsedCount, v => v.UsedCount - 1), cancellationToken);
        if (userId is { } id)
        {
            await db.UserVouchers.Where(u => u.VoucherId == voucherId && u.UserId == id && u.UsedCount > 0)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.UsedCount, u => u.UsedCount - 1), cancellationToken);
        }
    }
}
