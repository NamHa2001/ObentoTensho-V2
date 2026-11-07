namespace ObentoTensho.Core.Promotions.Abstractions;

// PERF-12: danh sách khuyến mãi công khai cache 5 phút, xóa ngay khi quản trị sửa
public interface IPromotionCache
{
    Task<IReadOnlyList<PublicPromotionEntry>> GetOrCreateAsync(Func<CancellationToken, Task<IReadOnlyList<PublicPromotionEntry>>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
