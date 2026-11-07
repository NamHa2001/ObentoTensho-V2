using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Banner trang chủ: chỉ gồm banner đang bật, trong ngày hiển thị, ảnh đã sẵn sàng.</summary>
public sealed record PublicBannerDto(int Id, ImageDto Image, ImageDto? ImageMobile, string Title, string? Subtitle, string? CtaText, string? CtaLink);
