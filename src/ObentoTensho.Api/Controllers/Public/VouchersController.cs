using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/14 §6: kiểm mã ở giỏ; giới hạn tần suất riêng để chống dò mã (BR-VCH-12)
[ApiController]
[Route("api/v1/vouchers")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class VouchersController(VoucherService vouchers) : ControllerBase
{
    [HttpPost("validate")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.VoucherPolicy)]
    public async Task<IActionResult> Validate(ValidateVoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.ValidateAsync(request, User.CustomerId(), cancellationToken));
}
