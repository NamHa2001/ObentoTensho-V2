using Microsoft.EntityFrameworkCore;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Infrastructure.Persistence.Repositories;

internal sealed class NewsRepository(AppDbContext db) : INewsRepository
{
    public async Task<IReadOnlyList<NewsCategory>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken) =>
        await db.NewsCategories.AsNoTracking()
            .Where(c => !activeOnly || c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<NewsCategory?> FindCategoryAsync(int id, CancellationToken cancellationToken) =>
        db.NewsCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> CategorySlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken) =>
        db.NewsCategories.AnyAsync(c => c.Slug == slug && c.Id != exceptId, cancellationToken);

    public Task<bool> CategoryHasNewsAsync(int categoryId, CancellationToken cancellationToken) =>
        db.News.AnyAsync(n => n.CategoryId == categoryId, cancellationToken);

    public void AddCategory(NewsCategory category) => db.NewsCategories.Add(category);

    public void RemoveCategory(NewsCategory category) => db.NewsCategories.Remove(category);

    public async Task<(IReadOnlyList<NewsListRow> Items, int Total)> ListAsync(NewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.News.AsNoTracking()
            .Where(n => n.IsDeleted == query.Trash
                && (query.Status == null || n.Status == query.Status)
                && (query.CategoryId == null || n.CategoryId == query.CategoryId));
        source = Search(source, query.Search);
        return await PageAsync(source.OrderByDescending(n => n.Id), query.Page, query.PageSize, cancellationToken);
    }

    public async Task<(IReadOnlyList<NewsListRow> Items, int Total)> ListPublicAsync(PublicNewsQuery query, DateTime nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var source = db.News.AsNoTracking()
            .Where(n => !n.IsDeleted && n.Status == NewsStatus.Published && n.PublishedAtUtc <= nowUtc);
        if (!string.IsNullOrWhiteSpace(query.CategorySlug))
        {
            source = source.Where(n => db.NewsCategories.Any(c => c.Id == n.CategoryId && c.Slug == query.CategorySlug));
        }

        source = Search(source, query.Search);
        return await PageAsync(source.OrderByDescending(n => n.PublishedAtUtc).ThenByDescending(n => n.Id), query.Page, query.PageSize, cancellationToken);
    }

    public Task<NewsArticle?> FindAsync(int id, bool includeDeleted, CancellationToken cancellationToken) =>
        db.News.SingleOrDefaultAsync(n => n.Id == id && (includeDeleted || !n.IsDeleted), cancellationToken);

    public Task<NewsArticle?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
        db.News.AsNoTracking().SingleOrDefaultAsync(n => n.Slug == slug, cancellationToken);

    public async Task<bool> SlugTakenAsync(string slug, int? exceptId, CancellationToken cancellationToken) =>
        await db.News.AnyAsync(n => n.Slug == slug && n.Id != exceptId, cancellationToken)
        || await db.SlugRedirects.AnyAsync(r => r.EntityType == SlugRedirect.NewsEntity && r.OldSlug == slug && r.EntityId != exceptId, cancellationToken);

    public Task<SlugRedirect?> FindRedirectAsync(string oldSlug, CancellationToken cancellationToken) =>
        db.SlugRedirects.AsNoTracking().SingleOrDefaultAsync(r => r.EntityType == SlugRedirect.NewsEntity && r.OldSlug == oldSlug, cancellationToken);

    public Task RemoveRedirectAsync(string slug, CancellationToken cancellationToken) =>
        db.SlugRedirects.Where(r => r.EntityType == SlugRedirect.NewsEntity && r.OldSlug == slug).ExecuteDeleteAsync(cancellationToken);

    public void AddRedirect(SlugRedirect redirect) => db.SlugRedirects.Add(redirect);

    public void Add(NewsArticle article) => db.News.Add(article);

    public async Task<int> PurgeTrashAsync(DateTime deletedBeforeUtc, CancellationToken cancellationToken)
    {
        var ids = await db.News.Where(n => n.IsDeleted && n.DeletedAtUtc < deletedBeforeUtc).Select(n => n.Id).ToListAsync(cancellationToken);
        await db.SlugRedirects.Where(r => r.EntityType == SlugRedirect.NewsEntity && ids.Contains(r.EntityId)).ExecuteDeleteAsync(cancellationToken);
        return await db.News.Where(n => ids.Contains(n.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    private static IQueryable<NewsArticle> Search(IQueryable<NewsArticle> source, string? search)
    {
        var term = TextNormalizer.Search(search);
        return term.Length > 0 ? source.Where(n => n.TitleSearch.Contains(term)) : source;
    }

    // BR-CNT-44: chiếu sang NewsListRow nên ContentHtml không được nạp
    private static async Task<(IReadOnlyList<NewsListRow> Items, int Total)> PageAsync(IOrderedQueryable<NewsArticle> source, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NewsListRow(n.Id, n.Title, n.Slug, n.Summary, n.ThumbnailImageId, n.CategoryId, n.AuthorDisplayName, n.Status, n.PublishedAtUtc, n.DeletedAtUtc, n.RowVersion))
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
