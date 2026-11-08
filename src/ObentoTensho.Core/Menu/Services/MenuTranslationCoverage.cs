using System.Globalization;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Domain;

namespace ObentoTensho.Core.Menu.Services;

// BR-I18N-06 (2): món khách đang thấy, danh mục đang bật, nhóm và tùy chọn gắn với các món đó
public static class MenuTranslationCoverage
{
    public static IReadOnlyList<CoverageItem> For(PublicMenuData data, IReadOnlyList<MenuTranslation> translations)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(translations);
        var snapshot = new MenuSnapshot(data);
        var existing = translations.ToDictionary(t => (t.GetType(), t.EntityId));
        var groups = snapshot.Items.SelectMany(i => snapshot.GroupsOf(i.Id)).DistinctBy(g => g.Id).ToArray();
        var items = new List<CoverageItem>();
        items.AddRange(snapshot.Items.Select(i => Item<MenuItemTranslation>("menu_item", i.Id, MenuSnapshot.ItemHash(i), existing)));
        items.AddRange(snapshot.Categories.Select(c => Item<MenuCategoryTranslation>("menu_category", c.Id, MenuRules.SourceHash(c.Name), existing)));
        items.AddRange(groups.Select(g => Item<OptionGroupTranslation>("option_group", g.Id, MenuRules.SourceHash(g.Name), existing)));
        items.AddRange(groups.SelectMany(g => snapshot.OptionsOf(g.Id)).DistinctBy(o => o.Id)
            .Select(o => Item<OptionTranslation>("option", o.Id, MenuRules.SourceHash(o.Name), existing)));
        return items;
    }

    private static CoverageItem Item<T>(string kind, int id, string hash, Dictionary<(Type, int), MenuTranslation> existing)
        where T : MenuTranslation
    {
        var t = existing.GetValueOrDefault((typeof(T), id));
        return new CoverageItem(kind, id.ToString(CultureInfo.InvariantCulture), hash, CoverageItem.StateOf(hash, t?.SourceHash, t?.ReviewStatus));
    }
}
