using ObentoTensho.Contracts.Dtos.Media;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Chi tiết món theo slug: mô tả đầy đủ (đã làm sạch HTML), ảnh phụ, nhóm tùy chọn, món liên quan.</summary>
public sealed record PublicMenuItemDetailDto(
    PublicMenuItemDto Item,
    string? FullDescHtml,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<PublicOptionGroupDto> OptionGroups,
    IReadOnlyList<PublicMenuItemDto> Related);
