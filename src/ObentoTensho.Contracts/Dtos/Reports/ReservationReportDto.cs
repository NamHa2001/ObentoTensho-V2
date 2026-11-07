namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Tỷ lệ tính trên tổng lượt đặt, phần trăm. Đến = Check-in/Hoàn thành (BR-RPT-11).</summary>
public sealed record ReservationReportDto(
    int Total,
    int Arrived,
    int Cancelled,
    int NoShow,
    decimal ArrivedPercent,
    decimal CancelledPercent,
    decimal NoShowPercent,
    IReadOnlyList<NamedValueDto> ByHour,
    IReadOnlyList<NamedValueDto> ByArea);
