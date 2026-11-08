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
    public async Task<IActionResult> Get(string slug, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.DataOrProblem(await promotions.GetAsync(slug, lang, cancellationToken));
}
