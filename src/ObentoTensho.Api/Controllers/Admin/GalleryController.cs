using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/gallery")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class GalleryController(GalleryAdminService gallery) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await gallery.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(GalleryItemRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await gallery.CreateAsync(request, cancellationToken), g => g.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, GalleryItemRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await gallery.UpdateAsync(id, request, this.IfMatch(), cancellationToken), g => g.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await gallery.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await gallery.ReorderAsync(request.Ids, cancellationToken));
}
