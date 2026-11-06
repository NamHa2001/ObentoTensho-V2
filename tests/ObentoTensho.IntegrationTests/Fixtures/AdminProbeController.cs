using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Endpoint chỉ có trong test: yêu cầu access token nhân viên / đúng quyền theo ma trận
[ApiController]
[Route("api/v1/admin/test-probe")]
public sealed class AdminProbeController : ControllerBase
{
    [HttpGet]
    [Authorize(AuthenticationSchemes = "staff")]
    public IActionResult Get() => Ok(new { subject = User.Identity?.Name });

    [HttpGet("content")]
    [RequireCapability(StaffCapability.Content)]
    public IActionResult Content() => Ok();
}
