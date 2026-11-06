namespace ObentoTensho.Contracts.Dtos.Tables;

/// <summary>Thay toàn bộ giờ mở cửa thường kỳ; phải đủ 7 ngày.</summary>
public sealed record WeeklyHoursRequest(IReadOnlyList<OpeningDayDto> Days);
