using System.Globalization;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Reports.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Reports.Services;

// docs/srs/23 FR-PLT-03, FR-PLT-04: doanh thu theo 4 nhóm thanh toán và kênh con, đọc thẳng bản chụp trên đơn (BR-PLT-04)
// nên đổi tỉ lệ ở Cài đặt không làm đổi số cũ. Đơn tính theo ngày hoàn thành (BR-PLT-09); đơn hủy không tính.
public sealed class PaymentGroupReportService(
    IPaymentGroupReportRepository repository,
    IUserRepository users,
    BusinessCalendar calendar,
    IReportWorkbook workbook,
    IReportPdf pdf,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public const int MaxPageSize = 100;
    public const string Definition = "Doanh thu ròng = giá gốc − giảm giá. Thực nhận = doanh thu ròng − khấu trừ − thuế GTGT − thuế TNCN, theo tỉ lệ đã chốt trên từng đơn lúc hoàn thành.";
    private const string Title = "Báo cáo doanh thu theo nhóm thanh toán";

    // BR-PLT-01: thứ tự cố định của nhóm và kênh con; kênh không có đơn vẫn hiện số 0
    private static readonly (PaymentGroup Group, (PaymentMethod? Method, Platform? Platform)[] Channels)[] Layout =
    [
        (PaymentGroup.InStore, [(PaymentMethod.Cash, null), (PaymentMethod.BankTransfer, null)]),
        (PaymentGroup.Tensho, [(null, Platform.Grab), (null, Platform.Shopee)]),
        (PaymentGroup.Hima, [(null, Platform.Grab), (null, Platform.Shopee), (null, Platform.Be)]),
        (PaymentGroup.Xanh, [(null, Platform.Xanh)]),
    ];

    private static readonly ReportColumn[] SummaryColumns =
    [
        new("Nhóm", ReportCellKind.Text), new("Kênh con", ReportCellKind.Text), new("Số đơn", ReportCellKind.Count),
        new("Giá gốc", ReportCellKind.Money), new("Giảm giá", ReportCellKind.Money), new("Doanh thu ròng", ReportCellKind.Money),
        new("Khấu trừ", ReportCellKind.Money), new("Thuế GTGT", ReportCellKind.Money), new("Thuế TNCN", ReportCellKind.Money),
        new("Thực nhận", ReportCellKind.Money),
    ];

    private static readonly ReportColumn[] DetailColumns =
    [
        new("Thời gian", ReportCellKind.DateTime), new("Mã đơn quán", ReportCellKind.Text), new("Mã đơn app", ReportCellKind.Text),
        new("Nhóm", ReportCellKind.Text), new("Kênh con", ReportCellKind.Text), new("Trạng thái", ReportCellKind.Text),
        new("Giá gốc", ReportCellKind.Money), new("% giảm", ReportCellKind.Percent), new("Giảm giá", ReportCellKind.Money),
        new("Doanh thu ròng", ReportCellKind.Money), new("Khấu trừ", ReportCellKind.Money), new("Thuế GTGT", ReportCellKind.Money),
        new("Thuế TNCN", ReportCellKind.Money), new("Thực nhận", ReportCellKind.Money),
    ];

    public async Task<Result<PaymentGroupReportDto>> SummaryAsync(DateOnly from, DateOnly to, ReportGrouping grouping, CancellationToken cancellationToken)
    {
        if (ReportService.CheckRange(from, to) is { } error)
        {
            return Result.Failure<PaymentGroupReportDto>(error);
        }

        return Result.Success(Summary(await LoadAsync(from, to, cancellationToken), grouping));
    }

    private PaymentGroupReportDto Summary(IReadOnlyList<PaymentGroupOrderRow> rows, ReportGrouping grouping)
    {
        var completed = rows.Where(r => r.Status == OrderStatus.Completed).ToArray();
        var groups = Layout
            .Select(g =>
            {
                var inGroup = completed.Where(r => r.Group == g.Group).ToArray();
                var channels = g.Channels
                    .Select(c => new PaymentChannelRowDto(c.Method, c.Platform, Sum(inGroup.Where(r => c.Platform is { } p ? r.Platform == p : r.PaymentMethod == c.Method))))
                    .ToArray();
                return new PaymentGroupRowDto(g.Group, Sum(inGroup), channels);
            })
            .ToArray();
        var series = completed
            .GroupBy(r => (Period: Period(calendar.DayOf(r.At), grouping), r.Group))
            .OrderBy(g => g.Key.Period, StringComparer.Ordinal).ThenBy(g => g.Key.Group)
            .Select(g => new PaymentGroupPeriodDto(g.Key.Period, g.Key.Group, g.Sum(r => r.NetRevenue ?? 0), g.Sum(r => r.ReceivedAmount ?? 0)))
            .ToArray();
        return new PaymentGroupReportDto(groups, Sum(completed), series, Definition);
    }

    public async Task<Result<PagedResponse<PaymentGroupOrderDto>>> OrdersAsync(DateOnly from, DateOnly to, PaymentGroup? group, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (ReportService.CheckRange(from, to) is { } error)
        {
            return Result.Failure<PagedResponse<PaymentGroupOrderDto>>(error);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var (start, _) = calendar.Bounds(from);
        var (_, end) = calendar.Bounds(to);
        var (items, total) = await repository.PageAsync(start, end, group, page, pageSize, cancellationToken);
        return Result.Success(new PagedResponse<PaymentGroupOrderDto>(items.Select(ToDto).ToArray(), new PageMeta(page, pageSize, total)));
    }

    // BR-RPT-12: khoảng tối đa 366 ngày, xuất ngay trong yêu cầu; sheet 1 tổng theo nhóm, sheet 2 chi tiết từng đơn
    public async Task<Result<ReportFile>> ExportAsync(DateOnly from, DateOnly to, ReportFormat format, Guid staffId, CancellationToken cancellationToken)
    {
        if (ReportService.CheckRange(from, to) is { } error)
        {
            return Result.Failure<ReportFile>(error);
        }

        var rows = await LoadAsync(from, to, cancellationToken);
        var exporter = (await users.FindByIdAsync(staffId, cancellationToken))?.FullName ?? string.Empty;
        var exportedAt = clock.ToLocal(clock.UtcNow);
        var range = string.Create(CultureInfo.InvariantCulture, $"Từ {from:dd/MM/yyyy} đến {to:dd/MM/yyyy}");
        var tables = new[] { SummaryTable(Summary(rows, ReportGrouping.Day)), DetailTable(rows) };

        audit.Record(new AuditEntry("reports.Report.exported", "Report", "payment-groups", After: new { from, to, Rows = rows.Count, Format = format }));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var name = string.Create(CultureInfo.InvariantCulture, $"bao-cao-nhom-thanh-toan-{from:yyyyMMdd}-{to:yyyyMMdd}");
        if (format == ReportFormat.Pdf)
        {
            var subtitle = string.Create(CultureInfo.InvariantCulture, $"{range} · xuất lúc {exportedAt:HH:mm dd/MM/yyyy} · người xuất {exporter}");
            return Result.Success(new ReportFile(name + ".pdf", "application/pdf", pdf.WriteTables(Title, subtitle, tables)));
        }

        string[] heading =
        [
            "Obento Tensho",
            Title,
            range,
            string.Create(CultureInfo.InvariantCulture, $"Xuất lúc {exportedAt:HH:mm dd/MM/yyyy}"),
            $"Người xuất: {exporter}",
        ];
        return Result.Success(new ReportFile(name + ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", workbook.Write(heading, tables)));
    }

    public static string GroupName(PaymentGroup group) => group switch
    {
        PaymentGroup.Tensho => "Tensho",
        PaymentGroup.Hima => "Hima",
        PaymentGroup.Xanh => "Xanh",
        _ => "Tại quán",
    };

    public static string ChannelName(PaymentMethod method, Platform? platform) => platform switch
    {
        Platform.Grab => "Grab",
        Platform.Shopee => "Shopee",
        Platform.Be => "Be",
        Platform.Xanh => "Xanh",
        _ => method == PaymentMethod.BankTransfer ? "Chuyển khoản" : "Tiền mặt",
    };

    private async Task<IReadOnlyList<PaymentGroupOrderRow>> LoadAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (start, _) = calendar.Bounds(from);
        var (_, end) = calendar.Bounds(to);
        return await repository.ListAsync(start, end, cancellationToken);
    }

    private static PaymentAmountsDto Sum(IEnumerable<PaymentGroupOrderRow> rows)
    {
        var list = rows as IReadOnlyCollection<PaymentGroupOrderRow> ?? rows.ToArray();
        return new PaymentAmountsDto(
            list.Count,
            list.Sum(r => r.SubTotal),
            list.Sum(r => r.Discount),
            list.Sum(r => r.NetRevenue ?? 0),
            list.Sum(r => r.DeductionAmount ?? 0),
            list.Sum(r => r.VatAmount ?? 0),
            list.Sum(r => r.PitAmount ?? 0),
            list.Sum(r => r.ReceivedAmount ?? 0));
    }

    private static PaymentGroupOrderDto ToDto(PaymentGroupOrderRow r)
    {
        var counted = r.Status == OrderStatus.Completed;
        return new PaymentGroupOrderDto(
            r.Id, r.Code, r.At, r.Status, r.Group, r.PaymentMethod, r.Platform, r.ExternalOrderCode, r.SubTotal, r.DiscountPercent, r.Discount,
            counted ? r.NetRevenue ?? 0 : 0,
            counted ? r.DeductionAmount ?? 0 : 0,
            counted ? r.VatAmount ?? 0 : 0,
            counted ? r.PitAmount ?? 0 : 0,
            counted ? r.ReceivedAmount ?? 0 : 0);
    }

    private static object?[] Amounts(PaymentAmountsDto a) =>
        [a.Orders, a.SubTotal, a.Discount, a.NetRevenue, a.DeductionAmount, a.VatAmount, a.PitAmount, a.ReceivedAmount];

    private static ReportTable SummaryTable(PaymentGroupReportDto report)
    {
        var rows = new List<IReadOnlyList<object?>>();
        foreach (var group in report.Groups)
        {
            rows.Add([GroupName(group.Group), "Cả nhóm", .. Amounts(group.Amounts)]);
            if (group.Group != PaymentGroup.Xanh)
            {
                rows.AddRange(group.Channels.Select(c => (IReadOnlyList<object?>)[GroupName(group.Group), ChannelName(c.PaymentMethod ?? PaymentMethod.Cash, c.Platform), .. Amounts(c.Amounts)]));
            }
        }

        return new ReportTable("Tổng theo nhóm", SummaryColumns, rows, ["Tổng cộng", string.Empty, .. Amounts(report.Total)]);
    }

    private ReportTable DetailTable(IReadOnlyList<PaymentGroupOrderRow> rows)
    {
        var details = rows.OrderBy(r => r.At).Select(ToDto).ToArray();
        var t = Sum(rows.Where(r => r.Status == OrderStatus.Completed));
        return new ReportTable(
            "Chi tiết từng đơn",
            DetailColumns,
            details.Select(d => (IReadOnlyList<object?>)
            [
                clock.ToLocal(d.AtUtc), d.Code, d.ExternalOrderCode ?? string.Empty, GroupName(d.Group), ChannelName(d.PaymentMethod, d.Platform),
                d.Status == OrderStatus.Completed ? "Hoàn thành" : "Đã hủy",
                d.SubTotal, d.DiscountPercent, d.Discount, d.NetRevenue, d.DeductionAmount, d.VatAmount, d.PitAmount, d.ReceivedAmount,
            ]).ToArray(),
            ["Tổng cộng (đơn hoàn thành)", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, t.SubTotal, null, t.Discount, t.NetRevenue, t.DeductionAmount, t.VatAmount, t.PitAmount, t.ReceivedAmount]);
    }

    private static string Period(DateOnly date, ReportGrouping grouping) => grouping switch
    {
        ReportGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ReportGrouping.Month => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        ReportGrouping.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };
}
