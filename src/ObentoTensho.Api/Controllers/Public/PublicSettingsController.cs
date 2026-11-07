using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Settings.Abstractions;

namespace ObentoTensho.Api.Controllers.Public;

// Thông tin quán, mạng xã hội, chuyển khoản, SEO: chỉ các khóa đánh dấu công khai trong danh mục
[ApiController]
[Route("api/v1/public/settings")]
[AllowAnonymous]
public sealed class PublicSettingsController(ISettingsReader settings) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => this.Data(settings.Public());
}
