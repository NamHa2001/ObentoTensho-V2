using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Bàn và vị trí trên sơ đồ (BR-TBL-11).</summary>
public sealed record TableDto(
    int Id,
    int AreaId,
    string Name,
    int MinCapacity,
    int MaxCapacity,
    TableStatus Status,
    TableLayout Layout,
    bool IsActive,
    string Version);
