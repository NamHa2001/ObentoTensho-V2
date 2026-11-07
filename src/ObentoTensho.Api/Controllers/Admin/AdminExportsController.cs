using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Customers.Services;

namespace ObentoTensho.Api.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/exports")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.CustomerExport)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminExportsController(DataExportService exports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken) =>
        this.Data(await exports.ListMineAsync(User.StaffId(), cancellationToken));

    [HttpPost("download")]
    public async Task<IActionResult> Download(DataExportDownloadRequest request, CancellationToken cancellationToken)
    {
        var result = await exports.DownloadAsync(request, User.StaffId(), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.FileName)
            : this.ToProblem(result);
    }
}
