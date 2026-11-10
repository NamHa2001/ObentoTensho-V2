using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Vouchers;
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

    [HttpGet("rewards")]
    public async Task<IActionResult> Rewards(CancellationToken cancellationToken) =>
        this.Data(await vouchers.ListRewardsAsync(cancellationToken));

    [HttpPost("redeem-points")]
    public async Task<IActionResult> Redeem(RedeemPointsRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await vouchers.RedeemPointsAsync(CustomerId(), request.RewardId, cancellationToken));

    // Hộp chọn mã ở bước thanh toán
    [HttpPost("voucher-options")]
    public async Task<IActionResult> Options(VoucherOptionsRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.OptionsAsync(request, CustomerId(), cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
