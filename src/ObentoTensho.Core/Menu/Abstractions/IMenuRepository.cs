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

    // Nhập/xuất Excel (BR-MNU-14)
    Task<IReadOnlyList<MenuItem>> ListAllItemsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItem>> FindItemsBySlugAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken);

    // Slug đã dùng, kể cả món đã xóa mềm
    Task<IReadOnlySet<string>> TakenItemSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItemTranslation>> ListItemTranslationsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<OptionGroup>> ListGroupsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuOption>> ListOptionsAsync(IReadOnlyCollection<int>? groupIds, CancellationToken cancellationToken);

    Task<OptionGroup?> FindGroupAsync(int id, CancellationToken cancellationToken);

    Task<int> CountExistingGroupsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);

    Task<bool> GroupInUseAsync(int groupId, CancellationToken cancellationToken);

    // BR-MNU-23: tùy chọn đã xuất hiện trong đơn nào chưa
    Task<bool> AnyOptionOrderedAsync(IReadOnlyCollection<int> optionIds, CancellationToken cancellationToken);

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

    // Đặt món (BR-ORD-02): đọc thẳng CSDL, không qua cache
    Task<OrderableMenu> LoadOrderableAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken);

    // Dữ liệu trang thực đơn công khai: chỉ thứ khách được thấy (BR-MNU-02), đọc một lần rồi cache
    Task<PublicMenuData> LoadPublicDataAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuItemImage>> ListItemImagesAsync(int itemId, CancellationToken cancellationToken);

    // Thay toàn bộ ảnh phụ theo thứ tự gửi lên, trong transaction của bên gọi
    Task ReplaceItemImagesAsync(int itemId, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken);

    // Mọi bản dịch (kể cả nháp) của một ngôn ngữ — màn hình tiến độ dịch
    Task<IReadOnlyList<MenuTranslation>> ListTranslationsByLangAsync(string lang, CancellationToken cancellationToken);
}
