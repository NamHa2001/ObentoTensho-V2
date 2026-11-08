using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Promotions.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/14 §6: không có tham số trạng thái — bản nháp/đã tắt không bao giờ lộ ra (BR-VCH-16)
[ApiController]
[Route("api/v1/promotions")]
[AllowAnonymous]
public sealed class PromotionsController(PublicPromotionService promotions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.Data(await promotions.ListAsync(lang, cancellationToken));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var found = await promotions.GetAsync(slug, lang, cancellationToken);
        if (found.IsSuccess)
        {
            return this.Data(found.Value);
        }

        // BR-CNT-41: slug cũ ⇒ 301 về địa chỉ mới
        return await promotions.CurrentSlugAsync(slug, cancellationToken) is { } target
            ? RedirectPermanent($"/api/v1/promotions/{Uri.EscapeDataString(target)}{(string.IsNullOrWhiteSpace(lang) ? string.Empty : "?lang=" + Uri.EscapeDataString(lang))}")
            : this.ToProblem(found);
    }
}
