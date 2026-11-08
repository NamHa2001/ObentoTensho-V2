using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/17 §5
[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public sealed class ContentController(PublicContentService content, StaticPageService pages) : ControllerBase
{
    // Danh sách slug cho phép nằm ở StaticPage; slug lạ hoặc trang chưa có nội dung ⇒ 404
    // docs/srs/22 §4.2: ngôn ngữ chọn bằng ?lang= (vi/en/ja đang bật), không đoán theo Accept-Language (BR-I18N-08)
    [HttpGet("public/pages/{slug}")]
    public async Task<IActionResult> Page(string slug, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.GetPublicAsync(slug, lang, cancellationToken));

    [HttpGet("banners")]
    public async Task<IActionResult> Banners([FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.Data(await content.BannersAsync(lang, cancellationToken));

    [HttpGet("gallery")]
    public async Task<IActionResult> Gallery([FromQuery] GalleryCategory? category, [FromQuery] string? lang, CancellationToken cancellationToken) =>
        this.Data(await content.GalleryAsync(category, lang, cancellationToken));
}
