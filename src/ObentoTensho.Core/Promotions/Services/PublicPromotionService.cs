using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Promotions.Services;

// BR-VCH-16/17: chỉ trả khuyến mãi đang chạy, hoặc sắp diễn ra có cờ PublicTeaser; không có tham số nào lấy được bản nháp/đã tắt
public sealed class PublicPromotionService(IPromotionRepository promotions, IPromotionCache cache, IClock clock)
{
    public async Task<IReadOnlyList<PublicPromotionDto>> ListAsync(CancellationToken cancellationToken) =>
        Visible(await LoadAsync(cancellationToken)).ToArray();

    public async Task<Result<PublicPromotionDto>> GetAsync(string slug, CancellationToken cancellationToken) =>
        Visible(await LoadAsync(cancellationToken)).FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)) is { } promotion
            ? Result.Success(promotion)
            : Result.Failure<PublicPromotionDto>(ErrorCodes.NotFound);

    private Task<IReadOnlyList<PublicPromotionEntry>> LoadAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ct => promotions.LoadPublicAsync(clock.TodayLocal, ct), cancellationToken);

    private IEnumerable<PublicPromotionDto> Visible(IReadOnlyList<PublicPromotionEntry> entries)
    {
        var today = clock.TodayLocal;
        var now = clock.UtcNow;
        return entries
            .Where(e => e.Promotion.IsRunning(today) || e.Promotion.IsUpcoming(today))
            .OrderBy(e => e.Promotion.StartDate)
            .Select(e => new PublicPromotionDto(
                e.Promotion.Slug,
                e.Promotion.Title,
                e.Promotion.DescriptionHtml,
                e.Promotion.StartDate,
                e.Promotion.EndDate,
                e.Promotion.IsUpcoming(today),
                e.Promotion.IsRunning(today) ? e.Vouchers.Where(v => Usable(v, now)).Select(v => v.Code).ToArray() : []));
    }

    private static bool Usable(Voucher voucher, DateTime nowUtc) =>
        (voucher.StartUtc is not { } start || start <= nowUtc)
        && (voucher.EndUtc is not { } end || nowUtc < end)
        && (voucher.MaxUsageTotal is not { } max || voucher.UsedCount < max);
}
