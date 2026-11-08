using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Content;

namespace ObentoTensho.Core.Content.Abstractions;

// PERF-11/12: trang đánh giá công khai cache ngắn theo khóa; Invalidate bỏ mọi trang khi có đánh giá đổi trạng thái/nội dung
public interface IPublicReviewCache
{
    Task<PagedResponse<PublicReviewDto>> GetOrCreateAsync(string key, Func<CancellationToken, Task<PagedResponse<PublicReviewDto>>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
