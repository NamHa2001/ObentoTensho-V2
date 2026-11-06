namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Giờ đặc biệt của một ngày (lễ/Tết), ghi đè giờ thường kỳ.</summary>
public sealed record SpecialHoursDto(DateOnly Date, bool Closed, IReadOnlyList<OpeningRangeDto> Ranges, string? Note);
