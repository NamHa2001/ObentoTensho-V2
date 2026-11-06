using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/blocked-dates")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
public sealed class BlockedDatesController(BlockedDateService blockedDates) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await blockedDates.ListAsync(cancellationToken));

    [HttpPost]
    [RequireCapability(StaffCapability.Shifts)]
    public async Task<IActionResult> Create(BlockedDateRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await blockedDates.CreateAsync(request, cancellationToken));

    [HttpDelete("{id:int}")]
    [RequireCapability(StaffCapability.Shifts)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await blockedDates.DeleteAsync(id, cancellationToken));
}
