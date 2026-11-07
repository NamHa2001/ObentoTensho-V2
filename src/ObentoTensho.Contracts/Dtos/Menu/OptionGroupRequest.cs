using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Menu;

/// <summary>Nhóm tùy chọn dùng chung (BR-MNU-18).</summary>
public sealed record OptionGroupRequest(string Name, OptionSelectionType SelectionType, bool IsRequired, int MinSelect, int MaxSelect, bool IsActive);
