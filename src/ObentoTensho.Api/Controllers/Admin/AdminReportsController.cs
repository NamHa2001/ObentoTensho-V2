using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Reports;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Reports.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/19 §2, §4: ngày lọc là ngày VN; khoảng tối đa 366 ngày (BR-RPT-30)
[ApiController]
[Route("api/v1/admin/reports")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Reports)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReportsController(ReportService reports, StatsRollupService rollup, PaymentGroupReportService paymentGroups) : ControllerBase
{
    private const string Xlsx = "xlsx";
    private const string Pdf = "pdf";

    // PERF-55: tính lại số liệu tổng hợp sau khi nhập dữ liệu cũ
    [HttpPost("recompute")]
    public async Task<IActionResult> Recompute(RecomputeStatsRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await rollup.RecomputeAsync(request, cancellationToken));

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ReportGrouping groupBy = ReportGrouping.Day,
        CancellationToken cancellationToken = default) =>
        this.DataOrProblem(await reports.RevenueAsync(from, to, groupBy, cancellationToken));

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        this.DataOrProblem(await reports.ReservationsAsync(from, to, cancellationToken));

    [HttpGet("orders")]
    public async Task<IActionResult> Orders([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int limit = 10, CancellationToken cancellationToken = default) =>
        this.DataOrProblem(await reports.OrdersAsync(from, to, limit, cancellationToken));

    [HttpGet("customers")]
    public async Task<IActionResult> Customers([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int limit = 10, CancellationToken cancellationToken = default) =>
        this.DataOrProblem(await reports.CustomersAsync(from, to, limit, cancellationToken));

    [HttpGet("top-dishes")]
    public async Task<IActionResult> TopDishes([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int limit = 10, CancellationToken cancellationToken = default) =>
        this.DataOrProblem(await reports.TopDishesAsync(from, to, limit, cancellationToken));

    // docs/srs/23 FR-PLT-03: doanh thu theo 4 nhóm thanh toán và kênh con
    [HttpGet("payment-groups")]
    public async Task<IActionResult> PaymentGroups(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ReportGrouping groupBy = ReportGrouping.Day,
        CancellationToken cancellationToken = default) =>
        this.DataOrProblem(await paymentGroups.SummaryAsync(from, to, groupBy, cancellationToken));

    [HttpGet("payment-groups/orders")]
    public async Task<IActionResult> PaymentGroupOrders(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] PaymentGroup? group = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await paymentGroups.OrdersAsync(from, to, group, page, pageSize, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result);
    }

    // FR-PLT-04: Excel hai sheet (tổng theo nhóm, chi tiết từng đơn) hoặc PDF A4 ngang
    [HttpGet("payment-groups/export")]
    public async Task<IActionResult> ExportPaymentGroups(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string format = Xlsx,
        CancellationToken cancellationToken = default)
    {
        if (Kind(format) is not { } kind)
        {
            return this.ToProblem(Core.Common.Result.Failure(Contracts.Errors.ErrorCodes.ValidationFailed));
        }

        var file = await paymentGroups.ExportAsync(from, to, kind, User.StaffId(), cancellationToken);
        return file.IsSuccess ? File(file.Value.Content, file.Value.ContentType, file.Value.FileName) : this.ToProblem(file);
    }

    [HttpGet("{report}/export")]
    public async Task<IActionResult> Export(
        string report,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string format = Xlsx,
        [FromQuery] ReportGrouping groupBy = ReportGrouping.Day,
        CancellationToken cancellationToken = default)
    {
        var kind = Kind(format);
        if (kind is null)
        {
            return this.ToProblem(Core.Common.Result.Failure(Contracts.Errors.ErrorCodes.ValidationFailed));
        }

        var file = await reports.ExportAsync(report, from, to, groupBy, kind.Value, cancellationToken);
        return file.IsSuccess ? File(file.Value.Content, file.Value.ContentType, file.Value.FileName) : this.ToProblem(file);
    }

    private static ReportFormat? Kind(string format) => format switch
    {
        Xlsx => ReportFormat.Xlsx,
        Pdf => ReportFormat.Pdf,
        _ => null,
    };
}
