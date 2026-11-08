using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Banner trang chủ: chỉ gồm banner đang bật, trong ngày hiển thị, ảnh đã sẵn sàng. <c>ContentLang</c>/<c>IsFallback</c>: BR-I18N-07.</summary>
public sealed record PublicBannerDto(int Id, ImageDto Image, ImageDto? ImageMobile, string Title, string? Subtitle, string? CtaText, string? CtaLink, string ContentLang, bool IsFallback);
