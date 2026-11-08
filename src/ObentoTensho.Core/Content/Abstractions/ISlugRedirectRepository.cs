using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

// BR-CNT-41: chuyển hướng slug cũ dùng chung cho món, khuyến mãi (tin tức có kho riêng trong INewsRepository)
public interface ISlugRedirectRepository
{
    Task<SlugRedirect?> FindAsync(string entityType, string oldSlug, CancellationToken cancellationToken);

    Task RemoveAsync(string entityType, string slug, CancellationToken cancellationToken);

    void Add(SlugRedirect redirect);
}
