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
public sealed class AdminAccountController(
    StaffAdminService members,
    StaffStepUpService stepUp,
    StaffPasswordService passwords,
    StaffOwnTwoFactorService twoFactor,
    HubTicketIssuer tickets) : ControllerBase
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

    // docs/srs/10 §4: tự bật 2FA (vai trò không bắt buộc), tạo lại mã khôi phục, tắt 2FA — hai việc sau cần step-up (SEC-61)
    [HttpPost("auth/me/2fa/setup")]
    public async Task<IActionResult> BeginTwoFactor(CancellationToken cancellationToken) =>
        this.DataOrProblem(await twoFactor.BeginSetupAsync(User.StaffId(), cancellationToken));

    [HttpPost("auth/me/2fa/setup/confirm")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ConfirmTwoFactor(TotpCodeRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await twoFactor.ConfirmSetupAsync(User.StaffId(), request, cancellationToken));

    [HttpPost("auth/me/2fa/recovery-codes")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> RegenerateRecoveryCodes(CancellationToken cancellationToken) =>
        this.DataOrProblem(await twoFactor.RegenerateRecoveryCodesAsync(User.StaffId(), cancellationToken));

    [HttpPost("auth/me/2fa/disable")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> DisableTwoFactor(CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await twoFactor.DisableAsync(User.StaffId(), cancellationToken));

    [HttpPost("auth/me/change-password")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await passwords.ChangeOwnPasswordAsync(User.StaffId(), request, cancellationToken));
}
