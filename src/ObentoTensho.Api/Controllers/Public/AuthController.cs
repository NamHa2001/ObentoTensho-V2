using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await registration.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sessions.LoginAsync(request, HttpContext.GetClientInfo(), cancellationToken);
        return StartSession(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ServiceFilter<TrustedOriginFilter>]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var result = await sessions.RefreshAsync(RefreshTokenCookie.Read(Request), HttpContext.GetClientInfo(), cancellationToken);
        if (!result.IsSuccess)
        {
            RefreshTokenCookie.Clear(Response);
        }

        return StartSession(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ServiceFilter<TrustedOriginFilter>]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Clear(Response);
        return NoContent();
    }

    private IActionResult StartSession(Result<AuthSession> result)
    {
        if (!result.IsSuccess)
        {
            return this.ToProblem(result);
        }

        var session = result.Value;
        RefreshTokenCookie.Write(Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc);
        return Ok(new ApiResponse<AccessTokenResponse>(
            new AccessTokenResponse(session.AccessToken.Value, session.AccessToken.ExpiresAtUtc)));
    }
}
