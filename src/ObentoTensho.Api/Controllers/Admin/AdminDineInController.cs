using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.DineIn;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Gọi món tại bàn (chủ quán 2026-11-13): lễ tân, thu ngân, quản lý phục vụ bàn; bếp xem phiếu và báo ra món;
// ai phục vụ bàn cũng thanh toán được; hủy món đã gửi bếp chỉ quản lý.
[ApiController]
[Route("api/v1/admin/dine-in")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDineInController(DineInService dineIn) : ControllerBase
{
    [HttpGet("tables")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Tables(CancellationToken cancellationToken) =>
        this.Data(await dineIn.TablesAsync(cancellationToken));

    [HttpPost("tables/{tableId:int}/clean")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> MarkClean(int tableId, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await dineIn.MarkCleanAsync(tableId, cancellationToken));

    [HttpGet("kitchen-view")]
    [RequireCapability(StaffCapability.OrdersView)]
    public async Task<IActionResult> Kitchen(CancellationToken cancellationToken) =>
        this.Data(await dineIn.KitchenAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.GetAsync(id, cancellationToken));

    [HttpPost]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Open(OpenTableRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.OpenAsync(request, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/items")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> AddItems(Guid id, AddDineInItemsRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.AddItemsAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/items/{itemId:long}/void")]
    [RequireCapability(StaffCapability.OrdersVoid)]
    public async Task<IActionResult> VoidItem(Guid id, long itemId, DineInReasonRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.VoidItemAsync(id, itemId, request, User.StaffId(), cancellationToken));

    // Bếp báo ra món
    [HttpPost("{id:guid}/items/{itemId:long}/served")]
    [RequireCapability(StaffCapability.OrdersView)]
    public async Task<IActionResult> Serve(Guid id, long itemId, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.ServeItemAsync(id, itemId, cancellationToken));

    // Nhân viên phục vụ đánh dấu đã bưng ra (lễ tân không có quyền màn hình bếp)
    [HttpPost("{id:guid}/items/{itemId:long}/served-at-table")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> ServeAtTable(Guid id, long itemId, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.ServeItemAsync(id, itemId, cancellationToken));

    [HttpPost("{id:guid}/move")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Move(Guid id, MoveTableRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.MoveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/merge")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Merge(Guid id, MergeBillRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.MergeAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/quote")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Quote(Guid id, DineInQuoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.QuoteAsync(id, request, cancellationToken));

    // Chủ quán 2026-11-13: ai phục vụ bàn cũng bấm thanh toán được (không cần quyền thu ngân của đơn online)
    [HttpPost("{id:guid}/checkout")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Checkout(Guid id, DineInCheckoutRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await dineIn.CheckoutAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [RequireCapability(StaffCapability.DineIn)]
    public async Task<IActionResult> Cancel(Guid id, DineInReasonRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await dineIn.CancelAsync(id, request, User.StaffId(), cancellationToken));
}
