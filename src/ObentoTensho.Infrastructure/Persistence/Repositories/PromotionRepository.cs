using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Promotions.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class PromotionRepository(AppDbContext db) : IPromotionRepository
{
    public async Task<IReadOnlyList<PublicPromotionEntry>> LoadPublicAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var promotions = await db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.DeletedAtUtc == null && p.EndDate >= today)
            .ToListAsync(cancellationToken);
        var ids = promotions.Select(p => p.Id).ToArray();

        // Chỉ mã dùng chung: không lộ mã cá nhân và mã sinh hàng loạt (phát riêng từng người)
        var vouchers = (await db.Vouchers.AsNoTracking()
            .Where(v => v.PromotionId != null && ids.Contains(v.PromotionId.Value) && v.IsActive
                && v.ApprovalStatus == ApprovalStatus.Approved && v.OwnerUserId == null && v.BatchId == null)
            .ToListAsync(cancellationToken))
            .ToLookup(v => v.PromotionId);
        return promotions.Select(p => new PublicPromotionEntry(p, vouchers[p.Id].ToArray())).ToArray();
    }

    public async Task<(IReadOnlyList<Promotion> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.Promotions.AsNoTracking().Where(p => p.DeletedAtUtc == null);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(p => p.StartDate).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Promotion?> FindAsync(int id, CancellationToken cancellationToken) =>
        db.Promotions.SingleOrDefaultAsync(p => p.Id == id && p.DeletedAtUtc == null, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken) =>
        db.Promotions.AnyAsync(p => p.Slug == slug && p.Id != exceptId, cancellationToken);

    public void Add(Promotion promotion) => db.Promotions.Add(promotion);
}
