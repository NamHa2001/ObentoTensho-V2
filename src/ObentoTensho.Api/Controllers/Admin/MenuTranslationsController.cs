using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Api.Controllers.Admin;

// docs/srs/22: bản dịch en/ja cho món, danh mục, nhóm tùy chọn, tùy chọn. §4.3: ContentEditor, Manager, SuperAdmin dịch/duyệt
[ApiController]
[Route("api/v1/admin/menu/{kind:regex(^(items|categories|option-groups|options)$)}/{id:int}/translations")]
[EnableCors(AuthConfiguration.AdminCorsPolicy)]
[RequireCapability(StaffCapability.Content)]
public sealed class MenuTranslationsController(MenuTranslationService translations) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string kind, int id, CancellationToken cancellationToken) =>
        this.DataOrProblem(await translations.ListAsync(Kind(kind), id, cancellationToken));

    [HttpPut("{lang:regex(^(en|ja)$)}")]
    public async Task<IActionResult> Upsert(string kind, int id, string lang, TranslationRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await translations.UpsertAsync(Kind(kind), id, lang, request, User.StaffId(), cancellationToken));

    private static MenuTranslationKind Kind(string kind) => kind switch
    {
        "items" => MenuTranslationKind.Item,
        "categories" => MenuTranslationKind.Category,
        "option-groups" => MenuTranslationKind.OptionGroup,
        _ => MenuTranslationKind.Option,
    };
}
