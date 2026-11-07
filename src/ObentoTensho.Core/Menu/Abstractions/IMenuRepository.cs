using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Abstractions;

// Hàm "Find…" trả bản được theo dõi để sửa; "List…" trả bản chỉ đọc
public interface IMenuRepository
{
    Task<IReadOnlyList<MenuCategory>> ListCategoriesAsync(CancellationToken cancellationToken);

    Task<MenuCategory?> FindCategoryAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuCategory>> FindCategoriesAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    Task<bool> CategorySlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken);

    // BR-MNU-08: tính cả món đã xóa mềm
    Task<bool> CategoryHasItemsAsync(int categoryId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, int>> CountItemsByCategoryAsync(CancellationToken cancellationToken);

    void AddCategory(MenuCategory category);

    void RemoveCategory(MenuCategory category);

    Task<(IReadOnlyList<MenuItem> Items, int Total)> ListItemsAsync(MenuItemQuery query, CancellationToken cancellationToken);

    Task<MenuItem?> FindItemAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> FindItemsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    // Kể cả món đã xóa mềm (chỉ mục UNIQUE)
    Task<bool> ItemSlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> GetItemGroupIdsAsync(int itemId, CancellationToken cancellationToken);

    Task ReplaceItemGroupsAsync(int itemId, IReadOnlyList<int> groupIds, CancellationToken cancellationToken);

    void AddItem(MenuItem item);

    Task<IReadOnlyList<OptionGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuOption>> ListOptionsAsync(IReadOnlyCollection<int>? groupIds, CancellationToken cancellationToken);

    Task<OptionGroup?> FindGroupAsync(int id, CancellationToken cancellationToken);

    Task<int> CountExistingGroupsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    Task<bool> GroupInUseAsync(int groupId, CancellationToken cancellationToken);

    Task<int> CountOptionsAsync(int groupId, CancellationToken cancellationToken);

    void AddGroup(OptionGroup group);

    void RemoveGroup(OptionGroup group);

    Task<MenuOption?> FindOptionAsync(int id, CancellationToken cancellationToken);

    void AddOption(MenuOption option);

    void RemoveOption(MenuOption option);

    Task<T?> FindTranslationAsync<T>(int entityId, string lang, CancellationToken cancellationToken)
        where T : MenuTranslation;

    Task<IReadOnlyList<T>> ListTranslationsAsync<T>(int entityId, CancellationToken cancellationToken)
        where T : MenuTranslation;

    void AddTranslation<T>(T translation)
        where T : MenuTranslation;

    // Dữ liệu trang thực đơn công khai: chỉ thứ khách được thấy (BR-MNU-02), đọc một lần rồi cache
    Task<PublicMenuData> LoadPublicDataAsync(CancellationToken cancellationToken);
}
