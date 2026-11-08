using System.Globalization;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Promotions.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Localization.Services;

// Bản tiếng Việt của từng loại nội dung: một mục theo khóa (id; trang tĩnh theo slug) hoặc các mục đang hiển thị cho khách
public sealed class ContentSources(
    IPromotionRepository promotions,
    IContentRepository content,
    INewsRepository news,
    IStaticPageRepository pages,
    IClock clock)
{
    public async Task<(int Id, SourceText Text)?> FindAsync(TranslatableContent kind, string key, CancellationToken cancellationToken)
    {
        if (kind == TranslatableContent.StaticPage)
        {
            return StaticPage.AllowedSlugs.Contains(key, StringComparer.Ordinal) && await pages.FindAsync(key, cancellationToken) is { } page
                ? (page.Id, Of(page))
                : null;
        }

        if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        return kind switch
        {
            TranslatableContent.Promotion => await promotions.FindAsync(id, cancellationToken) is { DeletedAtUtc: null } p ? (p.Id, Of(p)) : null,
            TranslatableContent.Banner => await content.FindBannerAsync(id, cancellationToken) is { } b ? (b.Id, Of(b)) : null,
            TranslatableContent.Gallery => await content.FindGalleryItemAsync(id, cancellationToken) is { Caption: not null } g ? (g.Id, Of(g)) : null,
            _ => await news.FindAsync(id, includeDeleted: false, cancellationToken) is { } n ? (n.Id, Of(n)) : null,
        };
    }

    // Các mục khách đang thấy (để đếm tiến độ dịch)
    public async Task<IReadOnlyList<(int Id, string Ref, SourceText Text)>> VisibleAsync(TranslatableContent kind, CancellationToken cancellationToken) => kind switch
    {
        TranslatableContent.Promotion => (await promotions.LoadPublicAsync(clock.TodayLocal, cancellationToken))
            .Select(e => (e.Promotion.Id, Ref(e.Promotion.Id), Of(e.Promotion))).ToArray(),
        TranslatableContent.Banner => (await content.ListBannersAsync(activeOnly: true, cancellationToken))
            .Where(b => b.IsVisible(clock.TodayLocal)).Select(b => (b.Id, Ref(b.Id), Of(b))).ToArray(),
        TranslatableContent.Gallery => (await content.ListGalleryAsync(activeOnly: true, category: null, cancellationToken))
            .Where(g => g.Caption is not null).Select(g => (g.Id, Ref(g.Id), Of(g))).ToArray(),
        TranslatableContent.News => (await news.ListVisibleAsync(clock.UtcNow, cancellationToken))
            .Select(n => (n.Id, Ref(n.Id), Of(n))).ToArray(),
        _ => (await pages.ListAsync(cancellationToken)).Select(p => (p.Id, p.Slug, Of(p))).ToArray(),
    };

    public static SourceText Of(Promotion p) => new(p.Title, BodyHtml: p.DescriptionHtml);

    public static SourceText Of(Banner b) => new(b.Title, b.Subtitle, CtaText: b.CtaText);

    public static SourceText Of(GalleryItem g) => new(g.Caption);

    public static SourceText Of(NewsArticle n) => new(n.Title, n.Summary, n.ContentHtml, MetaTitle: n.MetaTitle, MetaDescription: n.MetaDescription);

    public static SourceText Of(StaticPage p) => new(p.Title, BodyHtml: p.ContentHtml);

    private static string Ref(int id) => id.ToString(CultureInfo.InvariantCulture);
}
