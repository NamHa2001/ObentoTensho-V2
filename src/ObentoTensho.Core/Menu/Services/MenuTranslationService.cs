using FluentValidation;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Menu.Services;

// docs/srs/22 §Ghi: PUT bản dịch en/ja; SourceHash tính lại từ bản Việt hiện hành khi lưu; Approve = đã có người thạo duyệt
public sealed class MenuTranslationService(
    IMenuRepository menu,
    IUnitOfWork unitOfWork,
    IMenuCache cache,
    IHtmlSanitizer sanitizer,
    IClock clock,
    IValidator<TranslationRequest> validator)
{
    private static readonly string[] TargetLanguages = ["en", "ja"];

    public async Task<Result<IReadOnlyList<TranslationDto>>> ListAsync(MenuTranslationKind kind, int id, CancellationToken cancellationToken)
    {
        if (await SourceHashAsync(kind, id, cancellationToken) is not { } hash)
        {
            return Result.Failure<IReadOnlyList<TranslationDto>>(ErrorCodes.NotFound);
        }

        IReadOnlyList<MenuTranslation> list = kind switch
        {
            MenuTranslationKind.Item => await menu.ListTranslationsAsync<MenuItemTranslation>(id, cancellationToken),
            MenuTranslationKind.Category => await menu.ListTranslationsAsync<MenuCategoryTranslation>(id, cancellationToken),
            MenuTranslationKind.OptionGroup => await menu.ListTranslationsAsync<OptionGroupTranslation>(id, cancellationToken),
            _ => await menu.ListTranslationsAsync<OptionTranslation>(id, cancellationToken),
        };
        return Result.Success<IReadOnlyList<TranslationDto>>(list.Select(t => t.ToDto(hash)).ToArray());
    }

    public async Task<Result<TranslationDto>> UpsertAsync(MenuTranslationKind kind, int id, string lang, TranslationRequest request, Guid staffId, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<TranslationDto>();
        }

        if (!TargetLanguages.Contains(lang, StringComparer.Ordinal) || await SourceHashAsync(kind, id, cancellationToken) is not { } hash)
        {
            return Result.Failure<TranslationDto>(ErrorCodes.NotFound);
        }

        var now = clock.UtcNow;
        var name = request.Name.Trim();
        MenuTranslation translation = kind switch
        {
            MenuTranslationKind.Item => await FindOrAddAsync(id, lang, () => new MenuItemTranslation { EntityId = id, Lang = lang, Name = name, SourceHash = hash, UpdatedAtUtc = now }, cancellationToken),
            MenuTranslationKind.Category => await FindOrAddAsync(id, lang, () => new MenuCategoryTranslation { EntityId = id, Lang = lang, Name = name, SourceHash = hash, UpdatedAtUtc = now }, cancellationToken),
            MenuTranslationKind.OptionGroup => await FindOrAddAsync(id, lang, () => new OptionGroupTranslation { EntityId = id, Lang = lang, Name = name, SourceHash = hash, UpdatedAtUtc = now }, cancellationToken),
            _ => await FindOrAddAsync(id, lang, () => new OptionTranslation { EntityId = id, Lang = lang, Name = name, SourceHash = hash, UpdatedAtUtc = now }, cancellationToken),
        };

        translation.Name = name;
        translation.ShortDesc = kind == MenuTranslationKind.Item && !string.IsNullOrWhiteSpace(request.ShortDesc) ? request.ShortDesc.Trim() : null;
        translation.FullDescHtml = kind == MenuTranslationKind.Item && !string.IsNullOrWhiteSpace(request.FullDescHtml) ? sanitizer.Sanitize(request.FullDescHtml) : null;
        translation.SourceHash = hash;
        translation.TranslatedByStaffId = staffId;
        translation.ReviewStatus = request.Approve ? TranslationStatus.Approved : TranslationStatus.Draft;
        translation.ReviewedByStaffId = request.Approve ? staffId : null;
        translation.ReviewedAtUtc = request.Approve ? now : null;
        translation.UpdatedAtUtc = now;

        if (kind == MenuTranslationKind.Item)
        {
            await RefreshNameSearchAsync(id, translation, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate();
        return Result.Success(translation.ToDto(hash));
    }

    private async Task<T> FindOrAddAsync<T>(int id, string lang, Func<T> create, CancellationToken cancellationToken)
        where T : MenuTranslation
    {
        if (await menu.FindTranslationAsync<T>(id, lang, cancellationToken) is { } existing)
        {
            return existing;
        }

        var created = create();
        menu.AddTranslation(created);
        return created;
    }

    // DB-17: chuỗi tìm kiếm gồm tên Việt + tên bản dịch đã duyệt còn khớp
    private async Task RefreshNameSearchAsync(int itemId, MenuTranslation current, CancellationToken cancellationToken)
    {
        if (await menu.FindItemAsync(itemId, cancellationToken) is not { } item)
        {
            return;
        }

        var hash = MenuSnapshot.ItemHash(item);
        var names = (await menu.ListTranslationsAsync<MenuItemTranslation>(itemId, cancellationToken))
            .Where(t => !string.Equals(t.Lang, current.Lang, StringComparison.Ordinal) && t.IsDisplayable(hash))
            .Select(t => t.Name)
            .Append(current.IsDisplayable(hash) ? current.Name : null);
        item.NameSearch = TextNormalizer.Search([item.NameVi, .. names]);
    }

    private async Task<string?> SourceHashAsync(MenuTranslationKind kind, int id, CancellationToken cancellationToken) => kind switch
    {
        MenuTranslationKind.Item => await menu.FindItemAsync(id, cancellationToken) is { } item ? MenuSnapshot.ItemHash(item) : null,
        MenuTranslationKind.Category => await menu.FindCategoryAsync(id, cancellationToken) is { } c ? MenuRules.SourceHash(c.Name) : null,
        MenuTranslationKind.OptionGroup => await menu.FindGroupAsync(id, cancellationToken) is { } g ? MenuRules.SourceHash(g.Name) : null,
        _ => await menu.FindOptionAsync(id, cancellationToken) is { } o ? MenuRules.SourceHash(o.Name) : null,
    };
}
