using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Reports.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class StatsRepository(AppDbContext db) : IStatsRepository
{
    private const string Vietnam = "SE Asia Standard Time";

    // BR-RPT-30
    private const int QueryTimeoutSeconds = 15;

    // BR-RPT-10, BR-RPT-31: gom nhóm trong SQL, giờ VN bằng AT TIME ZONE; đơn lấy theo mốc riêng của từng số liệu; đặt bàn theo ReservedDate
    public async Task<IReadOnlyList<StatRow>> ComputeDayAsync(DateOnly day, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var rows = await db.Database.SqlQuery<StatSqlRow>($"""
            SELECT 'revenue_paid' AS Metric, '' AS Dimension, CAST(SUM(o.TotalAmount) AS decimal(18,2)) AS Value
            FROM Orders o WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'refunds_amount', '', CAST(SUM(o.TotalAmount) AS decimal(18,2))
            FROM Orders o WHERE o.RefundedAtUtc >= {startUtc} AND o.RefundedAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'revenue_paid', CONCAT('category:', m.CategoryId), CAST(SUM((i.ItemPrice + i.OptionsPrice) * i.Quantity) AS decimal(18,2))
            FROM Orders o JOIN OrderItems i ON i.OrderId = o.Id JOIN MenuItems m ON m.Id = i.MenuItemId
            WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} AND i.VoidedAtUtc IS NULL GROUP BY m.CategoryId
            UNION ALL
            SELECT 'item_quantity', CONCAT('item:', i.MenuItemId), CAST(SUM(i.Quantity) AS decimal(18,2))
            FROM Orders o JOIN OrderItems i ON i.OrderId = o.Id
            WHERE o.CreatedAtUtc >= {startUtc} AND o.CreatedAtUtc < {endUtc} AND o.Status <> 4 AND i.MenuItemId IS NOT NULL AND i.VoidedAtUtc IS NULL GROUP BY i.MenuItemId
            UNION ALL
            SELECT 'item_revenue', CONCAT('item:', i.MenuItemId), CAST(SUM((i.ItemPrice + i.OptionsPrice) * i.Quantity) AS decimal(18,2))
            FROM Orders o JOIN OrderItems i ON i.OrderId = o.Id
            WHERE o.CreatedAtUtc >= {startUtc} AND o.CreatedAtUtc < {endUtc} AND o.Status <> 4 AND i.MenuItemId IS NOT NULL AND i.VoidedAtUtc IS NULL GROUP BY i.MenuItemId
            UNION ALL
            SELECT 'orders_created', '', COUNT(*) FROM Orders o WHERE o.CreatedAtUtc >= {startUtc} AND o.CreatedAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'orders_created', CONCAT('status:', o.Status), COUNT(*) FROM Orders o WHERE o.CreatedAtUtc >= {startUtc} AND o.CreatedAtUtc < {endUtc} GROUP BY o.Status
            UNION ALL
            SELECT 'orders_created', CONCAT('hour:', h.Hour), COUNT(*)
            FROM Orders o CROSS APPLY (SELECT DATEPART(hour, o.CreatedAtUtc AT TIME ZONE 'UTC' AT TIME ZONE {Vietnam}) AS Hour) h
            WHERE o.CreatedAtUtc >= {startUtc} AND o.CreatedAtUtc < {endUtc} GROUP BY h.Hour
            UNION ALL
            SELECT 'orders_paid', '', COUNT(*) FROM Orders o WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'orders_completed', '', COUNT(*) FROM Orders o WHERE o.CompletedAtUtc >= {startUtc} AND o.CompletedAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'orders_cancelled', '', COUNT(*) FROM Orders o WHERE o.CancelledAtUtc >= {startUtc} AND o.CancelledAtUtc < {endUtc} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'reservations_total', '', COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'reservations_total', CONCAT('status:', r.Status), COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} GROUP BY r.Status
            UNION ALL
            SELECT 'reservations_total', CONCAT('hour:', DATEPART(hour, r.ReservedTime)), COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} GROUP BY DATEPART(hour, r.ReservedTime)
            UNION ALL
            SELECT 'reservations_total', CONCAT('area:', t.AreaId), COUNT(*)
            FROM Reservations r JOIN [Tables] t ON t.Id = r.TableId WHERE r.ReservedDate = {day} GROUP BY t.AreaId
            UNION ALL
            SELECT 'reservations_arrived', '', COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} AND r.Status IN (2, 3) HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'reservations_cancelled', '', COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} AND r.Status = 4 HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'reservations_noshow', '', COUNT(*) FROM Reservations r WHERE r.ReservedDate = {day} AND r.Status = 5 HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'reservation_guests', '', SUM(r.AdultCount + r.ChildCount) FROM Reservations r WHERE r.ReservedDate = {day} AND r.Status <> 4 HAVING COUNT(*) > 0
            UNION ALL
            SELECT 'new_customers', '', COUNT(*) FROM Users u WHERE u.CreatedAtUtc >= {startUtc} AND u.CreatedAtUtc < {endUtc} HAVING COUNT(*) > 0
            """).ToListAsync(cancellationToken);
        return rows.Concat(await ComputeChannelsAsync(startUtc, endUtc, cancellationToken)).Select(r => new StatRow(r.Metric, r.Dimension, r.Value)).ToArray();
    }

    // Theo kênh bán (mang về / tại quán / giao hàng) và tiền ship thực thu, cùng mốc ngày thanh toán/hoàn tiền như tổng
    private async Task<List<StatSqlRow>> ComputeChannelsAsync(DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken) =>
        await db.Database.SqlQuery<StatSqlRow>($"""
            SELECT 'revenue_paid' AS Metric, CONCAT('channel:', o.Channel) AS Dimension, CAST(SUM(o.TotalAmount) AS decimal(18,2)) AS Value
            FROM Orders o WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} GROUP BY o.Channel
            UNION ALL
            SELECT 'refunds_amount', CONCAT('channel:', o.Channel), CAST(SUM(o.TotalAmount) AS decimal(18,2))
            FROM Orders o WHERE o.RefundedAtUtc >= {startUtc} AND o.RefundedAtUtc < {endUtc} GROUP BY o.Channel
            UNION ALL
            SELECT 'orders_paid', CONCAT('channel:', o.Channel), COUNT(*)
            FROM Orders o WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} GROUP BY o.Channel
            UNION ALL
            SELECT 'shipping_collected', '', CAST(SUM(o.DeliveryFee - o.ShippingDiscount) AS decimal(18,2))
            FROM Orders o WHERE o.PaidAtUtc >= {startUtc} AND o.PaidAtUtc < {endUtc} AND o.Channel = 2 HAVING COUNT(*) > 0
            """).ToListAsync(cancellationToken);

    public async Task ReplaceDayAsync(DateOnly day, IReadOnlyList<StatRow> rows, DateTime nowUtc, CancellationToken cancellationToken)
    {
        await db.DailyStats.Where(s => s.Date == day).ExecuteDeleteAsync(cancellationToken);
        db.DailyStats.AddRange(rows.Select(r => new DailyStat { Date = day, Metric = r.Metric, Dimension = r.Dimension, Value = r.Value, UpdatedAtUtc = nowUtc }));
    }

    public async Task<IReadOnlyList<DailyStat>> ReadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.DailyStats.AsNoTracking().Where(s => s.Date >= from && s.Date <= to).ToListAsync(cancellationToken);

    public async Task<ReportNames> LoadNamesAsync(CancellationToken cancellationToken)
    {
        var categories = await db.MenuCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var items = await db.MenuItems.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.NameVi, cancellationToken);
        var areas = await db.TableAreas.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);
        return new ReportNames(categories, items, areas);
    }

    public async Task<CustomerStats> LoadCustomerStatsAsync(DateTime fromUtc, DateTime toUtc, DateTime retentionFromUtc, int topLimit, CancellationToken cancellationToken)
    {
        db.Database.SetCommandTimeout(QueryTimeoutSeconds);
        var tiers = await db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .GroupBy(u => u.MemberTier)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var perCustomer = db.Orders.AsNoTracking()
            .Where(o => o.UserId != null && o.Status != OrderStatus.Cancelled && o.CreatedAtUtc >= retentionFromUtc && o.CreatedAtUtc < toUtc)
            .GroupBy(o => o.UserId)
            .Select(g => g.Count());
        var active = await perCustomer.CountAsync(cancellationToken);
        var returning = await perCustomer.CountAsync(c => c >= 2, cancellationToken);
        var top = await db.Orders.AsNoTracking()
            .Where(o => o.UserId != null && o.Status != OrderStatus.Cancelled && o.PaidAtUtc >= fromUtc && o.PaidAtUtc < toUtc)
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, Orders = g.Count(), Spent = g.Sum(o => o.TotalAmount) })
            .OrderByDescending(x => x.Spent)
            .Take(topLimit)
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new TopCustomerDto(u.Id, u.FullName, x.Orders, x.Spent))
            .ToListAsync(cancellationToken);
        return new CustomerStats(tiers.Select(t => new TierCountDto(t.Key.ToString(), t.Count)).ToArray(), active, returning, top.OrderByDescending(t => t.Spent).ToArray());
    }

    // Một truy vấn cho mọi phần phụ của bảng tin (BR-RPT-32)
    public async Task<DashboardExtras> LoadDashboardExtrasAsync(DateOnly today, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var soon = nowUtc.AddDays(3);
        var rows = await db.Database.SqlQuery<StatSqlRow>($"""
            SELECT 'unread' AS Metric, '' AS Dimension, CAST(COUNT(*) AS decimal(18,2)) AS Value FROM ContactMessages WHERE IsRead = 0
            UNION ALL
            SELECT 'voucher', v.Code, DATEDIFF(hour, {nowUtc}, v.EndUtc) FROM Vouchers v
            WHERE v.IsActive = 1 AND v.OwnerUserId IS NULL AND v.EndUtc > {nowUtc} AND v.EndUtc <= {soon}
            UNION ALL
            SELECT 'slot', CONVERT(varchar(5), s.StartTime, 108), d.BookedCount * 100 / NULLIF(s.MaxReservations, 0)
            FROM SlotDayCapacity d JOIN ServiceSlots s ON s.Id = d.SlotId
            WHERE d.Date = {today} AND d.BookedCount * 100 > s.MaxReservations * 80
            UNION ALL
            SELECT CONCAT('category:', c.Name), '', c.Id FROM MenuCategories c
            UNION ALL
            SELECT CONCAT('item:', m.NameVi), '', m.Id FROM MenuItems m
            """).ToListAsync(cancellationToken);

        var unread = (int)rows.Where(r => string.Equals(r.Metric, "unread", StringComparison.Ordinal)).Sum(r => r.Value);
        var vouchers = rows.Where(r => string.Equals(r.Metric, "voucher", StringComparison.Ordinal)).Select(r => (r.Dimension, (int)r.Value)).OrderBy(v => v.Item2).ToArray();
        var slots = rows.Where(r => string.Equals(r.Metric, "slot", StringComparison.Ordinal)).Select(r => (r.Dimension, (int)r.Value)).OrderBy(s => s.Dimension, StringComparer.Ordinal).ToArray();
        return new DashboardExtras(unread, vouchers, slots, Names(rows, "category:"), Names(rows, "item:"));
    }

    private static Dictionary<int, string> Names(List<StatSqlRow> rows, string prefix) =>
        rows.Where(r => r.Metric.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(r => decimal.ToInt32(r.Value), r => r.Metric[prefix.Length..]);
}
