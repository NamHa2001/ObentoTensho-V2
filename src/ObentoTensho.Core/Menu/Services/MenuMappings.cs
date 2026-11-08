using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// Thực thể → DTO cho trang quản trị (AR-08)
public static class MenuMappings
{
    public static AdminCategoryDto ToAdmin(this MenuCategory c, int itemCount) =>
        new(c.Id, c.Name, c.Slug, c.DisplayOrder, c.IsActive, itemCount, RowVersions.Encode(c.RowVersion), c.ImageId);

    public static AdminMenuItemDto ToAdmin(this MenuItem i, IReadOnlyList<int> groupIds) =>
        new(
            i.Id,
            i.CategoryId,
            i.NameVi,
            i.Slug,
            i.ShortDesc,
            i.FullDescHtml,
            i.Price,
            i.OriginalPrice,
            new MenuLabels(i.IsNew, i.IsBestseller, i.IsFeatured, i.IsVegetarian, i.IsSpicy),
            i.AllowTakeaway,
            i.Status,
            i.SoldOutUntilUtc,
            MenuRules.ParseAllergens(i.Allergens),
            groupIds,
            i.DisplayOrder,
            RowVersions.Encode(i.RowVersion));

    public static AdminOptionDto ToAdmin(this MenuOption o) =>
        new(o.Id, o.GroupId, o.Name, o.PriceDelta, o.IsDefault, o.Status, MenuRules.ParseAllergens(o.Allergens), o.DisplayOrder, RowVersions.Encode(o.RowVersion));

    public static AdminOptionGroupDto ToAdmin(this OptionGroup g, IEnumerable<MenuOption> options) =>
        new(
            g.Id,
            g.Name,
            g.SelectionType,
            g.IsRequired,
            g.MinSelect,
            g.MaxSelect,
            g.DisplayOrder,
            g.IsActive,
            options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.Id).Select(o => o.ToAdmin()).ToArray(),
            RowVersions.Encode(g.RowVersion));

    public static TranslationDto ToDto(this MenuTranslation t, string currentSourceHash) =>
        new(t.Lang, t.Name, t.ShortDesc, t.FullDescHtml, t.ReviewStatus, !string.Equals(t.SourceHash, currentSourceHash, StringComparison.Ordinal));
}
