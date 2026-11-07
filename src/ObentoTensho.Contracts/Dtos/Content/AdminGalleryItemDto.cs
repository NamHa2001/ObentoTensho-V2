using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Mục gallery cho trang quản trị; <c>Version</c> dùng làm ETag.</summary>
public sealed record AdminGalleryItemDto(
    int Id,
    GalleryMediaType MediaType,
    ImageDto? Image,
    string? VideoUrl,
    GalleryCategory Category,
    string? Caption,
    int DisplayOrder,
    bool IsActive,
    string Version);
