namespace ObentoTensho.Contracts.Dtos.Tables;

public sealed record TableRequest(int AreaId, string Name, int MinCapacity, int MaxCapacity, TableLayout Layout, bool IsActive);
