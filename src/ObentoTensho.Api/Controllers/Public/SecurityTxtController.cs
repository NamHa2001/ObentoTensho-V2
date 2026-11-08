using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;

namespace ObentoTensho.Api.Controllers.Public;

// SEC-60, RFC 9116: nơi nhà nghiên cứu bảo mật tìm địa chỉ báo lỗ hổng. Expires luôn tính lại từ thời điểm hiện tại
[ApiController]
[AllowAnonymous]
public sealed class SecurityTxtController(IOptions<PublicLinkOptions> options, IClock clock) : ControllerBase
{
    private const int ExpiresDays = 180;

    [HttpGet("/.well-known/security.txt")]
    public IActionResult Get()
    {
        if (string.IsNullOrWhiteSpace(options.Value.SecurityContact))
        {
            return NotFound();
        }

        var expires = clock.UtcNow.AddDays(ExpiresDays).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return Content($"Contact: mailto:{options.Value.SecurityContact.Trim()}\nExpires: {expires}\nPreferred-Languages: vi, en\n", "text/plain; charset=utf-8");
    }
}
