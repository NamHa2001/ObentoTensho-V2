using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class MenuRepository(AppDbContext db) : IMenuRepository
{
    public async Task<IReadOnlyList<MenuCategory>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        await db.MenuCategories.AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToListAsync(cancellationToken);

    public Task<MenuCategory?> FindCategoryAsync(int id, CancellationToken cancellationToken) =>
        db.MenuCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MenuCategory>> FindCategoriesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        await db.MenuCategories.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);

    public Task<bool> CategorySlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken) =>
        db.MenuCategories.AnyAsync(c => c.Slug == slug && c.Id != exceptId, cancellationToken);

    public Task<bool> CategoryHasItemsAsync(int categoryId, CancellationToken cancellationToken) =>
        db.MenuItems.AnyAsync(i => i.CategoryId == categoryId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, int>> CountItemsByCategoryAsync(CancellationToken cancellationToken) =>
        await db.MenuItems.Where(i => !i.IsDeleted)
            .GroupBy(i => i.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

    public void AddCategory(MenuCategory category) => db.MenuCategories.Add(category);

    public void RemoveCategory(MenuCategory category) => db.MenuCategories.Remove(category);

    public async Task<(IReadOnlyList<MenuItem> Items, int Total)> ListItemsAsync(MenuItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var items = db.MenuItems.AsNoTracking().Where(i => !i.IsDeleted);
        if (query.CategoryId is { } categoryId)
        {
            items = items.Where(i => i.CategoryId == categoryId);
        }

        if (query.Status is { } status)
        {
            items = items.Where(i => i.Status == status);
        }

        var search = TextNormalizer.Search(query.Search);
        if (search.Length > 0)
        {
            items = items.Where(i => i.NameSearch.Contains(search) || i.Slug.Contains(search));
        }

        var total = await items.CountAsync(cancellationToken);
        var page = await items
            .OrderBy(i => i.CategoryId).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .ToListAsync(cancellationToken);
        return (page, total);
    }

    public Task<MenuItem?> FindItemAsync(int id, CancellationToken cancellationToken) =>
        db.MenuItems.SingleOrDefaultAsync(i => i.Id == id && !i.IsDeleted, cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> FindItemsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        await db.MenuItems.Where(i => ids.Contains(i.Id) && !i.IsDeleted).ToListAsync(cancellationToken);

    public Task<bool> ItemSlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken) =>
        db.MenuItems.AnyAsync(i => i.Slug == slug && i.Id != exceptId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetItemGroupIdsAsync(int itemId, CancellationToken cancellationToken) =>
        await db.MenuItemOptionGroups.AsNoTracking()
            .Where(l => l.MenuItemId == itemId)
            .OrderBy(l => l.DisplayOrder)
            .Select(l => l.GroupId)
            .ToListAsync(cancellationToken);

    // Thứ tự nhóm theo thứ tự gửi lên; gọi trong giao dịch của dịch vụ món
    public async Task ReplaceItemGroupsAsync(int itemId, IReadOnlyList<int> groupIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        await db.MenuItemOptionGroups.Where(l => l.MenuItemId == itemId).ExecuteDeleteAsync(cancellationToken);
        db.MenuItemOptionGroups.AddRange(groupIds.Distinct().Select((groupId, index) =>
            new MenuItemOptionGroup { MenuItemId = itemId, GroupId = groupId, DisplayOrder = index }));
    }

    public void AddItem(MenuItem item) => db.MenuItems.Add(item);

    public async Task<IReadOnlyList<MenuItem>> ListAllItemsAsync(CancellationToken cancellationToken) =>
        await db.MenuItems.AsNoTracking()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.CategoryId).ThenBy(i => i.DisplayOrder).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> FindItemsBySlugAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken) =>
        await db.MenuItems.Where(i => slugs.Contains(i.Slug) && !i.IsDeleted).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> TakenItemSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken) =>
        (await db.MenuItems.Where(i => slugs.Contains(i.Slug)).Select(i => i.Slug).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<MenuItemTranslation>> ListItemTranslationsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken) =>
        await db.MenuItemTranslations.AsNoTracking().Where(t => itemIds.Contains(t.EntityId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionGroup>> ListGroupsAsync(CancellationToken cancellationToken) =>
        await db.OptionGroups.AsNoTracking().OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuOption>> ListOptionsAsync(IReadOnlyCollection<int>? groupIds, CancellationToken cancellationToken) =>
        await db.Options.AsNoTracking()
            .Where(o => groupIds == null || groupIds.Contains(o.GroupId))
            .OrderBy(o => o.GroupId).ThenBy(o => o.DisplayOrder).ThenBy(o => o.Id)
            .ToListAsync(cancellationToken);

    public Task<OptionGroup?> FindGroupAsync(int id, CancellationToken cancellationToken) =>
        db.OptionGroups.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<int> CountExistingGroupsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        db.OptionGroups.CountAsync(g => ids.Contains(g.Id), cancellationToken);

    public Task<bool> GroupInUseAsync(int groupId, CancellationToken cancellationToken) =>
        db.MenuItemOptionGroups.AnyAsync(l => l.GroupId == groupId, cancellationToken);

    public Task<int> CountOptionsAsync(int groupId, CancellationToken cancellationToken) =>
        db.Options.CountAsync(o => o.GroupId == groupId, cancellationToken);

    public void AddGroup(OptionGroup group) => db.OptionGroups.Add(group);

    public void RemoveGroup(OptionGroup group) => db.OptionGroups.Remove(group);

    public Task<MenuOption?> FindOptionAsync(int id, CancellationToken cancellationToken) =>
        db.Options.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void AddOption(MenuOption option) => db.Options.Add(option);

    public void RemoveOption(MenuOption option) => db.Options.Remove(option);

    public Task<T?> FindTranslationAsync<T>(int entityId, string lang, CancellationToken cancellationToken)
        where T : MenuTranslation =>
        db.Set<T>().SingleOrDefaultAsync(t => t.EntityId == entityId && t.Lang == lang, cancellationToken);

    public async Task<IReadOnlyList<T>> ListTranslationsAsync<T>(int entityId, CancellationToken cancellationToken)
        where T : MenuTranslation =>
        await db.Set<T>().AsNoTracking().Where(t => t.EntityId == entityId).OrderBy(t => t.Lang).ToListAsync(cancellationToken);

    public void AddTranslation<T>(T translation)
        where T : MenuTranslation => db.Set<T>().Add(translation);

    public async Task<OrderableMenu> LoadOrderableAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && !i.IsDeleted && db.MenuCategories.Any(c => c.Id == i.CategoryId && c.IsActive))
            .ToListAsync(cancellationToken);
        var ids = items.Select(i => i.Id).ToArray();
        var links = await db.MenuItemOptionGroups.AsNoTracking().Where(l => ids.Contains(l.MenuItemId)).ToListAsync(cancellationToken);
        var groupIds = links.Select(l => l.GroupId).Distinct().ToArray();
        var groups = await db.OptionGroups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToListAsync(cancellationToken);
        var options = await db.Options.AsNoTracking().Where(o => groupIds.Contains(o.GroupId)).ToListAsync(cancellationToken);
        return new OrderableMenu(items, links, groups, options);
    }

    public async Task<PublicMenuData> LoadPublicDataAsync(CancellationToken cancellationToken)
    {
        var categories = await db.MenuCategories.AsNoTracking().Where(c => c.IsActive).ToListAsync(cancellationToken);
        var items = await db.MenuItems.AsNoTracking()
            .Where(i => !i.IsDeleted && i.Status != SaleStatus.Hidden && db.MenuCategories.Any(c => c.Id == i.CategoryId && c.IsActive))
            .ToListAsync(cancellationToken);
        var groups = await db.OptionGroups.AsNoTracking().Where(g => g.IsActive).ToListAsync(cancellationToken);
        var options = await db.Options.AsNoTracking()
            .Where(o => o.Status != SaleStatus.Hidden && db.OptionGroups.Any(g => g.Id == o.GroupId && g.IsActive))
            .ToListAsync(cancellationToken);
        var links = await db.MenuItemOptionGroups.AsNoTracking()
            .Where(l => db.OptionGroups.Any(g => g.Id == l.GroupId && g.IsActive))
            .ToListAsync(cancellationToken);

        var translations = new List<MenuTranslation>();
        translations.AddRange(await ApprovedAsync<MenuCategoryTranslation>(cancellationToken));
        translations.AddRange(await ApprovedAsync<MenuItemTranslation>(cancellationToken));
        translations.AddRange(await ApprovedAsync<OptionGroupTranslation>(cancellationToken));
        translations.AddRange(await ApprovedAsync<OptionTranslation>(cancellationToken));

        return new PublicMenuData(categories, items, groups, options, links, translations);
    }

    private async Task<List<T>> ApprovedAsync<T>(CancellationToken cancellationToken)
        where T : MenuTranslation =>
        await db.Set<T>().AsNoTracking().Where(t => t.ReviewStatus == TranslationStatus.Approved).ToListAsync(cancellationToken);
}
