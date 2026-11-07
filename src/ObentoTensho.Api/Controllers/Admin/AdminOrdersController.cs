using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/12 §8 (quản trị). SEC-12: bếp chỉ dùng màn hình bếp và chuyển bước chế biến (không thấy liên hệ, tiền);
// thu ngân/quản lý xem danh sách, gọi xác nhận, thanh toán, giao món, hoàn tiền.
[ApiController]
[Route("api/v1/admin/orders")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrdersController(OrderDeskService desk) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? q,
        [FromQuery] bool refundPending = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await desk.ListAsync(new OrderQuery(status, from, to, q, refundPending, page, pageSize), cancellationToken));

    [HttpGet("kitchen-view")]
    [RequireCapability(StaffCapability.OrdersView)]
    public async Task<IActionResult> Kitchen(CancellationToken cancellationToken) =>
        this.Data(await desk.KitchenAsync(cancellationToken));

    // docs/srs/20 #30: đơn chờ quá lâu, không ai đến lấy, chờ hoàn tiền
    [HttpGet("attention")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> Attention(CancellationToken cancellationToken) =>
        this.Data(await desk.AttentionAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.GetAsync(id, cancellationToken));

    // Quyền theo bước kiểm ở Service (bếp: làm món; thu ngân: giao món; hủy: quyền hủy)
    [HttpPatch("{id:guid}/status")]
    [RequireCapability(StaffCapability.OrdersView)]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeOrderStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.ChangeStatusAsync(id, request, User.StaffId(), User.StaffRole(), cancellationToken));

    [HttpPost("{id:guid}/reveal-contact")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> RevealContact(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.RevealContactAsync(id, User.StaffId(), cancellationToken));

    [HttpPatch("{id:guid}/contact-confirmed")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> ConfirmContact(Guid id, OrderNoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.ConfirmContactAsync(id, User.StaffId(), request.Note, cancellationToken));

    [HttpPatch("{id:guid}/payment")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> MarkPaid(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.MarkPaidAsync(id, User.StaffId(), cancellationToken));

    [HttpPatch("{id:guid}/refund")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> Refund(Guid id, OrderNoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await desk.RecordRefundAsync(id, User.StaffId(), request.Note, cancellationToken));
}
