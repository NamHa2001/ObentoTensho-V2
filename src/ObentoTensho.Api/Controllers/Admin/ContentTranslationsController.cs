using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Localization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Localization.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/22 §4.2, §4.3: bản dịch khuyến mãi, banner, gallery, tin tức, trang tĩnh (khóa là id; trang tĩnh là slug)
// và màn hình tiến độ dịch. Bật/tắt ngôn ngữ nằm ở Cài đặt (Manager+)
[ApiController]
[Route("api/v1/admin/translations")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ContentTranslationsController(ContentTranslationService translations, TranslationProgressService progress) : ControllerBase
{
    [HttpGet("progress")]
    public async Task<IActionResult> Progress([FromQuery] string lang, CancellationToken cancellationToken) =>
        await progress.ProgressAsync(lang, cancellationToken) is { } result
            ? this.Data(result)
            : this.ToProblem(Result.Failure(ErrorCodes.NotFound));

    [HttpGet("{kind:regex(^(promotion|banner|gallery|news|static_page)$)}/{key}")]
    public async Task<IActionResult> List(string kind, string key, CancellationToken cancellationToken) =>
        this.DataOrProblem(await translations.ListAsync(Kind(kind), key, cancellationToken));

    [HttpPut("{kind:regex(^(promotion|banner|gallery|news|static_page)$)}/{key}/{lang:regex(^(en|ja)$)}")]
    public async Task<IActionResult> Upsert(string kind, string key, string lang, ContentTranslationRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await translations.UpsertAsync(Kind(kind), key, lang, request, User.StaffId(), cancellationToken));

    private static TranslatableContent Kind(string kind) => kind switch
    {
        "promotion" => TranslatableContent.Promotion,
        "banner" => TranslatableContent.Banner,
        "gallery" => TranslatableContent.Gallery,
        "news" => TranslatableContent.News,
        _ => TranslatableContent.StaticPage,
    };
}
