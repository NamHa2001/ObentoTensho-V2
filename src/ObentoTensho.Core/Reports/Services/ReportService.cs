using System.Globalization;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Reports.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reports.Services;

// FR-RPT-02: mọi báo cáo dùng chung DailyStats với bảng tin (BR-RPT-31); ngày hiện tại tính trực tiếp
public sealed class ReportService(
    IStatsRepository stats,
    BusinessCalendar calendar,
    ITableSpreadsheet spreadsheet,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int MaxRangeDays = 366;
    public const int MaxLimit = 50;
    public const int RetentionDays = 90;
    public const string RevenueDefinition = "Doanh thu = tiền thu của đơn theo ngày thanh toán, trừ hoàn tiền theo ngày hoàn.";
    public const string RetentionDefinition = "Retention = % khách có từ 2 đơn không hủy trong 90 ngày, trên số khách có đơn trong 90 ngày đó.";

    private static readonly string[] RevenueColumns = ["Kỳ", "Tiền thu", "Hoàn tiền", "Doanh thu"];
    private static readonly string[] SummaryColumns = ["Chỉ số", "Giá trị"];
    private static readonly string[] CustomerColumns = ["Khách", "Số đơn", "Đã chi"];
    private static readonly string[] DishColumns = ["Món", "Số lượng", "Doanh thu"];

    public static string? CheckRange(DateOnly from, DateOnly to) =>
        from > to || to.DayNumber - from.DayNumber >= MaxRangeDays ? ErrorCodes.ValidationFailed : null;

    public async Task<Result<RevenueReportDto>> RevenueAsync(DateOnly from, DateOnly to, ReportGrouping grouping, CancellationToken cancellationToken)
    {
        if (CheckRange(from, to) is { } error)
        {
            return Result.Failure<RevenueReportDto>(error);
        }

        var length = to.DayNumber - from.DayNumber + 1;
        var rows = await LoadAsync(from.AddDays(-length), to, cancellationToken);
        var current = rows.Where(r => r.Date >= from).ToArray();
        var previous = rows.Where(r => r.Date < from).ToArray();
        var paid = Total(current, StatMetrics.RevenuePaid);
        var refunds = Total(current, StatMetrics.RefundsAmount);
        var previousNet = Total(previous, StatMetrics.RevenuePaid) - Total(previous, StatMetrics.RefundsAmount);
        var series = current
            .Where(r => r.Dimension.Length == 0 && (Is(r, StatMetrics.RevenuePaid) || Is(r, StatMetrics.RefundsAmount)))
            .GroupBy(r => Period(r.Date, grouping), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var periodPaid = g.Where(r => Is(r, StatMetrics.RevenuePaid)).Sum(r => r.Value);
                var periodRefunds = g.Where(r => Is(r, StatMetrics.RefundsAmount)).Sum(r => r.Value);
                return new RevenuePointDto(g.Key, periodPaid, periodRefunds, periodPaid - periodRefunds);
            })
            .ToArray();
        var names = await stats.LoadNamesAsync(cancellationToken);
        return Result.Success(new RevenueReportDto(paid, refunds, paid - refunds, previousNet, series, Named(current, StatMetrics.RevenuePaid, StatMetrics.CategoryPrefix, names.Categories), RevenueDefinition));
    }

    public async Task<Result<ReservationReportDto>> ReservationsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (CheckRange(from, to) is { } error)
        {
            return Result.Failure<ReservationReportDto>(error);
        }

        var rows = await LoadAsync(from, to, cancellationToken);
        var total = (int)Total(rows, StatMetrics.ReservationsTotal);
        var arrived = (int)Total(rows, StatMetrics.ReservationsArrived);
        var cancelled = (int)Total(rows, StatMetrics.ReservationsCancelled);
        var noShow = (int)Total(rows, StatMetrics.ReservationsNoShow);
        var names = await stats.LoadNamesAsync(cancellationToken);
        return Result.Success(new ReservationReportDto(
            total,
            arrived,
            cancelled,
            noShow,
            Percent(arrived, total),
            Percent(cancelled, total),
            Percent(noShow, total),
            Hours(rows, StatMetrics.ReservationsTotal),
            Named(rows, StatMetrics.ReservationsTotal, StatMetrics.AreaPrefix, names.Areas)));
    }

    public async Task<Result<OrderReportDto>> OrdersAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
    {
        if (CheckRange(from, to) is { } error)
        {
            return Result.Failure<OrderReportDto>(error);
        }

        var rows = await LoadAsync(from, to, cancellationToken);
        var names = await stats.LoadNamesAsync(cancellationToken);
        var paidOrders = Total(rows, StatMetrics.OrdersPaid);
        return Result.Success(new OrderReportDto(
            (int)Total(rows, StatMetrics.OrdersCreated),
            (int)Total(rows, StatMetrics.OrdersCompleted),
            (int)Total(rows, StatMetrics.OrdersCancelled),
            paidOrders == 0 ? 0 : Math.Round(Total(rows, StatMetrics.RevenuePaid) / paidOrders, 0, MidpointRounding.AwayFromZero),
            Hours(rows, StatMetrics.OrdersCreated),
            TopDishes(rows, names, limit)));
    }

    public async Task<Result<IReadOnlyList<TopDishDto>>> TopDishesAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
    {
        if (CheckRange(from, to) is { } error)
        {
            return Result.Failure<IReadOnlyList<TopDishDto>>(error);
        }

        var rows = await LoadAsync(from, to, cancellationToken);
        return Result.Success<IReadOnlyList<TopDishDto>>(TopDishes(rows, await stats.LoadNamesAsync(cancellationToken), limit));
    }

    public async Task<Result<CustomerReportDto>> CustomersAsync(DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
    {
        if (CheckRange(from, to) is { } error)
        {
            return Result.Failure<CustomerReportDto>(error);
        }

        var rows = await LoadAsync(from, to, cancellationToken);
        var (start, _) = calendar.Bounds(from);
        var (_, end) = calendar.Bounds(to);
        var customers = await stats.LoadCustomerStatsAsync(start, end, end.AddDays(-RetentionDays), Math.Clamp(limit, 1, MaxLimit), cancellationToken);
        var daily = rows.Where(r => r.Dimension.Length == 0 && Is(r, StatMetrics.NewCustomers)).OrderBy(r => r.Date).Select(r => new DayValueDto(r.Date, r.Value)).ToArray();
        return Result.Success(new CustomerReportDto(
            daily,
            (int)daily.Sum(d => d.Value),
            customers.Tiers,
            Percent(customers.ReturningCustomers, customers.ActiveCustomers),
            customers.TopCustomers,
            RetentionDefinition));
    }

    // BR-RPT-12: khoảng tối đa 366 ngày nên luôn dưới 10.000 dòng, xuất ngay trong yêu cầu
    public async Task<Result<ReportFile>> ExportAsync(string report, DateOnly from, DateOnly to, ReportGrouping grouping, CancellationToken cancellationToken)
    {
        var table = report switch
        {
            "revenue" => await RevenueTableAsync(from, to, grouping, cancellationToken),
            "reservations" => await ReservationTableAsync(from, to, cancellationToken),
            "orders" => await OrderTableAsync(from, to, cancellationToken),
            "customers" => await CustomerTableAsync(from, to, cancellationToken),
            "top-dishes" => await DishTableAsync(from, to, cancellationToken),
            _ => Result.Failure<(string[] Columns, string[][] Rows)>(ErrorCodes.NotFound),
        };
        if (!table.IsSuccess)
        {
            return Result.Failure<ReportFile>(table.ErrorCode ?? ErrorCodes.ValidationFailed);
        }

        audit.Record(new AuditEntry("reports.Report.exported", "Report", report, After: new { from, to, Rows = table.Value.Rows.Length }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var name = string.Create(CultureInfo.InvariantCulture, $"bao-cao-{report}-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
        return Result.Success(new ReportFile(name, spreadsheet.Write("Báo cáo", table.Value.Columns, table.Value.Rows)));
    }

    private async Task<Result<(string[] Columns, string[][] Rows)>> RevenueTableAsync(DateOnly from, DateOnly to, ReportGrouping grouping, CancellationToken cancellationToken)
    {
        var report = await RevenueAsync(from, to, grouping, cancellationToken);
        return report.IsSuccess
            ? Result.Success((RevenueColumns, report.Value.Series.Select(p => new[] { p.Period, Number(p.Paid), Number(p.Refunds), Number(p.Net) }).ToArray()))
            : Result.Failure<(string[], string[][])>(report.ErrorCode ?? ErrorCodes.ValidationFailed);
    }

    private async Task<Result<(string[] Columns, string[][] Rows)>> ReservationTableAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var report = await ReservationsAsync(from, to, cancellationToken);
        return report.IsSuccess
            ? Result.Success((SummaryColumns, new[]
            {
                new[] { "Tổng lượt đặt", Number(report.Value.Total) },
                ["Đã đến", Number(report.Value.Arrived)],
                ["Đã hủy", Number(report.Value.Cancelled)],
                ["Không đến", Number(report.Value.NoShow)],
            }))
            : Result.Failure<(string[], string[][])>(report.ErrorCode ?? ErrorCodes.ValidationFailed);
    }

    private async Task<Result<(string[] Columns, string[][] Rows)>> OrderTableAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var report = await OrdersAsync(from, to, MaxLimit, cancellationToken);
        return report.IsSuccess
            ? Result.Success((SummaryColumns, new[]
            {
                new[] { "Đơn đặt", Number(report.Value.Created) },
                ["Đơn hoàn thành", Number(report.Value.Completed)],
                ["Đơn hủy", Number(report.Value.Cancelled)],
                ["Giá trị trung bình", Number(report.Value.AverageValue)],
            }))
            : Result.Failure<(string[], string[][])>(report.ErrorCode ?? ErrorCodes.ValidationFailed);
    }

    private async Task<Result<(string[] Columns, string[][] Rows)>> CustomerTableAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var report = await CustomersAsync(from, to, MaxLimit, cancellationToken);
        return report.IsSuccess
            ? Result.Success((CustomerColumns, report.Value.TopCustomers.Select(c => new[] { c.FullName, Number(c.Orders), Number(c.Spent) }).ToArray()))
            : Result.Failure<(string[], string[][])>(report.ErrorCode ?? ErrorCodes.ValidationFailed);
    }

    private async Task<Result<(string[] Columns, string[][] Rows)>> DishTableAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var report = await TopDishesAsync(from, to, MaxLimit, cancellationToken);
        return report.IsSuccess
            ? Result.Success((DishColumns, report.Value.Select(d => new[] { d.Name, Number(d.Quantity), Number(d.Revenue) }).ToArray()))
            : Result.Failure<(string[], string[][])>(report.ErrorCode ?? ErrorCodes.ValidationFailed);
    }

    private async Task<List<DailyStat>> LoadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var today = calendar.Today;
        var rows = (await stats.ReadAsync(from, to, cancellationToken)).Where(r => r.Date != today).ToList();
        if (today >= from && today <= to)
        {
            var (start, end) = calendar.Bounds(today);
            var now = clock.UtcNow;
            rows.AddRange((await stats.ComputeDayAsync(today, start, end, cancellationToken))
                .Select(r => new DailyStat { Date = today, Metric = r.Metric, Dimension = r.Dimension, Value = r.Value, UpdatedAtUtc = now }));
        }

        return rows;
    }

    private static bool Is(DailyStat row, string metric) => string.Equals(row.Metric, metric, StringComparison.Ordinal);

    private static decimal Total(IEnumerable<DailyStat> rows, string metric) =>
        rows.Where(r => r.Dimension.Length == 0 && Is(r, metric)).Sum(r => r.Value);

    private static decimal Percent(int part, int total) =>
        total == 0 ? 0 : Math.Round(part * 100m / total, 1, MidpointRounding.AwayFromZero);

    private static int Id(string dimension, string prefix) =>
        int.Parse(dimension.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static NamedValueDto[] Named(IEnumerable<DailyStat> rows, string metric, string prefix, IReadOnlyDictionary<int, string> names) =>
        rows.Where(r => Is(r, metric) && r.Dimension.StartsWith(prefix, StringComparison.Ordinal))
            .GroupBy(r => Id(r.Dimension, prefix))
            .Select(g => new NamedValueDto(g.Key, names.GetValueOrDefault(g.Key) ?? string.Empty, g.Sum(r => r.Value)))
            .OrderByDescending(n => n.Value)
            .ToArray();

    private static NamedValueDto[] Hours(IEnumerable<DailyStat> rows, string metric) =>
        rows.Where(r => Is(r, metric) && r.Dimension.StartsWith(StatMetrics.HourPrefix, StringComparison.Ordinal))
            .GroupBy(r => Id(r.Dimension, StatMetrics.HourPrefix))
            .OrderBy(g => g.Key)
            .Select(g => new NamedValueDto(g.Key, string.Create(CultureInfo.InvariantCulture, $"{g.Key:00}:00"), g.Sum(r => r.Value)))
            .ToArray();

    private static TopDishDto[] TopDishes(IEnumerable<DailyStat> rows, ReportNames names, int limit)
    {
        var list = rows as IReadOnlyCollection<DailyStat> ?? rows.ToArray();
        var revenue = Named(list, StatMetrics.ItemRevenue, StatMetrics.ItemPrefix, names.Items).ToDictionary(n => n.Id, n => n.Value);
        return Named(list, StatMetrics.ItemQuantity, StatMetrics.ItemPrefix, names.Items)
            .Take(Math.Clamp(limit, 1, MaxLimit))
            .Select(n => new TopDishDto(n.Id, n.Name, n.Value, revenue.GetValueOrDefault(n.Id)))
            .ToArray();
    }

    private static string Period(DateOnly date, ReportGrouping grouping) => grouping switch
    {
        ReportGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ReportGrouping.Month => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        ReportGrouping.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
