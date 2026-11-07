using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Promotions.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/14 §6 (quản trị): ma trận quyền "Khách hàng, khuyến mãi, voucher" — SuperAdmin, Manager
[ApiController]
[Route("api/v1/admin/promotions")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Customers)]
public sealed class AdminPromotionsController(PromotionAdminService promotions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await promotions.ListAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await promotions.GetAsync(id, cancellationToken), p => p.Version);

    [HttpPost]
    public async Task<IActionResult> Create(PromotionRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await promotions.CreateAsync(request, User.StaffId(), cancellationToken), p => p.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PromotionRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await promotions.UpdateAsync(id, request, this.IfMatch(), cancellationToken), p => p.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await promotions.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
