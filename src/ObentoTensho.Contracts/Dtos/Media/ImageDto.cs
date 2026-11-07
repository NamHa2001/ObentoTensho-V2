using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Media;

/// <summary>
/// Ảnh đã tải lên. <c>Variants</c> rỗng khi còn <c>Processing</c>. <c>BlurHash</c> để hiện ảnh mờ trong lúc tải (FR-IMG-09).
/// </summary>
public sealed record ImageDto(
    Guid Id,
    ImageStatus Status,
    int Width,
    int Height,
    string AltText,
    string? BlurHash,
    IReadOnlyList<ImageVariantDto> Variants);
