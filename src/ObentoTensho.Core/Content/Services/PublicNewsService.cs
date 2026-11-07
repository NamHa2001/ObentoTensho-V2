using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-01: chỉ bài xuất bản và tới giờ đăng; bài hẹn giờ tự hiện mà không cần job
public sealed class PublicNewsService(INewsRepository news, IImageRepository images, ImageMapper imageMapper, IClock clock)
{
    public const int MaxPageSize = 50;

    public async Task<IReadOnlyList<NewsCategoryDto>> CategoriesAsync(CancellationToken cancellationToken) =>
        (await news.ListCategoriesAsync(activeOnly: true, cancellationToken))
            .Select(c => new NewsCategoryDto(c.Id, c.Name, c.Slug, c.DisplayOrder, c.IsActive, RowVersions.Encode(c.RowVersion)))
            .ToArray();

    public async Task<PagedResponse<PublicNewsListItemDto>> ListAsync(PublicNewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await news.ListPublicAsync(query with { Page = page, PageSize = pageSize }, clock.UtcNow, cancellationToken);
        var slugs = await CategorySlugsAsync(cancellationToken);
        var loaded = await images.LoadAsync(items.Select(n => n.ThumbnailImageId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return new PagedResponse<PublicNewsListItemDto>(
            items.Select(n => new PublicNewsListItemDto(
                n.Slug,
                n.Title,
                n.Summary,
                Image(n.ThumbnailImageId, loaded),
                n.CategoryId is { } c ? slugs.GetValueOrDefault(c) : null,
                n.AuthorDisplayName,
                n.PublishedAtUtc ?? default)).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<NewsLookup>> FindAsync(string slug, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (await news.FindBySlugAsync(slug, cancellationToken) is { } article && article.IsVisible(now))
        {
            return Result.Success(new NewsLookup(await ToDtoAsync(article, cancellationToken), null));
        }

        if (await news.FindRedirectAsync(slug, cancellationToken) is { } redirect
            && await news.FindAsync(redirect.EntityId, includeDeleted: false, cancellationToken) is { } target
            && target.IsVisible(now))
        {
            return Result.Success(new NewsLookup(null, target.Slug));
        }

        return Result.Failure<NewsLookup>(ErrorCodes.NotFound);
    }

    private async Task<PublicNewsDto> ToDtoAsync(NewsArticle n, CancellationToken cancellationToken)
    {
        var slugs = await CategorySlugsAsync(cancellationToken);
        var loaded = await images.LoadAsync(new[] { n.ThumbnailImageId, n.OgImageId }.OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return new PublicNewsDto(
            n.Slug,
            n.Title,
            n.Summary,
            Image(n.ThumbnailImageId, loaded),
            n.ContentHtml,
            n.CategoryId is { } c ? slugs.GetValueOrDefault(c) : null,
            n.AuthorDisplayName,
            n.PublishedAtUtc ?? default,
            n.MetaTitle,
            n.MetaDescription,
            Image(n.OgImageId, loaded));
    }

    private async Task<Dictionary<int, string>> CategorySlugsAsync(CancellationToken cancellationToken) =>
        (await news.ListCategoriesAsync(activeOnly: false, cancellationToken)).ToDictionary(c => c.Id, c => c.Slug);

    private Contracts.Dtos.Media.ImageDto? Image(Guid? id, IReadOnlyDictionary<Guid, StoredImage> loaded) =>
        id is { } imageId && loaded.TryGetValue(imageId, out var image) ? imageMapper.ToDto(image) : null;
}
