using ObentoTensho.Contracts.Enums;

namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Khóa cả ngày (Start/End null) hoặc khung <c>[Start, End)</c>.</summary>
public sealed record BlockedDateDto(int Id, DateOnly Date, BlockScope AppliesTo, TimeOnly? Start, TimeOnly? End, string? Reason);
