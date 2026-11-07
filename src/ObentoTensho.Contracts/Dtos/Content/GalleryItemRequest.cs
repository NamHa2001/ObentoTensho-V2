using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>
/// Tạo/sửa mục gallery (FR-CNT-02). Ảnh: <c>ImageId</c> đã qua pipeline. Video: <c>VideoUrl</c> chỉ nhận YouTube/TikTok (BR-CNT-35).
/// </summary>
public sealed record GalleryItemRequest(
    GalleryMediaType MediaType,
    Guid? ImageId,
    string? VideoUrl,
    GalleryCategory Category,
    string? Caption,
    bool IsActive);
