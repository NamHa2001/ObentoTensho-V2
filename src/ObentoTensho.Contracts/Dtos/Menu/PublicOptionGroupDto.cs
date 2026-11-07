using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Nhóm tùy chọn của một món.</summary>
public sealed record PublicOptionGroupDto(
    int Id,
    string Name,
    OptionSelectionType SelectionType,
    bool IsRequired,
    int MinSelect,
    int MaxSelect,
    IReadOnlyList<PublicOptionDto> Options);
