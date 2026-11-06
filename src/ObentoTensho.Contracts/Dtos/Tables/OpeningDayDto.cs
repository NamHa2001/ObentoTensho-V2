namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Một ngày trong tuần (0 = Chủ nhật … 6 = Thứ bảy).</summary>
public sealed record OpeningDayDto(int DayOfWeek, bool Closed, IReadOnlyList<OpeningRangeDto> Ranges);
