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
    [HttpGet("public/pages/{slug}")]
    public async Task<IActionResult> Page(string slug, CancellationToken cancellationToken) =>
        this.DataOrProblem(await pages.GetAsync(slug, cancellationToken));

    [HttpGet("banners")]
    public async Task<IActionResult> Banners(CancellationToken cancellationToken) =>
        this.Data(await content.BannersAsync(cancellationToken));

    [HttpGet("gallery")]
    public async Task<IActionResult> Gallery([FromQuery] GalleryCategory? category, CancellationToken cancellationToken) =>
        this.Data(await content.GalleryAsync(category, cancellationToken));
}
