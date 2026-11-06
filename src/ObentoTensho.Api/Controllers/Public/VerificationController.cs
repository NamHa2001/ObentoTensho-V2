using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Verification;
using ObentoTensho.Core.Verification.Services;

namespace ObentoTensho.Api.Controllers.Public;

// BR-AUTH-18: gửi mã xác minh dùng chung (đặt bàn khi bị yêu cầu OTP; sau này SĐT tài khoản)
[ApiController]
[Route("api/v1/otp")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VerificationController(OtpService otp) : ControllerBase
{
    [HttpPost("send")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    [ServiceFilter<TurnstileFilter>]
    public async Task<IActionResult> Send(SendOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await otp.SendAsync(request.Purpose, request.Email, request.Lang, HttpContext.GetClientInfo().IpAddress, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }
}
