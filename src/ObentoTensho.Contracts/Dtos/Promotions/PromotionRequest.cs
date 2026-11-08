namespace ObentoTensho.Contracts.Dtos.Promotions;

/// <summary>
/// Tạo/sửa chương trình khuyến mãi (docs/srs/14). Ngày là ngày Việt Nam. <c>Slug</c> trống ⇒ tự sinh từ tên.
/// <c>PublicTeaser</c>: cho hiện ở mục "sắp diễn ra" trước ngày bắt đầu (BR-VCH-16).
/// </summary>
public sealed record PromotionRequest(
    string Title,
    string? Slug,
    string? DescriptionHtml,
    DateOnly StartDate,
    DateOnly EndDate,
    bool PublicTeaser,
    bool IsActive,
    Guid? BannerImageId = null);
