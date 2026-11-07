using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/reviews")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Feedback)]
public sealed class AdminReviewsController(ReviewAdminService reviews) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ReviewStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await reviews.ListAsync(status, page, pageSize, cancellationToken));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await reviews.ApproveAsync(id, this.IfMatch(), User.StaffId(), cancellationToken));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await reviews.RejectAsync(id, this.IfMatch(), User.StaffId(), cancellationToken));

    [HttpPost("{id:int}/reply")]
    public async Task<IActionResult> Reply(int id, ReviewReplyRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await reviews.ReplyAsync(id, request, this.IfMatch(), User.StaffId(), cancellationToken));
}
