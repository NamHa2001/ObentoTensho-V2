using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/13 §7: voucher của tôi, đổi điểm lấy voucher
[ApiController]
[Route("api/v1/account")]
[RequireCustomer]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountVouchersController(VoucherService vouchers) : ControllerBase
{
    [HttpGet("vouchers")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await vouchers.ListMineAsync(CustomerId(), cancellationToken));

    [HttpPost("redeem-points")]
    public async Task<IActionResult> Redeem(CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await vouchers.RedeemPointsAsync(CustomerId(), cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
