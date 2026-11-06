using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Tables.Services;

namespace ObentoTensho.Api.Controllers.Public;

[ApiController]
[Route("api/v1/public")]
public sealed class PublicScheduleController(OpeningHoursService hours) : ControllerBase
{
    // Trang công khai; dữ liệu giống nhau với mọi người nên cho phép cache ngắn (PERF-11)
    [HttpGet("opening-hours")]
    [AllowAnonymous]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> OpeningHours(CancellationToken cancellationToken) =>
        this.Data(await hours.GetPublicAsync(cancellationToken));
}
