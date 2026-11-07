using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Menu.Services;

// Thực đơn công khai (docs/srs/15 §3.3, §6): chỉ món chưa xóa, không ẩn, thuộc danh mục đang bật (BR-MNU-02)
public sealed class PublicMenuService(IMenuRepository menu, IMenuCache cache, IClock clock)
{
    public const int MaxPageSize = 100;
    private const int FeaturedLimit = 12;
    private const int RelatedLimit = 4;

    public async Task<IReadOnlyList<PublicCategoryDto>> CategoriesAsync(string? lang, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var language = Language(lang);
        var counts = snapshot.Items.GroupBy(i => i.CategoryId).ToDictionary(g => g.Key, g => g.Count());
        return snapshot.Categories
            .Select(c =>
            {
                var t = snapshot.Translation<MenuCategoryTranslation>(c.Id, language);
                return new PublicCategoryDto(c.Id, c.Slug, t?.Name ?? c.Name, counts.GetValueOrDefault(c.Id), t is null ? MenuSnapshot.SourceLang : language, t is null && !string.Equals(language, MenuSnapshot.SourceLang, StringComparison.Ordinal));
            })
            .ToArray();
    }

    public async Task<PagedResponse<PublicMenuItemDto>> ItemsAsync(PublicMenuQuery query, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var language = Language(query.Lang);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var items = Filter(snapshot, query).ToArray();
        var paged = Sort(snapshot, items, query.Sort).Skip((page - 1) * pageSize).Take(pageSize);
        return new PagedResponse<PublicMenuItemDto>(
            paged.Select(i => ToDto(snapshot, i, language)).ToArray(),
            new PageMeta(page, pageSize, items.Length));
    }

    public async Task<IReadOnlyList<PublicMenuItemDto>> FeaturedAsync(string? lang, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        return Sort(snapshot, snapshot.Items.Where(i => i.IsFeatured), sort: null)
            .Take(FeaturedLimit)
            .Select(i => ToDto(snapshot, i, Language(lang)))
            .ToArray();
    }

    // Món ẩn/đã xóa trả 404 kể cả khi biết slug (BR-MNU-02)
    public async Task<Result<PublicMenuItemDetailDto>> DetailAsync(string slug, string? lang, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(cancellationToken);
        var item = snapshot.Items.FirstOrDefault(i => string.Equals(i.Slug, slug, StringComparison.Ordinal));
        if (item is null)
        {
            return Result.Failure<PublicMenuItemDetailDto>(ErrorCodes.NotFound);
        }

        var language = Language(lang);
        var t = snapshot.Translation<MenuItemTranslation>(item.Id, language);
        var groups = snapshot.GroupsOf(item.Id).Select(g => ToDto(snapshot, g, language)).ToArray();
        var related = Sort(snapshot, snapshot.Items.Where(i => i.CategoryId == item.CategoryId && i.Id != item.Id), sort: null)
            .Take(RelatedLimit)
            .Select(i => ToDto(snapshot, i, language))
            .ToArray();
        return Result.Success(new PublicMenuItemDetailDto(ToDto(snapshot, item, language), t?.FullDescHtml ?? item.FullDescHtml, groups, related));
    }

    // Ảnh chụp thực đơn công khai (cache); đặt món dùng để lấy tên đã dịch, còn giá luôn đọc từ CSDL (BR-ORD-02)
    public Task<MenuSnapshot> SnapshotAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(async ct => new MenuSnapshot(await menu.LoadPublicDataAsync(ct)), cancellationToken);

    private static IEnumerable<MenuItem> Filter(MenuSnapshot snapshot, PublicMenuQuery query)
    {
        IEnumerable<MenuItem> items = snapshot.Items;
        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            var category = snapshot.Categories.FirstOrDefault(c => string.Equals(c.Slug, query.CategorySlug, StringComparison.Ordinal));
            items = items.Where(i => i.CategoryId == category?.Id);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var terms = TextNormalizer.Search(query.Search).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            items = items.Where(i => terms.All(term => i.NameSearch.Contains(term, StringComparison.Ordinal)));
        }

        if (query.ExcludeAllergens is { Count: > 0 } exclude)
        {
            items = items.Where(i => !snapshot.AllergensOf(i).Intersect(exclude).Any());
        }

        return query.TakeawayOnly == true ? items.Where(i => i.AllowTakeaway) : items;
    }

    // Chỉ các kiểu sắp xếp cho phép (docs/srs/09 §5); mặc định theo thứ tự danh mục rồi thứ tự món
    private static IEnumerable<MenuItem> Sort(MenuSnapshot snapshot, IEnumerable<MenuItem> items, string? sort)
    {
        var categoryOrder = snapshot.Categories.Select((c, index) => (c.Id, index)).ToDictionary(x => x.Id, x => x.index);
        return sort switch
        {
            "price" => items.OrderBy(i => i.Price).ThenBy(i => i.Id),
            "-price" => items.OrderByDescending(i => i.Price).ThenBy(i => i.Id),
            "name" => items.OrderBy(i => i.NameSearch, StringComparer.Ordinal),
            "new" => items.OrderByDescending(i => i.IsNew).ThenByDescending(i => i.CreatedAtUtc),
            _ => items.OrderBy(i => categoryOrder.GetValueOrDefault(i.CategoryId)).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id),
        };
    }

    private PublicMenuItemDto ToDto(MenuSnapshot snapshot, MenuItem item, string language)
    {
        var t = snapshot.Translation<MenuItemTranslation>(item.Id, language);
        return new PublicMenuItemDto(
            item.Id,
            item.Slug,
            item.CategoryId,
            t?.Name ?? item.NameVi,
            t is null ? item.ShortDesc : t.ShortDesc,
            item.Price,
            item.OriginalPrice,
            MenuRules.Effective(item.Status, item.SoldOutUntilUtc, clock.UtcNow),
            item.AllowTakeaway,
            new MenuLabels(item.IsNew, item.IsBestseller, item.IsFeatured, item.IsVegetarian, item.IsSpicy),
            snapshot.AllergensOf(item),
            t is null ? MenuSnapshot.SourceLang : language,
            t is null && !string.Equals(language, MenuSnapshot.SourceLang, StringComparison.Ordinal));
    }

    private static PublicOptionGroupDto ToDto(MenuSnapshot snapshot, OptionGroup group, string language) =>
        new(
            group.Id,
            snapshot.Translation<OptionGroupTranslation>(group.Id, language)?.Name ?? group.Name,
            group.SelectionType,
            group.IsRequired,
            group.MinSelect,
            group.MaxSelect,
            snapshot.OptionsOf(group.Id)
                .Select(o => new PublicOptionDto(
                    o.Id,
                    snapshot.Translation<OptionTranslation>(o.Id, language)?.Name ?? o.Name,
                    o.PriceDelta,
                    o.IsDefault,
                    o.Status,
                    MenuRules.ParseAllergens(o.Allergens)))
                .ToArray());

    private static string Language(string? lang) =>
        lang is not null && AuthPolicy.SupportedLanguages.Contains(lang, StringComparer.Ordinal) ? lang : MenuSnapshot.SourceLang;
}
