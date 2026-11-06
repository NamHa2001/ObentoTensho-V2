using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/10 §8 (admin). Các bước đăng nhập chạy trước khi có phiên nên [AllowAnonymous];
// bước sau mật khẩu được bảo vệ bằng mã tạm trong body.
[ApiController]
[Route("api/v1/admin/auth")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAuthController(
    StaffLoginService login,
    StaffTwoFactorService twoFactor,
    StaffPasswordService passwords,
    StaffSessionService sessions) : ControllerBase
{
    private const string Cookie = RefreshTokenCookie.AdminName;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        SignIn(await login.LoginAsync(request, HttpContext.GetClientInfo(), cancellationToken));

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> VerifyTwoFactor(TwoFactorVerifyRequest request, CancellationToken cancellationToken) =>
        SignIn(await twoFactor.VerifyAsync(request, HttpContext.GetClientInfo(), cancellationToken));

    [HttpPost("2fa/setup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> BeginTwoFactorSetup(ChallengeTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await twoFactor.BeginSetupAsync(request.ChallengeToken, cancellationToken);
        return result.IsSuccess ? Ok(new ApiResponse<TotpSetupResponse>(result.Value)) : this.ToProblem(result);
    }

    [HttpPost("2fa/setup/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ConfirmTwoFactorSetup(TotpSetupConfirmRequest request, CancellationToken cancellationToken)
    {
        var result = await twoFactor.ConfirmSetupAsync(request, HttpContext.GetClientInfo(), cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result);
        }

        var session = WriteSession(result.Value.Session);
        return Ok(new ApiResponse<TotpSetupConfirmResponse>(new TotpSetupConfirmResponse(session, result.Value.RecoveryCodes)));
    }

    [HttpPost("change-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ChangePassword(StaffChangePasswordRequest request, CancellationToken cancellationToken) =>
        SignIn(await passwords.ChangePasswordAsync(request, HttpContext.GetClientInfo(), cancellationToken));

    [HttpPost("accept-invite")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> AcceptInvite(AcceptInviteRequest request, CancellationToken cancellationToken)
    {
        var result = await passwords.AcceptInviteAsync(request, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ServiceFilter<AdminOriginFilter>]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var result = await sessions.RefreshAsync(RefreshTokenCookie.Read(Request, Cookie), HttpContext.GetClientInfo(), cancellationToken);
        if (!result.IsSuccess)
        {
            RefreshTokenCookie.Clear(Response, Cookie);
            return this.ToProblem(result);
        }

        return Ok(new ApiResponse<AccessTokenResponse>(WriteSession(result.Value)));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ServiceFilter<AdminOriginFilter>]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(RefreshTokenCookie.Read(Request, Cookie), cancellationToken);
        RefreshTokenCookie.Clear(Response, Cookie);
        return NoContent();
    }

    private IActionResult SignIn(Result<StaffSignInResult> result)
    {
        if (!result.IsSuccess)
        {
            return this.ToProblem(result);
        }

        var (session, challenge) = result.Value;
        var response = session is not null
            ? new StaffLoginResponse(WriteSession(session), null)
            : new StaffLoginResponse(null, challenge is null ? null : new StaffChallengeResponse(challenge.Kind, challenge.Token, challenge.ExpiresAtUtc));
        return Ok(new ApiResponse<StaffLoginResponse>(response));
    }

    private AccessTokenResponse WriteSession(AuthSession session)
    {
        RefreshTokenCookie.Write(Response, Cookie, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return new AccessTokenResponse(session.AccessToken.Value, session.AccessToken.ExpiresAtUtc);
    }
}
