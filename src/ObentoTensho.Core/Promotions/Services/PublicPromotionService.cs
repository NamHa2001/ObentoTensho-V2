using ObentoTensho.Contracts.Dtos.Promotions;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Localization.Domain;
using ObentoTensho.Core.Localization.Services;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Domain;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Promotions.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Vouchers.Domain;

namespace ObentoTensho.Core.Promotions.Services;

// BR-VCH-16/17: chỉ trả khuyến mãi đang chạy, hoặc sắp diễn ra có cờ PublicTeaser; không có tham số nào lấy được bản nháp/đã tắt
public sealed class PublicPromotionService(
    IPromotionRepository promotions,
    IPromotionCache cache,
    ContentLocalizer localizer,
    IImageRepository images,
    ImageMapper imageMapper,
    SlugHistory slugs,
    IClock clock)
{
    public async Task<IReadOnlyList<PublicPromotionDto>> ListAsync(string? lang, CancellationToken cancellationToken) =>
        (await VisibleAsync(lang, cancellationToken)).ToArray();

    public async Task<Result<PublicPromotionDto>> GetAsync(string slug, string? lang, CancellationToken cancellationToken) =>
        (await VisibleAsync(lang, cancellationToken)).FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase)) is { } promotion
            ? Result.Success(promotion)
            : Result.Failure<PublicPromotionDto>(ErrorCodes.NotFound);

    // BR-CNT-41: slug cũ của khuyến mãi còn hiển thị ⇒ slug hiện tại để trả 301
    public async Task<string?> CurrentSlugAsync(string oldSlug, CancellationToken cancellationToken) =>
        await slugs.TargetAsync(SlugRedirect.PromotionEntity, oldSlug, cancellationToken) is { } id
            ? (await LoadAsync(cancellationToken)).FirstOrDefault(e => e.Promotion.Id == id && (e.Promotion.IsRunning(clock.TodayLocal) || e.Promotion.IsUpcoming(clock.TodayLocal)))?.Promotion.Slug
            : null;

    // Ảnh banner đọc theo yêu cầu (vài mã) để thấy ngay ảnh vừa xử lý xong; chỉ ảnh đã sẵn sàng
    private async Task<IEnumerable<PublicPromotionDto>> VisibleAsync(string? lang, CancellationToken cancellationToken)
    {
        var entries = await LoadAsync(cancellationToken);
        var banners = await images.LoadAsync(entries.Select(e => e.Promotion.BannerImageId).OfType<Guid>().Distinct().ToArray(), cancellationToken);
        return Visible(entries, await localizer.ForAsync(TranslatableContent.Promotion, lang, cancellationToken), banners);
    }

    private Task<IReadOnlyList<PublicPromotionEntry>> LoadAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ct => promotions.LoadPublicAsync(clock.TodayLocal, ct), cancellationToken);

    private IEnumerable<PublicPromotionDto> Visible(IReadOnlyList<PublicPromotionEntry> entries, TranslationSet translations, IReadOnlyDictionary<Guid, StoredImage> banners)
    {
        var today = clock.TodayLocal;
        var now = clock.UtcNow;
        return entries
            .Where(e => e.Promotion.IsRunning(today) || e.Promotion.IsUpcoming(today))
            .OrderBy(e => e.Promotion.StartDate)
            .Select(e =>
            {
                var text = translations.Apply(e.Promotion.Id, ContentSources.Of(e.Promotion));
                return new PublicPromotionDto(
                    e.Promotion.Slug,
                    text.Text.Title ?? e.Promotion.Title,
                    text.Text.BodyHtml,
                    e.Promotion.StartDate,
                    e.Promotion.EndDate,
                    e.Promotion.IsUpcoming(today),
                    e.Promotion.IsRunning(today) ? e.Vouchers.Where(v => Usable(v, now)).Select(v => v.Code).ToArray() : [],
                    e.Promotion.BannerImageId is { } bannerId && banners.TryGetValue(bannerId, out var banner) && banner.Status == ImageStatus.Ready ? imageMapper.ToDto(banner) : null,
                    text.ContentLang,
                    text.IsFallback);
            });
    }

    private static bool Usable(Voucher voucher, DateTime nowUtc) =>
        (voucher.StartUtc is not { } start || start <= nowUtc)
        && (voucher.EndUtc is not { } end || nowUtc < end)
        && (voucher.MaxUsageTotal is not { } max || voucher.UsedCount < max);
}
