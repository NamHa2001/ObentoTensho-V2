using ObentoTensho.Core.Media.Domain;

namespace ObentoTensho.Core.Media.Abstractions;

public interface IImageRepository
{
    Task<StoredImage?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<StoredImage?> FindByHashAsync(string contentHash, CancellationToken cancellationToken);

    // Không theo dõi, cho DTO
    Task<IReadOnlyDictionary<Guid, StoredImage>> LoadAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> ListProcessingAsync(int limit, CancellationToken cancellationToken);

    // FR-IMG-08: ảnh lỗi còn lượt thử lại và đã qua thời gian chờ (bản được theo dõi)
    Task<IReadOnlyList<StoredImage>> ListRetryableAsync(int maxAttempts, DateTime failedBeforeUtc, int limit, CancellationToken cancellationToken);

    // BR-CNT-32: ảnh tạo trước mốc, không còn cột nào trỏ tới (banner, gallery, tin, món, danh mục, khuyến mãi, cài đặt)
    Task<IReadOnlyList<StoredImage>> ListUnlinkedAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken);

    // Ảnh còn được chèn trong nội dung HTML (tin, trang tĩnh và các phiên bản, bản tin, mô tả, bản dịch)
    Task<bool> IsUsedInHtmlAsync(Guid imageId, CancellationToken cancellationToken);

    Task DeleteAsync(Guid imageId, CancellationToken cancellationToken);

    void Add(StoredImage image);
}
