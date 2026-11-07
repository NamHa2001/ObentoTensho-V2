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
public sealed class ContentController(PublicContentService content) : ControllerBase
{
    [HttpGet("banners")]
    public async Task<IActionResult> Banners(CancellationToken cancellationToken) =>
        this.Data(await content.BannersAsync(cancellationToken));

    [HttpGet("gallery")]
    public async Task<IActionResult> Gallery([FromQuery] GalleryCategory? category, CancellationToken cancellationToken) =>
        this.Data(await content.GalleryAsync(category, cancellationToken));
}
