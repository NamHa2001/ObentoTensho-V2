using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// FR-CNT-04: PUT tạo trang nếu chưa có; đã có thì cần If-Match
[ApiController]
[Route("api/v1/admin/pages")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class StaticPagesController(StaticPageService pages) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await pages.ListAsync(cancellationToken));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.GetAsync(slug, cancellationToken), p => p.Version);

    [HttpPut("{slug}")]
    public async Task<IActionResult> Save(string slug, StaticPageRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.SaveAsync(slug, request, this.IfMatch(), User.StaffId(), cancellationToken), p => p.Version);

    [HttpGet("{slug}/revisions")]
    public async Task<IActionResult> Revisions(string slug, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.RevisionsAsync(slug, cancellationToken));

    [HttpPost("{slug}/revisions/{revision:int}/restore")]
    public async Task<IActionResult> Restore(string slug, int revision, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.RestoreAsync(slug, revision, this.IfMatch(), User.StaffId(), cancellationToken), p => p.Version);
}
