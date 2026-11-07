using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Loyalty;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Loyalty.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/13 §7: quản lý xem điểm của khách, điều chỉnh có lý do, duyệt bốn mắt khi vượt ngưỡng (BR-LOY-21)
[ApiController]
[Route("api/v1/admin")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Customers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminLoyaltyController(LoyaltyService loyalty) : ControllerBase
{
    [HttpGet("customers/{userId:guid}/loyalty-points")]
    public async Task<IActionResult> Summary(Guid userId, CancellationToken cancellationToken) =>
        this.DataOrProblem(await loyalty.SummaryAsync(userId, cancellationToken));

    [HttpGet("customers/{userId:guid}/loyalty-points/history")]
    public async Task<IActionResult> History(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await loyalty.HistoryAsync(userId, page, pageSize, cancellationToken));

    [HttpPatch("customers/{userId:guid}/loyalty-points")]
    public async Task<IActionResult> Adjust(Guid userId, AdjustPointsRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await loyalty.AdjustAsync(userId, request, User.StaffId(), cancellationToken));

    [HttpGet("loyalty/adjustments")]
    public async Task<IActionResult> Adjustments([FromQuery] AdjustmentStatus? status, CancellationToken cancellationToken) =>
        this.Data(await loyalty.ListAdjustmentsAsync(status, cancellationToken));

    [HttpPost("loyalty/adjustments/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, ReviewAdjustmentRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await loyalty.ApproveAsync(id, User.StaffId(), request.Note, cancellationToken));

    [HttpPost("loyalty/adjustments/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, ReviewAdjustmentRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await loyalty.RejectAsync(id, User.StaffId(), request.Note, cancellationToken));
}
