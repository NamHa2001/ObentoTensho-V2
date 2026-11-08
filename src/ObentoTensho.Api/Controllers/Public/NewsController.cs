using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/17 §5: tin tức công khai
[ApiController]
[Route("api/v1/news")]
[AllowAnonymous]
public sealed class NewsController(PublicNewsService news) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? lang = null,
        CancellationToken cancellationToken = default) =>
        Ok(await news.ListAsync(new PublicNewsQuery(category, q, page, pageSize), lang, cancellationToken));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        this.Data(await news.CategoriesAsync(cancellationToken));

    // BR-CNT-41: slug cũ trả 301 về địa chỉ mới
    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var found = await news.FindAsync(slug, lang, cancellationToken);
        if (!found.IsSuccess)
        {
            return this.ToProblem(found);
        }

        return found.Value.RedirectSlug is { } target
            ? RedirectPermanent($"/api/v1/news/{Uri.EscapeDataString(target)}")
            : this.Data(found.Value.Article);
    }
}
