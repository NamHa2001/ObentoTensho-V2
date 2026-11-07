using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Content.Domain;

namespace ObentoTensho.Core.Content.Abstractions;

public interface IReviewRepository
{
    // null: đơn không tồn tại hoặc không phải của người này
    Task<ReviewOrderFacts?> FindOrderAsync(Guid orderId, Guid userId, CancellationToken cancellationToken);

    Task<int> CountCreatedSinceAsync(Guid userId, DateTime sinceUtc, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid userId, Guid orderId, int? menuItemId, CancellationToken cancellationToken);

    Task<Review?> FindAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Review>> ListMineAsync(Guid userId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ReviewRow> Items, int Total)> ListAsync(ReviewStatus? status, int? menuItemId, int page, int pageSize, CancellationToken cancellationToken);

    void Add(Review review);

    void Remove(Review review);
}
