using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Mọi nhân viên, trên chính tài khoản của mình
[ApiController]
[Route("api/v1/admin")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireStaff]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAccountController(StaffAdminService members, StaffStepUpService stepUp, StaffPasswordService passwords) : ControllerBase
{
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
