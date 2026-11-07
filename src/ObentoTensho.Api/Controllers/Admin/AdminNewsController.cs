using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/news")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class AdminNewsController(NewsAdminService news, NewsCategoryService categories) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] NewsStatus? status,
        [FromQuery] int? categoryId,
        [FromQuery] string? q,
        [FromQuery] bool trash = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await news.ListAsync(new NewsQuery(status, categoryId, q, trash, page, pageSize), cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await news.GetAsync(id, cancellationToken), n => n.Version);

    [HttpPost]
    public async Task<IActionResult> Create(NewsRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await news.CreateAsync(request, User.StaffId(), cancellationToken), n => n.Version);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, NewsRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await news.UpdateAsync(id, request, this.IfMatch(), cancellationToken), n => n.Version);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await news.DeleteAsync(id, this.IfMatch(), cancellationToken));

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await news.RestoreAsync(id, cancellationToken));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        this.Data(await categories.ListAsync(cancellationToken));

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(NewsCategoryRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await categories.CreateAsync(request, cancellationToken), c => c.Version);

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, NewsCategoryRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await categories.UpdateAsync(id, request, this.IfMatch(), cancellationToken), c => c.Version);

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await categories.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
