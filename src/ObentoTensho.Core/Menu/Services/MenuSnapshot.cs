using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// Thực đơn công khai dựng sẵn trong bộ nhớ (PERF-10). Bản dịch chỉ giữ bản đã duyệt và còn khớp bản Việt
// hiện tại (BR-I18N-02/03); thiếu thì trả tiếng Việt kèm cờ dự phòng (BR-I18N-07).
public sealed class MenuSnapshot
{
    public const string SourceLang = "vi";

    private readonly Dictionary<(Type Kind, int Id, string Lang), MenuTranslation> _translations;
    private readonly ILookup<int, MenuOption> _optionsByGroup;
    private readonly Dictionary<int, OptionGroup> _groups;
    private readonly ILookup<int, MenuItemOptionGroup> _itemGroups;

    public MenuSnapshot(PublicMenuData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Categories = data.Categories.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToArray();
        var activeCategories = Categories.Select(c => c.Id).ToHashSet();
        Items = data.Items.Where(i => activeCategories.Contains(i.CategoryId)).ToArray();
        _groups = data.Groups.ToDictionary(g => g.Id);
        _optionsByGroup = data.Options.OrderBy(o => o.DisplayOrder).ThenBy(o => o.Id).ToLookup(o => o.GroupId);
        _itemGroups = data.ItemGroups.OrderBy(l => l.DisplayOrder).ToLookup(l => l.MenuItemId);
        _translations = data.Translations
            .Where(t => t.IsDisplayable(CurrentHash(t)))
            .ToDictionary(t => (t.GetType(), t.EntityId, t.Lang));
    }

    public IReadOnlyList<MenuCategory> Categories { get; }

    public IReadOnlyList<MenuItem> Items { get; }

    public IEnumerable<OptionGroup> GroupsOf(int itemId) =>
        _itemGroups[itemId].Select(l => _groups.GetValueOrDefault(l.GroupId)).OfType<OptionGroup>();

    public IEnumerable<MenuOption> OptionsOf(int groupId) => _optionsByGroup[groupId];

    // Dị ứng của món gồm cả tùy chọn mặc định (BR-MNU-24)
    public IReadOnlyList<Allergen> AllergensOf(MenuItem item) =>
        MenuRules.ParseAllergens(item.Allergens)
            .Concat(GroupsOf(item.Id).SelectMany(g => OptionsOf(g.Id)).Where(o => o.IsDefault).SelectMany(o => MenuRules.ParseAllergens(o.Allergens)))
            .Distinct()
            .Order()
            .ToArray();

    public T? Translation<T>(int entityId, string lang)
        where T : MenuTranslation =>
        string.Equals(lang, SourceLang, StringComparison.Ordinal) ? null : _translations.GetValueOrDefault((typeof(T), entityId, lang)) as T;

    private string CurrentHash(MenuTranslation translation) => translation switch
    {
        MenuItemTranslation t => Items.FirstOrDefault(i => i.Id == t.EntityId) is { } item ? ItemHash(item) : string.Empty,
        MenuCategoryTranslation t => Categories.FirstOrDefault(c => c.Id == t.EntityId) is { } c ? MenuRules.SourceHash(c.Name) : string.Empty,
        OptionGroupTranslation t => _groups.GetValueOrDefault(t.EntityId) is { } g ? MenuRules.SourceHash(g.Name) : string.Empty,
        OptionTranslation t => _optionsByGroup.SelectMany(o => o).FirstOrDefault(o => o.Id == t.EntityId) is { } o ? MenuRules.SourceHash(o.Name) : string.Empty,
        _ => string.Empty,
    };

    public static string ItemHash(MenuItem item) => MenuRules.SourceHash(item.NameVi, item.ShortDesc, item.FullDescHtml);
}
