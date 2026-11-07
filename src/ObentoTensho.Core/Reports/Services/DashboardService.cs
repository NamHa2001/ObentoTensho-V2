using System.Globalization;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reports.Services;

// FR-RPT-01: hôm nay tính trực tiếp, các ngày trước đọc DailyStats; không quá 3 truy vấn và đệm 45 giây (BR-RPT-04, BR-RPT-32)
public sealed class DashboardService(IStatsRepository stats, BusinessCalendar calendar, IDashboardCache cache, IClock clock)
{
    private const int ChartDays = 7;
    private const int TopItemCount = 10;
    private const int HistoryDays = 70;

    public Task<ManagerDashboardDto> ManagerAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync("manager", BuildManagerAsync, cancellationToken);

    public Task<ReceptionDashboardDto> ReceptionAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync("reception", BuildReceptionAsync, cancellationToken);

    private async Task<ManagerDashboardDto> BuildManagerAsync(CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var rows = await LoadAsync(today, cancellationToken);
        var extras = await stats.LoadDashboardExtrasAsync(today, clock.UtcNow, cancellationToken);
        var todayRows = rows.Where(r => r.Date == today).ToArray();
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var span = today.DayNumber - monthStart.DayNumber;
        return new ManagerDashboardDto(
            new PeriodValueDto(Revenue(rows, today, today), Revenue(rows, today.AddDays(-1), today.AddDays(-1))),
            new PeriodValueDto(Revenue(rows, weekStart, today), Revenue(rows, weekStart.AddDays(-7), today.AddDays(-7))),
            new PeriodValueDto(Revenue(rows, monthStart, today), Revenue(rows, monthStart.AddMonths(-1), monthStart.AddMonths(-1).AddDays(span))),
            ReservationStatuses(todayRows),
            OrderStatuses(todayRows),
            (int)Sum(rows, StatMetrics.NewCustomers, monthStart, today),
            extras.UnreadContacts,
            Days(today).Select(d => new DayValueDto(d, Revenue(rows, d, d))).ToArray(),
            Days(today).Select(d => new DayValueDto(d, Sum(rows, StatMetrics.ReservationsTotal, d, d))).ToArray(),
            Named(rows, StatMetrics.RevenuePaid, StatMetrics.CategoryPrefix, today.AddDays(1 - ChartDays), today, extras.CategoryNames, int.MaxValue),
            Named(rows, StatMetrics.ItemQuantity, StatMetrics.ItemPrefix, today.AddDays(1 - ChartDays), today, extras.ItemNames, TopItemCount),
            extras.ExpiringVouchers.Select(v => new ExpiringVoucherDto(v.Code, v.HoursLeft)).ToArray(),
            extras.BusySlots.Select(s => new BusySlotDto(s.StartTime, s.PercentFull)).ToArray());
    }

    private async Task<ReceptionDashboardDto> BuildReceptionAsync(CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var rows = await LoadAsync(today, cancellationToken);
        var extras = await stats.LoadDashboardExtrasAsync(today, clock.UtcNow, cancellationToken);
        return new ReceptionDashboardDto(
            ReservationStatuses(rows.Where(r => r.Date == today).ToArray()),
            Days(today).Select(d => new DayValueDto(d, Sum(rows, StatMetrics.ReservationsTotal, d, d))).ToArray(),
            extras.BusySlots.Select(s => new BusySlotDto(s.StartTime, s.PercentFull)).ToArray());
    }

    private async Task<List<DailyStat>> LoadAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var (start, end) = calendar.Bounds(today);
        var now = clock.UtcNow;
        var history = await stats.ReadAsync(today.AddDays(-HistoryDays), today.AddDays(-1), cancellationToken);
        var live = await stats.ComputeDayAsync(today, start, end, cancellationToken);
        return [.. history, .. live.Select(r => new DailyStat { Date = today, Metric = r.Metric, Dimension = r.Dimension, Value = r.Value, UpdatedAtUtc = now })];
    }

    private static IEnumerable<DateOnly> Days(DateOnly today) =>
        Enumerable.Range(0, ChartDays).Select(i => today.AddDays(i + 1 - ChartDays));

    private static decimal Sum(IEnumerable<DailyStat> rows, string metric, DateOnly from, DateOnly to) =>
        rows.Where(r => r.Dimension.Length == 0 && string.Equals(r.Metric, metric, StringComparison.Ordinal) && r.Date >= from && r.Date <= to).Sum(r => r.Value);

    private static decimal Revenue(IEnumerable<DailyStat> rows, DateOnly from, DateOnly to)
    {
        var list = rows as IReadOnlyCollection<DailyStat> ?? rows.ToArray();
        return Sum(list, StatMetrics.RevenuePaid, from, to) - Sum(list, StatMetrics.RefundsAmount, from, to);
    }

    private static ReservationStatusCountDto[] ReservationStatuses(DailyStat[] rows) =>
        Enum.GetValues<ReservationStatus>()
            .Select(s => new ReservationStatusCountDto(s, Status(rows, StatMetrics.ReservationsTotal, (byte)s)))
            .ToArray();

    private static OrderStatusCountDto[] OrderStatuses(DailyStat[] rows) =>
        Enum.GetValues<OrderStatus>()
            .Select(s => new OrderStatusCountDto(s, Status(rows, StatMetrics.OrdersCreated, (byte)s)))
            .ToArray();

    private static int Status(DailyStat[] rows, string metric, byte status)
    {
        var dimension = StatMetrics.StatusPrefix + status.ToString(CultureInfo.InvariantCulture);
        return (int)rows.Where(r => string.Equals(r.Metric, metric, StringComparison.Ordinal) && string.Equals(r.Dimension, dimension, StringComparison.Ordinal)).Sum(r => r.Value);
    }

    private static NamedValueDto[] Named(IEnumerable<DailyStat> rows, string metric, string prefix, DateOnly from, DateOnly to, IReadOnlyDictionary<int, string> names, int take) =>
        rows.Where(r => string.Equals(r.Metric, metric, StringComparison.Ordinal) && r.Dimension.StartsWith(prefix, StringComparison.Ordinal) && r.Date >= from && r.Date <= to)
            .GroupBy(r => int.Parse(r.Dimension.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture))
            .Select(g => new NamedValueDto(g.Key, names.GetValueOrDefault(g.Key) ?? string.Empty, g.Sum(r => r.Value)))
            .OrderByDescending(n => n.Value)
            .Take(take)
            .ToArray();
}
