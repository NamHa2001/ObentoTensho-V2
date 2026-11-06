using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/table-areas")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
public sealed class TableAreasController(TableAreaService areas) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await areas.ListAsync(cancellationToken));

    [HttpPost]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> Create(TableAreaRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await areas.CreateAsync(request, cancellationToken), a => a.Version);

    [HttpPut("{id:int}")]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> Update(int id, TableAreaRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await areas.UpdateAsync(id, request, this.IfMatch(), cancellationToken), a => a.Version);

    [HttpDelete("{id:int}")]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await areas.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
