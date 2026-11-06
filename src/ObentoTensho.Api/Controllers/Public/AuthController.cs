using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Common;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/10 §8. Các endpoint này dùng trước khi có phiên nên đều [AllowAnonymous].
[ApiController]
[Route("api/v1/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(CustomerRegistrationService registration, CustomerSessionService sessions) : ControllerBase
{
    private const string Cookie = RefreshTokenCookie.CustomerName;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await registration.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sessions.LoginAsync(request, HttpContext.GetClientInfo(), cancellationToken);
        return StartSession(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ServiceFilter<CustomerOriginFilter>]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var result = await sessions.RefreshAsync(RefreshTokenCookie.Read(Request, Cookie), HttpContext.GetClientInfo(), cancellationToken);
        if (!result.IsSuccess)
        {
            RefreshTokenCookie.Clear(Response, Cookie);
        }

        return StartSession(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ServiceFilter<CustomerOriginFilter>]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(RefreshTokenCookie.Read(Request, Cookie), cancellationToken);
        RefreshTokenCookie.Clear(Response, Cookie);
        return NoContent();
    }

    private IActionResult StartSession(Result<AuthSession> result)
    {
        if (!result.IsSuccess)
        {
            return this.ToProblem(result);
        }

        var session = result.Value;
        RefreshTokenCookie.Write(Response, Cookie, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(new ApiResponse<AccessTokenResponse>(
            new AccessTokenResponse(session.AccessToken.Value, session.AccessToken.ExpiresAtUtc)));
    }
}
