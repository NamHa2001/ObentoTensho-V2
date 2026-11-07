using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface INewsRepository
{
    Task<IReadOnlyList<NewsCategory>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken);

    Task<NewsCategory?> FindCategoryAsync(int id, CancellationToken cancellationToken);

    Task<bool> CategorySlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken);

    // BR-CNT-42: tính cả bài trong thùng rác
    Task<bool> CategoryHasNewsAsync(int categoryId, CancellationToken cancellationToken);

    void AddCategory(NewsCategory category);

    void RemoveCategory(NewsCategory category);

    Task<(IReadOnlyList<NewsListRow> Items, int Total)> ListAsync(NewsQuery query, CancellationToken cancellationToken);

    Task<(IReadOnlyList<NewsListRow> Items, int Total)> ListPublicAsync(PublicNewsQuery query, DateTime nowUtc, CancellationToken cancellationToken);

    // Bản được theo dõi để sửa; includeDeleted để khôi phục từ thùng rác
    Task<NewsArticle?> FindAsync(int id, bool includeDeleted, CancellationToken cancellationToken);

    Task<NewsArticle?> FindBySlugAsync(string slug, CancellationToken cancellationToken);

    // Slug đã dùng: bài khác (kể cả thùng rác) hoặc chuyển hướng của bài khác
    Task<bool> SlugTakenAsync(string slug, int? exceptId, CancellationToken cancellationToken);

    Task<SlugRedirect?> FindRedirectAsync(string oldSlug, CancellationToken cancellationToken);

    Task RemoveRedirectAsync(string slug, CancellationToken cancellationToken);

    void AddRedirect(SlugRedirect redirect);

    void Add(NewsArticle article);

    // BR-CNT-42: xóa hẳn bài đã nằm thùng rác quá hạn, kèm chuyển hướng của bài đó
    Task<int> PurgeTrashAsync(DateTime deletedBeforeUtc, CancellationToken cancellationToken);
}
