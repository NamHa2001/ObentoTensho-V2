using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Customers.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/18 §1, §4
[ApiController]
[Route("api/v1/admin/customers")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Customers)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminCustomersController(CustomerAdminService customers, DataExportService exports, AccountDeletionService deletion) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] MemberTier? tier,
        [FromQuery] bool? isActive,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await customers.ListAsync(q, tier, isActive, from, to, page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await customers.GetAsync(id, User.StaffId(), cancellationToken));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, CustomerStatusRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await customers.SetStatusAsync(id, request, cancellationToken));

    // BR-CUS-05: khách yêu cầu qua nhân viên; cùng 7 ngày chờ như tự xóa
    [HttpPost("{id:guid}/deletion")]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> ScheduleDeletion(Guid id, CustomerDeletionRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await deletion.RequestByStaffAsync(id, request, cancellationToken));

    // BR-CUS-07: chỉ xếp việc; tệp do job tạo
    [HttpPost("export")]
    [RequireCapability(StaffCapability.CustomerExport)]
    [ServiceFilter<StepUpFilter>]
    public async Task<IActionResult> Export(DataExportRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await exports.RequestAsync(DataExportKind.Customers, request, User.StaffId(), cancellationToken));
}
