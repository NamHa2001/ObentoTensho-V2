using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/18 §4: mỗi thao tác ghi đều cần step-up (SEC-61)
[ApiController]
[Route("api/v1/admin/staff")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.StaffAndAudit)]
public sealed class AdminStaffController(StaffAdminService members) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] StaffRole? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await members.ListAsync(new StaffQuery(q, role, isActive, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await members.GetAsync(id, cancellationToken));

    [HttpPost]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> Create(CreateStaffRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await members.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> Update(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await members.UpdateAsync(id, request, cancellationToken));

    [HttpPatch("{id:guid}/status")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> Status(Guid id, StaffStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await members.SetStatusAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/resend-invite")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await members.ResendInviteAsync(id, cancellationToken));

    [HttpPost("{id:guid}/revoke-sessions")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await members.RevokeSessionsAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reset-2fa")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> ResetTwoFactor(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await members.ResetTwoFactorAsync(id, cancellationToken));
}
