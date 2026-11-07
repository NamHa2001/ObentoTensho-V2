using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Vouchers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Vouchers.Abstractions;
using ObentoTensho.Core.Vouchers.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/14 §6 (quản trị): ma trận quyền "Khách hàng, khuyến mãi, voucher" — SuperAdmin, Manager
[ApiController]
[Route("api/v1/admin/vouchers")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Customers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminVouchersController(VoucherAdminService vouchers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] VoucherSource? source,
        [FromQuery] ApprovalStatus? approvalStatus,
        [FromQuery] Guid? batchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await vouchers.ListAsync(new VoucherQuery(q, source, approvalStatus, batchId, page, pageSize), cancellationToken));

    // BR-VCH-15: dùng cùng bộ lọc với danh sách
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? q,
        [FromQuery] VoucherSource? source,
        [FromQuery] ApprovalStatus? approvalStatus,
        [FromQuery] Guid? batchId,
        CancellationToken cancellationToken) =>
        File(
            await vouchers.ExportAsync(new VoucherQuery(q, source, approvalStatus, batchId, Page: 1, PageSize: 1), cancellationToken),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            vouchers.ExportFileName());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.GetAsync(id, cancellationToken), v => v.Version);

    [HttpPost]
    public async Task<IActionResult> Create(VoucherRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await vouchers.CreateAsync(request, User.StaffId(), cancellationToken), v => v.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, VoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.UpdateAsync(id, request, this.IfMatch(), cancellationToken), v => v.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await vouchers.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPost("bulk-generate")]
    public async Task<IActionResult> BulkGenerate(BulkGenerateVouchersRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await vouchers.BulkGenerateAsync(request, User.StaffId(), cancellationToken));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, ReviewVoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.ReviewAsync(id, User.StaffId(), approve: true, request.Note, cancellationToken));

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, ReviewVoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.ReviewAsync(id, User.StaffId(), approve: false, request.Note, cancellationToken));

    [HttpPost("batches/{batchId:guid}/approve")]
    public async Task<IActionResult> ApproveBatch(Guid batchId, ReviewVoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.ReviewBatchAsync(batchId, User.StaffId(), approve: true, request.Note, cancellationToken));

    [HttpPost("batches/{batchId:guid}/reject")]
    public async Task<IActionResult> RejectBatch(Guid batchId, ReviewVoucherRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await vouchers.ReviewBatchAsync(batchId, User.StaffId(), approve: false, request.Note, cancellationToken));
}
