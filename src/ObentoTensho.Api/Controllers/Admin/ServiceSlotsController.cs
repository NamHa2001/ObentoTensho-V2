using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/service-slots")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
public sealed class ServiceSlotsController(ServiceSlotService slots) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await slots.ListAsync(cancellationToken));

    [HttpPost]
    [RequireCapability(StaffCapability.Shifts)]
    public async Task<IActionResult> Create(ServiceSlotRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await slots.CreateAsync(request, cancellationToken), s => s.Version);

    [HttpPut("{id:int}")]
    [RequireCapability(StaffCapability.Shifts)]
    public async Task<IActionResult> Update(int id, ServiceSlotRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await slots.UpdateAsync(id, request, this.IfMatch(), cancellationToken), s => s.Version);

    [HttpDelete("{id:int}")]
    [RequireCapability(StaffCapability.Shifts)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await slots.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
