using ObentoTensho.Core.Promotions.Domain;

namespace ObentoTensho.Core.Promotions.Abstractions;

public interface IPromotionRepository
{
    // Chưa xóa, đang bật, chưa kết thúc tính tới hôm nay
    Task<IReadOnlyList<PublicPromotionEntry>> LoadPublicAsync(DateOnly today, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Promotion> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);

    // Bản được theo dõi để sửa; không gồm bản đã xóa mềm
    Task<Promotion?> FindAsync(int id, CancellationToken cancellationToken);

    // Kể cả bản đã xóa mềm (chỉ mục UNIQUE)
    Task<bool> SlugExistsAsync(string slug, int? exceptId, CancellationToken cancellationToken);

    void Add(Promotion promotion);
}
