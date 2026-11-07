using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu/items")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Menu)]
public sealed class MenuItemsController(MenuItemAdminService items) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? categoryId,
        [FromQuery] SaleStatus? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await items.ListAsync(new MenuItemQuery(categoryId, status, q, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.GetAsync(id, cancellationToken), i => i.Version);

    [HttpPost]
    public async Task<IActionResult> Create(MenuItemRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await items.CreateAsync(request, cancellationToken), i => i.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MenuItemRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.UpdateAsync(id, request, this.IfMatch(), cancellationToken), i => i.Version);

    // Bật/tắt nhanh lúc đang phục vụ (BR-MNU-16), không cần If-Match; vẫn ghi audit (BR-MNU-13)
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int id, MenuItemStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await items.SetStatusAsync(id, request, cancellationToken), i => i.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await items.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await items.ReorderAsync(request.Ids, cancellationToken));
}
