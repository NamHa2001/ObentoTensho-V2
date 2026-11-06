using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Tables;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// Giờ thường kỳ: chỉ SuperAdmin ghi; giờ đặc biệt theo ngày: Manager+ (docs/srs/16 §2)
[ApiController]
[Route("api/v1/admin")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
public sealed class OpeningHoursController(OpeningHoursService hours) : ControllerBase
{
    [HttpGet("opening-hours")]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> GetWeekly(CancellationToken cancellationToken)
    {
        var weekly = await hours.GetWeeklyAsync(cancellationToken);
        return this.Data(weekly, weekly.Version);
    }

    [HttpPut("opening-hours")]
    [RequireCapability(StaffCapability.RegularHours)]
    public async Task<IActionResult> ReplaceWeekly(WeeklyHoursRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await hours.ReplaceWeeklyAsync(request, this.IfMatch(), cancellationToken), w => w.Version);

    [HttpGet("special-hours")]
    [RequireCapability(StaffCapability.TablesView)]
    public async Task<IActionResult> ListSpecial(CancellationToken cancellationToken) =>
        this.Data(await hours.ListSpecialAsync(cancellationToken));

    [HttpPut("special-hours/{date}")]
    [RequireCapability(StaffCapability.SpecialHours)]
    public async Task<IActionResult> SetSpecial(DateOnly date, SpecialHoursRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await hours.SetSpecialAsync(date, request, cancellationToken));

    [HttpDelete("special-hours/{date}")]
    [RequireCapability(StaffCapability.SpecialHours)]
    public async Task<IActionResult> DeleteSpecial(DateOnly date, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await hours.DeleteSpecialAsync(date, cancellationToken));
}
