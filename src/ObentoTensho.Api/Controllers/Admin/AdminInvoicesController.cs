using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Invoices;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Invoices.Abstractions;
using ObentoTensho.Core.Invoices.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Yêu cầu xuất hóa đơn: thu ngân thêm cho đơn; kế toán (quản lý) xem, xuất Excel, ghi số hóa đơn, hủy yêu cầu
[ApiController]
[Route("api/v1/admin")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminInvoicesController(InvoiceService invoices) : ControllerBase
{
    [HttpPost("orders/{orderId:guid}/invoice-request")]
    [RequireCapability(StaffCapability.OrdersCashier)]
    public async Task<IActionResult> Create(Guid orderId, InvoiceBuyerRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await invoices.CreateForOrderAsync(orderId, request, User.StaffId(), cancellationToken));

    [HttpGet("invoice-requests")]
    [RequireCapability(StaffCapability.Invoices)]
    public async Task<IActionResult> List(
        [FromQuery] InvoiceRequestStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await invoices.ListAsync(new InvoiceQuery(status, from, to, q, page, pageSize), cancellationToken));

    [HttpGet("invoice-requests/export")]
    [RequireCapability(StaffCapability.Invoices)]
    public async Task<IActionResult> Export(
        [FromQuery] InvoiceRequestStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        var file = await invoices.ExportAsync(new InvoiceQuery(status, from, to, q, 1, InvoiceService.ExportLimit), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("invoice-requests/{id:guid}/reveal")]
    [RequireCapability(StaffCapability.Invoices)]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await invoices.RevealAsync(id, cancellationToken));

    [HttpPost("invoice-requests/{id:guid}/issue")]
    [RequireCapability(StaffCapability.Invoices)]
    public async Task<IActionResult> Issue(Guid id, IssueInvoiceRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await invoices.IssueAsync(id, request, User.StaffId(), cancellationToken));

    [HttpPost("invoice-requests/{id:guid}/cancel")]
    [RequireCapability(StaffCapability.Invoices)]
    public async Task<IActionResult> Cancel(Guid id, CancelInvoiceRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await invoices.CancelAsync(id, request, cancellationToken));
}
