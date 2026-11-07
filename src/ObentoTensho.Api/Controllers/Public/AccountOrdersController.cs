using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/12 §8: khách đã đăng nhập xem đơn của mình
[ApiController]
[Route("api/v1/account/orders")]
[RequireCustomer]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountOrdersController(PublicOrderService orders) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await orders.ListMineAsync(CustomerId(), page, pageSize, cancellationToken));

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.GetMineAsync(CustomerId(), code, cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
