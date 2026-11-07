using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Nhóm tùy chọn kèm các tùy chọn.</summary>
public sealed record AdminOptionGroupDto(
    int Id,
    string Name,
    OptionSelectionType SelectionType,
    bool IsRequired,
    int MinSelect,
    int MaxSelect,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<AdminOptionDto> Options,
    string Version);
