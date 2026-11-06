namespace ObentoTensho.Contracts.Dtos.Tables;

public sealed record SpecialHoursRequest(bool Closed, IReadOnlyList<OpeningRangeDto> Ranges, string? Note);
