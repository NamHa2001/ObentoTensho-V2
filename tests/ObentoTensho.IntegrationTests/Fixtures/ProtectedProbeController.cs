using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Endpoint chỉ có trong test để kiểm access token (chính sách mặc định yêu cầu đăng nhập)
[ApiController]
[Route("api/v1/test-probe")]
public sealed class ProtectedProbeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Get() => Ok(new { subject = User.Identity?.Name });
}
