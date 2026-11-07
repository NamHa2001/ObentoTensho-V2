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
[Route("api/v1/admin/banners")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class BannersController(BannerAdminService banners) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await banners.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(BannerRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await banners.CreateAsync(request, cancellationToken), b => b.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BannerRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await banners.UpdateAsync(id, request, this.IfMatch(), cancellationToken), b => b.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await banners.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPatch("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await banners.ReorderAsync(request.Ids, cancellationToken));
}
