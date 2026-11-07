namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Doanh thu = tiền thu theo ngày thanh toán trừ hoàn tiền theo ngày hoàn (BR-RPT-31); kỳ trước cùng độ dài.</summary>
public sealed record ManagerDashboardDto(
    PeriodValueDto RevenueToday,
    PeriodValueDto RevenueWeek,
    PeriodValueDto RevenueMonth,
    IReadOnlyList<ReservationStatusCountDto> ReservationsToday,
    IReadOnlyList<OrderStatusCountDto> OrdersToday,
    int NewCustomersThisMonth,
    int UnreadContacts,
    IReadOnlyList<DayValueDto> RevenueByDay,
    IReadOnlyList<DayValueDto> ReservationsByDay,
    IReadOnlyList<NamedValueDto> RevenueByCategory,
    IReadOnlyList<NamedValueDto> TopItems,
    IReadOnlyList<ExpiringVoucherDto> ExpiringVouchers,
    IReadOnlyList<BusySlotDto> BusySlots);
