using ObentoTensho.Contracts.Dtos.Localization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Abstractions;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Menu.Abstractions;
using ObentoTensho.Core.Menu.Services;

namespace ObentoTensho.Core.Localization.Services;

// docs/srs/22 §4.3 "Tiến độ dịch" và BR-I18N-06: chỉ bật một ngôn ngữ khi mọi món/danh mục/tùy chọn khách đang thấy
// và các trang tĩnh chính đều có bản dịch đã duyệt, còn khớp bản Việt. Mẫu email đủ 3 ngôn ngữ được kiểm bằng test.
public sealed class TranslationProgressService(
    IMenuRepository menu,
    ContentSources sources,
    ITranslationRepository translations,
    ContentLanguages languages)
{
    public static readonly IReadOnlyList<string> RequiredPages = ["about", "contact", "privacy", "terms", "policy"];

    private static readonly (TranslatableContent Kind, string Name)[] ContentKinds =
    [
        (TranslatableContent.Promotion, "promotion"),
        (TranslatableContent.Banner, "banner"),
        (TranslatableContent.Gallery, "gallery"),
        (TranslatableContent.News, "news"),
        (TranslatableContent.StaticPage, "static_page"),
    ];

    public async Task<TranslationProgressDto?> ProgressAsync(string lang, CancellationToken cancellationToken)
    {
        if (!ContentLanguages.Targets.Contains(lang, StringComparer.Ordinal))
        {
            return null;
        }

        var items = await CoverageAsync(lang, cancellationToken);
        var kinds = items.GroupBy(i => i.Kind, StringComparer.Ordinal)
            .Select(g => new TranslationKindProgressDto(
                g.Key,
                g.Count(),
                g.Count(i => i.State == ContentTranslationState.Approved),
                g.Count(i => i.State == ContentTranslationState.Outdated),
                g.Count(i => i.State == ContentTranslationState.Draft),
                g.Count(i => i.State == ContentTranslationState.Missing)))
            .ToArray();
        var blocking = Blocking(items);
        return new TranslationProgressDto(lang, languages.Enabled().Contains(lang, StringComparer.Ordinal), blocking.Length == 0, kinds, blocking);
    }

    // Danh sách còn thiếu để bật ngôn ngữ; rỗng ⇒ bật được
    public async Task<IReadOnlyList<string>> MissingForEnableAsync(string lang, CancellationToken cancellationToken) =>
        ContentLanguages.Targets.Contains(lang, StringComparer.Ordinal) ? Blocking(await CoverageAsync(lang, cancellationToken)) : [];

    private static string[] Blocking(IReadOnlyList<CoverageItem> items) =>
        items.Where(i => i.State != ContentTranslationState.Approved
                && (i.Kind.StartsWith("menu_", StringComparison.Ordinal) || i.Kind.StartsWith("option", StringComparison.Ordinal)
                    || (string.Equals(i.Kind, "static_page", StringComparison.Ordinal) && RequiredPages.Contains(i.Ref, StringComparer.Ordinal))))
            .Select(i => $"{i.Kind}:{i.Ref}")
            .ToArray();

    private async Task<IReadOnlyList<CoverageItem>> CoverageAsync(string lang, CancellationToken cancellationToken)
    {
        var items = new List<CoverageItem>(MenuTranslationCoverage.For(await menu.LoadPublicDataAsync(cancellationToken), await menu.ListTranslationsByLangAsync(lang, cancellationToken)));
        foreach (var (kind, name) in ContentKinds)
        {
            var existing = (await translations.ListAsync(kind, lang, cancellationToken)).ToDictionary(t => t.EntityId);
            foreach (var (id, reference, text) in await sources.VisibleAsync(kind, cancellationToken))
            {
                if (kind == TranslatableContent.StaticPage && !StaticPage.AllowedSlugs.Contains(reference, StringComparer.Ordinal))
                {
                    continue;
                }

                var t = existing.GetValueOrDefault(id);
                var hash = text.Hash;
                items.Add(new CoverageItem(name, reference, hash, CoverageItem.StateOf(hash, t?.SourceHash, t?.ReviewStatus)));
            }
        }

        return items;
    }
}
