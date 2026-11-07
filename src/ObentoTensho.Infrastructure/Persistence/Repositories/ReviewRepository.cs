using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class ReviewRepository(AppDbContext db) : IReviewRepository
{
    public async Task<ReviewOrderFacts?> FindOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.UserId == userId)
            .Select(o => new { o.Status, o.CompletedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.OrderId == orderId && i.MenuItemId != null)
            .Select(i => i.MenuItemId!.Value)
            .ToListAsync(cancellationToken);
        return new ReviewOrderFacts(order.Status, order.CompletedAtUtc, items);
    }

    public Task<int> CountCreatedSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        db.Reviews.CountAsync(r => r.UserId == userId && r.CreatedAtUtc >= sinceUtc, cancellationToken);

    public Task<bool> ExistsAsync(Guid userId, Guid orderId, int? menuItemId, CancellationToken cancellationToken) =>
        db.Reviews.AnyAsync(r => r.UserId == userId && r.OrderId == orderId && r.MenuItemId == menuItemId, cancellationToken);

    public Task<Review?> FindAsync(int id, CancellationToken cancellationToken) =>
        db.Reviews.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Review>> ListMineAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Reviews.AsNoTracking().Where(r => r.UserId == userId).OrderByDescending(r => r.Id).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<ReviewRow> Items, int Total)> ListAsync(ReviewStatus? status, int? menuItemId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.Reviews.AsNoTracking()
            .Where(r => (status == null || r.Status == status) && (menuItemId == null || r.MenuItemId == menuItemId));
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Users, r => r.UserId, u => u.Id, (r, u) => new { Review = r, u.FullName })
            .Join(db.Orders, x => x.Review.OrderId, o => o.Id, (x, o) => new
            {
                x.Review,
                x.FullName,
                o.OrderCode,
                ItemName = db.OrderItems.Where(i => i.OrderId == x.Review.OrderId && i.MenuItemId == x.Review.MenuItemId).Select(i => i.ItemName).FirstOrDefault(),
            })
            .OrderByDescending(x => x.Review.Id)
            .ToListAsync(cancellationToken);
        return (items.Select(x => new ReviewRow(x.Review, x.FullName, x.OrderCode, x.ItemName)).ToArray(), total);
    }

    public void Add(Review review) => db.Reviews.Add(review);

    public void Remove(Review review) => db.Reviews.Remove(review);
}
