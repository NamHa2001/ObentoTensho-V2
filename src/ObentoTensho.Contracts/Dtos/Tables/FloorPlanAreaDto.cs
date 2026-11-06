namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Sơ đồ bàn của một khu vực, tải bằng một truy vấn.</summary>
public sealed record FloorPlanAreaDto(int Id, string Name, bool IsActive, IReadOnlyList<TableDto> Tables);
