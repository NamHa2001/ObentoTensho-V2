using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/15 §4. Dữ liệu đọc từ ảnh chụp trong bộ nhớ nên tham số lạ không tạo thêm mục cache (ATK-14)
[ApiController]
[Route("api/v1/menu")]
[AllowAnonymous]
public sealed class MenuController(PublicMenuService menu, ReviewService reviews) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.Data(await menu.CategoriesAsync(lang, cancellationToken));

    [HttpGet("items")]
    public async Task<IActionResult> Items(
        [FromQuery] string? category,
        [FromQuery] string? q,
        [FromQuery] string? excludeAllergens,
        [FromQuery] bool? takeaway,
        [FromQuery] string? sort,
        [FromQuery] string? lang,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await menu.ItemsAsync(
            new PublicMenuQuery(category, q, MenuRules.ParseAllergens(excludeAllergens), takeaway, sort, page, pageSize, lang ?? string.Empty),
            cancellationToken));

    [HttpGet("items/featured")]
    public async Task<IActionResult> Featured([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.Data(await menu.FeaturedAsync(lang, cancellationToken));

    [HttpGet("items/{slug}")]
    public async Task<IActionResult> Detail(string slug, [FromQuery] string? lang, CancellationToken cancellationToken)
    {
        var found = await menu.DetailAsync(slug, lang, cancellationToken);
        if (found.IsSuccess)
        {
            return this.Data(found.Value);
        }

        // BR-CNT-41: slug cũ ⇒ 301 về địa chỉ mới, giữ ngôn ngữ đang xem
        return await menu.CurrentSlugAsync(slug, cancellationToken) is { } target
            ? RedirectPermanent($"/api/v1/menu/items/{Uri.EscapeDataString(target)}{LangQuery(lang)}")
            : this.ToProblem(found);
    }

    private static string LangQuery(string? lang) => string.IsNullOrWhiteSpace(lang) ? string.Empty : "?lang=" + Uri.EscapeDataString(lang);

    // Đánh giá đã duyệt của món đang bán; món ẩn/đã xóa ⇒ 404 như trang chi tiết
    [HttpGet("items/{slug}/reviews")]
    public async Task<IActionResult> Reviews(string slug, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var item = await menu.FindVisibleIdAsync(slug, cancellationToken);
        return item.IsSuccess ? Ok(await reviews.ListPublicAsync(item.Value, page, pageSize, cacheable: true, cancellationToken)) : this.ToProblem(item);
    }
}
