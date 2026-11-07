using ObentoTensho.Contracts.Dtos.Media;
using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Content;

/// <summary>Mục gallery công khai. <c>EmbedUrl</c>: địa chỉ nhúng ít theo dõi, giao diện chỉ tải khi khách bấm (BR-CNT-35).</summary>
public sealed record PublicGalleryItemDto(int Id, GalleryMediaType MediaType, ImageDto? Image, string? EmbedUrl, GalleryCategory Category, string? Caption);
