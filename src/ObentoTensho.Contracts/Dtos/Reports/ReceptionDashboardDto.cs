namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Lễ tân chỉ có KPI đặt bàn; server không trả trường doanh thu (BR-RPT-32).</summary>
public sealed record ReceptionDashboardDto(
    IReadOnlyList<ReservationStatusCountDto> ReservationsToday,
    IReadOnlyList<DayValueDto> ReservationsByDay,
    IReadOnlyList<BusySlotDto> BusySlots);
