using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/menu")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Menu)]
public sealed class OptionGroupsController(OptionGroupAdminService groups) : ControllerBase
{
    [HttpGet("option-groups")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        this.Data(await groups.ListAsync(cancellationToken));

    [HttpPost("option-groups")]
    public async Task<IActionResult> Create(OptionGroupRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await groups.CreateGroupAsync(request, cancellationToken), g => g.Version);

    [HttpPut("option-groups/{id:int}")]
    public async Task<IActionResult> Update(int id, OptionGroupRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await groups.UpdateGroupAsync(id, request, this.IfMatch(), cancellationToken), g => g.Version);

    [HttpDelete("option-groups/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await groups.DeleteGroupAsync(id, cancellationToken));

    [HttpPost("option-groups/{groupId:int}/options")]
    public async Task<IActionResult> CreateOption(int groupId, OptionRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await groups.CreateOptionAsync(groupId, request, cancellationToken), o => o.Version);

    [HttpPut("options/{id:int}")]
    public async Task<IActionResult> UpdateOption(int id, OptionRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await groups.UpdateOptionAsync(id, request, this.IfMatch(), cancellationToken), o => o.Version);

    [HttpDelete("options/{id:int}")]
    public async Task<IActionResult> DeleteOption(int id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await groups.DeleteOptionAsync(id, cancellationToken));
}
