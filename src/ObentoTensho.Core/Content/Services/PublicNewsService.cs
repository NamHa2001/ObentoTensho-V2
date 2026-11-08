using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Localization.Services;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-01: chỉ bài xuất bản và tới giờ đăng; bài hẹn giờ tự hiện mà không cần job
public sealed class PublicNewsService(INewsRepository news, IImageRepository images, ImageMapper imageMapper, ContentLocalizer localizer, IClock clock)
{
    public const int MaxPageSize = 50;

    public async Task<IReadOnlyList<NewsCategoryDto>> CategoriesAsync(CancellationToken cancellationToken) =>
        (await news.ListCategoriesAsync(activeOnly: true, cancellationToken))
            .Select(c => new NewsCategoryDto(c.Id, c.Name, c.Slug, c.DisplayOrder, c.IsActive, RowVersions.Encode(c.RowVersion)))
            .ToArray();

    // Thẻ bài dịch tiêu đề và tóm tắt; tìm kiếm vẫn theo tiêu đề tiếng Việt không dấu
    public async Task<PagedResponse<PublicNewsListItemDto>> ListAsync(PublicNewsQuery query, string? lang, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await news.ListPublicAsync(query with { Page = page, PageSize = pageSize }, clock.UtcNow, cancellationToken);
        var slugs = await CategorySlugsAsync(cancellationToken);
        var loaded = await images.LoadAsync(items.Select(n => n.ThumbnailImageId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        var translations = await localizer.ForAsync(TranslatableContent.News, lang, cancellationToken);
        var full = await FullTextAsync(translations, items.Select(n => n.Id).ToArray(), cancellationToken);
        return new PagedResponse<PublicNewsListItemDto>(
            items.Select(n =>
            {
                var text = full.TryGetValue(n.Id, out var source) ? translations.Apply(n.Id, source) : new LocalizedText(new SourceText(n.Title, n.Summary), ContentLanguages.Source, false);
                return new PublicNewsListItemDto(
                    n.Slug,
                    text.Text.Title ?? n.Title,
                    text.Text.Summary,
                    Image(n.ThumbnailImageId, loaded),
                    n.CategoryId is { } c ? slugs.GetValueOrDefault(c) : null,
                    n.AuthorDisplayName,
                    n.PublishedAtUtc ?? default,
                    text.ContentLang,
                    text.IsFallback);
            }).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<NewsLookup>> FindAsync(string slug, string? lang, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (await news.FindBySlugAsync(slug, cancellationToken) is { } article && article.IsVisible(now))
        {
            var translations = await localizer.ForAsync(TranslatableContent.News, lang, cancellationToken);
            return Result.Success(new NewsLookup(await ToDtoAsync(article, translations.Apply(article.Id, ContentSources.Of(article)), cancellationToken), null));
        }

        if (await news.FindRedirectAsync(slug, cancellationToken) is { } redirect
            && await news.FindAsync(redirect.EntityId, includeDeleted: false, cancellationToken) is { } target
            && target.IsVisible(now))
        {
            return Result.Success(new NewsLookup(null, target.Slug));
        }

        return Result.Failure<NewsLookup>(ErrorCodes.NotFound);
    }

    private async Task<PublicNewsDto> ToDtoAsync(NewsArticle n, LocalizedText text, CancellationToken cancellationToken)
    {
        var slugs = await CategorySlugsAsync(cancellationToken);
        var loaded = await images.LoadAsync(new[] { n.ThumbnailImageId, n.OgImageId }.OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return new PublicNewsDto(
            n.Slug,
            text.Text.Title ?? n.Title,
            text.Text.Summary,
            Image(n.ThumbnailImageId, loaded),
            text.Text.BodyHtml ?? n.ContentHtml,
            n.CategoryId is { } c ? slugs.GetValueOrDefault(c) : null,
            n.AuthorDisplayName,
            n.PublishedAtUtc ?? default,
            text.Text.MetaTitle,
            text.Text.MetaDescription,
            Image(n.OgImageId, loaded),
            text.ContentLang,
            text.IsFallback);
    }

    // Kiểm bản dịch còn khớp phải băm đủ các trường của bản Việt; dòng danh sách không có nội dung bài nên đọc riêng
    private async Task<IReadOnlyDictionary<int, SourceText>> FullTextAsync(TranslationSet translations, int[] ids, CancellationToken cancellationToken)
    {
        if (ReferenceEquals(translations, TranslationSet.Source) || ids.Length == 0)
        {
            return new Dictionary<int, SourceText>();
        }

        var articles = await news.ListByIdsAsync(ids, cancellationToken);
        return articles.ToDictionary(a => a.Id, ContentSources.Of);
    }

    private async Task<Dictionary<int, string>> CategorySlugsAsync(CancellationToken cancellationToken) =>
        (await news.ListCategoriesAsync(activeOnly: false, cancellationToken)).ToDictionary(c => c.Id, c => c.Slug);

    private Contracts.Dtos.Media.ImageDto? Image(Guid? id, IReadOnlyDictionary<Guid, StoredImage> loaded) =>
        id is { } imageId && loaded.TryGetValue(imageId, out var image) ? imageMapper.ToDto(image) : null;
}
