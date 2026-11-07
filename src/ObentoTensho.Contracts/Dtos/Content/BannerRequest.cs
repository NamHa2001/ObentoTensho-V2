namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>
/// Tạo/sửa banner (FR-CNT-01). Ảnh là mã ảnh đã tải qua pipeline (BR-CNT-30). <c>CtaLink</c>: đường dẫn nội bộ <c>/…</c>
/// hoặc <c>https://</c> thuộc danh sách miền cho phép (SEC-47). Ngày là ngày Việt Nam (BR-CNT-34).
/// </summary>
public sealed record BannerRequest(
    Guid ImageId,
    Guid? ImageMobileId,
    string Title,
    string? Subtitle,
    string? CtaText,
    string? CtaLink,
    DateOnly? ShowFromDate,
    DateOnly? ShowToDate,
    bool IsActive);
