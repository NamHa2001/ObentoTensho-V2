using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Api.Realtime;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Mọi nhân viên, trên chính tài khoản của mình
[ApiController]
[Route("api/v1/admin")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireStaff]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAccountController(StaffAdminService members, StaffStepUpService stepUp, StaffPasswordService passwords, HubTicketIssuer tickets) : ControllerBase
{
    // RT-09: vé một lần để nối /hubs/v1/staff, không đặt access token lên URL
    [HttpPost("realtime/ticket")]
    public IActionResult RealtimeTicket() =>
        this.DataOrProblem(tickets.Issue(User, AuthSchemes.Staff, HubIdentity.StaffKind));

    [HttpGet("staff/me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        this.DataOrProblem(await members.MeAsync(User.StaffId(), cancellationToken));

    [HttpPost("auth/step-up")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> StepUp(StepUpRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await stepUp.VerifyAsync(User.StaffId(), request, cancellationToken));

    [HttpPost("auth/me/change-password")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await passwords.ChangeOwnPasswordAsync(User.StaffId(), request, cancellationToken));
}
