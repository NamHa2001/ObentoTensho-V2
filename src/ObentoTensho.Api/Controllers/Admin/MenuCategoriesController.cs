using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu/categories")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Menu)]
public sealed class MenuCategoriesController(MenuCategoryAdminService categories) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await categories.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await categories.CreateAsync(request, cancellationToken), c => c.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CategoryRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await categories.UpdateAsync(id, request, this.IfMatch(), cancellationToken), c => c.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await categories.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await categories.ReorderAsync(request.Ids, cancellationToken));
}
