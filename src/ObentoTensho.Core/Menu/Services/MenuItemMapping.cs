using ObentoTensho.Contracts.Dtos.Menu;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// Gán dữ liệu yêu cầu vào món — dùng chung cho form quản trị và nhập Excel để hai đường cho cùng kết quả (BR-MNU-14)
internal static class MenuItemMapping
{
    public static void Apply(MenuItem item, MenuItemRequest request, IHtmlSanitizer sanitizer, DateTime nowUtc)
    {
        item.CategoryId = request.CategoryId;
        item.NameVi = request.NameVi.Trim();
        item.ShortDesc = string.IsNullOrWhiteSpace(request.ShortDesc) ? null : request.ShortDesc.Trim();
        item.FullDescHtml = string.IsNullOrWhiteSpace(request.FullDescHtml) ? null : sanitizer.Sanitize(request.FullDescHtml);
        item.Price = request.Price;
        item.OriginalPrice = request.OriginalPrice;
        item.IsNew = request.Labels.IsNew;
        item.IsBestseller = request.Labels.IsBestseller;
        item.IsFeatured = request.Labels.IsFeatured;
        item.IsVegetarian = request.Labels.IsVegetarian;
        item.IsSpicy = request.Labels.IsSpicy;
        item.AllowTakeaway = request.AllowTakeaway;
        item.Status = request.Status;
        item.SoldOutUntilUtc = request.Status == SaleStatus.SoldOut ? item.SoldOutUntilUtc : null;
        item.Allergens = MenuRules.JoinAllergens(request.Allergens);
        item.UpdatedAtUtc = nowUtc;
    }

    // DB-17: tên Việt + tên các bản dịch đã duyệt còn khớp bản Việt hiện tại
    public static string SearchText(MenuItem item, IEnumerable<MenuItemTranslation> translations)
    {
        var hash = MenuSnapshot.ItemHash(item);
        var names = translations.Where(t => t.EntityId == item.Id && t.IsDisplayable(hash)).OrderBy(t => t.Lang, StringComparer.Ordinal).Select(t => t.Name);
        var text = TextNormalizer.Search([item.NameVi, .. names]);
        return text.Length <= MenuRules.NameSearchMaxLength ? text : text[..MenuRules.NameSearchMaxLength];
    }

    public static object Snapshot(MenuItem i) =>
        new { i.NameVi, i.Slug, i.CategoryId, i.Price, i.OriginalPrice, i.Status, i.AllowTakeaway, i.Allergens };
}
