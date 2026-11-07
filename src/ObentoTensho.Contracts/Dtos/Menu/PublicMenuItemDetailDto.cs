namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Chi tiết món theo slug: mô tả đầy đủ (đã làm sạch HTML), nhóm tùy chọn, món liên quan.</summary>
public sealed record PublicMenuItemDetailDto(
    PublicMenuItemDto Item,
    string? FullDescHtml,
    IReadOnlyList<PublicOptionGroupDto> OptionGroups,
    IReadOnlyList<PublicMenuItemDto> Related);
