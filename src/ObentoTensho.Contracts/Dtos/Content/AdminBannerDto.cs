using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Banner cho trang quản trị; <c>Version</c> dùng làm ETag.</summary>
public sealed record AdminBannerDto(
    int Id,
    ImageDto? Image,
    ImageDto? ImageMobile,
    string Title,
    string? Subtitle,
    string? CtaText,
    string? CtaLink,
    int DisplayOrder,
    DateOnly? ShowFromDate,
    DateOnly? ShowToDate,
    bool IsActive,
    string Version);
