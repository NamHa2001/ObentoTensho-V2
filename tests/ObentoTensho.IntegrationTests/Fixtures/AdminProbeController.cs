using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Endpoint chỉ có trong test: yêu cầu access token nhân viên
[ApiController]
[Route("api/v1/admin/test-probe")]
public sealed class AdminProbeController : ControllerBase
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = "staff")]
    public IActionResult Get() => Ok(new { subject = User.Identity?.Name });
}
