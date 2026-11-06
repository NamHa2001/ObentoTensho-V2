namespace ObentoTensho.Contracts.Dtos.Tables;

public sealed record WeeklyHoursDto(IReadOnlyList<OpeningDayDto> Days, string Version);
