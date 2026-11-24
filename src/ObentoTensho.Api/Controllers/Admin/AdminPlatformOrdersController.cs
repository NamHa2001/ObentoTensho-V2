using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.DineIn;
using ObentoTensho.Contracts.Dtos.PlatformOrders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/23: nhập lại đơn từ app giao đồ ăn. Ai phục vụ bàn, quầy thì nhập được; sửa và hủy cần quyền quản lý (orders_void).
// Món của đơn app đi chung đường bếp với đơn tại quầy (dine-in/kitchen-view, báo ra món).
[ApiController]
[Route("api/v1/admin/platform-orders")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPlatformOrdersController(PlatformOrderService platformOrders) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Open(CancellationToken cancellationToken) =>
        this.Data(await platformOrders.OpenAsync(cancellationToken));

    [HttpGet("rates")]
    [RequireCapability(StaffCapability.DineIn)]
    public IActionResult Rates() => this.Data(platformOrders.Rates());

    [HttpPost]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Create(CreatePlatformOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await platformOrders.CreateAsync(request, User.StaffId(), cancellationToken));

    [HttpPut("{id:guid}")]
    [RequireCapability(StaffCapability.OrdersVoid)]
    public async Task<IActionResult> Update(Guid id, UpdatePlatformOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await platformOrders.UpdateAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [RequireCapability(StaffCapability.OrdersVoid)]
    public async Task<IActionResult> Cancel(Guid id, DineInReasonRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await platformOrders.CancelAsync(id, request?.Reason, User.StaffId(), cancellationToken));
}
