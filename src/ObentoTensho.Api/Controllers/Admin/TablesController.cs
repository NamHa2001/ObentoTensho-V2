using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/01 §3: Reception+ xem và tạo bàn; Manager+ sửa/xóa/đổi trạng thái
[ApiController]
[Route("api/v1/admin/tables")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
public sealed class TablesController(TableService tables) : ControllerBase
{
    [HttpGet]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> List([FromQuery] int? areaId, CancellationToken cancellationToken) =>
        this.Data(await tables.ListAsync(areaId, cancellationToken));

    [HttpGet("floor-plan")]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> FloorPlan(CancellationToken cancellationToken) =>
        this.Data(await tables.FloorPlanAsync(cancellationToken));

    [HttpPost]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> Create(TableRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await tables.CreateAsync(request, cancellationToken), t => t.Version);

    [HttpPut("{id:int}")]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> Update(int id, TableRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await tables.UpdateAsync(id, request, this.IfMatch(), cancellationToken), t => t.Version);

    [HttpPatch("{id:int}/status")]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> SetStatus(int id, TableStatusRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await tables.SetStatusAsync(id, request.Status, cancellationToken), t => t.Version);

    [HttpDelete("{id:int}")]
    [RequireCapability(StaffCapability.TablesManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await tables.DeleteAsync(id, this.IfMatch(), cancellationToken));
}
