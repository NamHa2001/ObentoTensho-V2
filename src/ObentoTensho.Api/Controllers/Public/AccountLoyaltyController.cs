using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Loyalty.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/13 §7: thành viên xem số dư, hạng, tiến độ lên hạng, lịch sử điểm
[ApiController]
[Route("api/v1/account/loyalty-points")]
[RequireCustomer]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountLoyaltyController(LoyaltyService loyalty) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        this.DataOrProblem(await loyalty.SummaryAsync(CustomerId(), cancellationToken));

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await loyalty.HistoryAsync(CustomerId(), page, pageSize, cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
