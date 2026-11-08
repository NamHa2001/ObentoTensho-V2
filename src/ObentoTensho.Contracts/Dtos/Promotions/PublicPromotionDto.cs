namespace ObentoTensho.Contracts.Dtos.Promotions;

/// <summary>
/// Khuyến mãi trang công khai (BR-VCH-16). <c>Upcoming</c>: chưa tới ngày bắt đầu. <c>VoucherCodes</c>: mã công khai
/// của chương trình đang dùng được; không gồm mã cá nhân và mã sinh hàng loạt.
/// </summary>
public sealed record PublicPromotionDto(
    string Slug,
    string Title,
    string? DescriptionHtml,
    DateOnly StartDate,
    DateOnly EndDate,
    bool Upcoming,
    IReadOnlyList<string> VoucherCodes,
    string ContentLang,
    bool IsFallback);
