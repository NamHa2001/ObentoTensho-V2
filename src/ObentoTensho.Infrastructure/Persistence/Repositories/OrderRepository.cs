using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    private static readonly OrderStatus[] Open = [OrderStatus.Pending, OrderStatus.Preparing, OrderStatus.Ready];

    public void Add(Order order) => db.Orders.Add(order);

    public void AddHistory(OrderStatusHistory history) => db.OrderStatusHistory.Add(history);

    public Task<Order?> FindForUpdateAsync(Guid id, bool withItems, CancellationToken cancellationToken) =>
        Source(withItems, tracking: true).SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> FindByCodeAsync(string code, bool withItems, CancellationToken cancellationToken) =>
        Source(withItems, tracking: true).SingleOrDefaultAsync(o => o.OrderCode == code, cancellationToken);

    public async Task<IReadOnlyList<OrderStatusHistory>> GetHistoryAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.OrderStatusHistory.AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.Id)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Order> Items, int Total)> ListAsync(OrderQuery query, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.Orders.AsNoTracking()
            .Where(o => (query.Status == null || o.Status == query.Status)
                && (fromUtc == null || o.PickupAtUtc >= fromUtc)
                && (toUtc == null || o.PickupAtUtc < toUtc));

        if (query.RefundPending)
        {
            source = source.Where(o => o.Status == OrderStatus.Cancelled && o.PaymentStatus == PaymentStatus.Paid && o.RefundedAtUtc == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var code = OrderCodes.Normalize(term);
            var phone = VietnamesePhoneNumber.TryNormalize(term, out var normalized) ? normalized : null;
            source = source.Where(o => o.OrderCode == code || o.CustomerPhone == phone || o.CustomerName.Contains(term));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(o => o.Items)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Order>> ListOpenAsync(int limit, CancellationToken cancellationToken) =>
        await Source(withItems: true, tracking: false)
            .Where(o => Open.Contains(o.Status))
            .OrderBy(o => o.PickupAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Order> Items, int Total)> ListForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var source = db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<int> CountOpenForPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Orders.CountAsync(o => o.CustomerPhone == phone && Open.Contains(o.Status), cancellationToken);

    public Task<bool> HasCompletedForPhoneAsync(string phone, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(o => o.CustomerPhone == phone && o.Status == OrderStatus.Completed, cancellationToken);

    public async Task<int> NextDailySeqAsync(DateOnly date, CancellationToken cancellationToken)
    {
        const string kind = DailyCounter.OrderKind;
        await db.Database.ExecuteSqlAsync($"""
            IF NOT EXISTS (SELECT 1 FROM [DailyCounters] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] = {date} AND [Kind] = {kind})
                INSERT INTO [DailyCounters] ([Date], [Kind], [LastValue]) VALUES ({date}, {kind}, 0);
            """, cancellationToken);

        var values = await db.Database.SqlQuery<int>($"""
            UPDATE [DailyCounters] SET [LastValue] = [LastValue] + 1
            OUTPUT inserted.[LastValue] AS [Value]
            WHERE [Date] = {date} AND [Kind] = {kind}
            """).ToListAsync(cancellationToken);
        return values.Single();
    }

    public async Task<bool> TryMarkPaidAsync(Guid id, Guid staffId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await db.Orders
            .Where(o => o.Id == id && o.PaymentStatus == PaymentStatus.Unpaid && o.Status != OrderStatus.Cancelled)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(o => o.PaymentStatus, PaymentStatus.Paid)
                    .SetProperty(o => o.PaidAtUtc, nowUtc)
                    .SetProperty(o => o.PaidByStaffId, staffId)
                    .SetProperty(o => o.UpdatedAtUtc, nowUtc),
                cancellationToken) == 1;

    private IQueryable<Order> Source(bool withItems, bool tracking)
    {
        IQueryable<Order> source = tracking ? db.Orders : db.Orders.AsNoTracking();
        return withItems ? source.Include(o => o.Items).ThenInclude(i => i.Options).AsSplitQuery() : source;
    }
}
