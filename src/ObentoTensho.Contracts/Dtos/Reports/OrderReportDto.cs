namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary><c>AverageValue</c> = tiền thu / số đơn đã thanh toán.</summary>
public sealed record OrderReportDto(
    int Created,
    int Completed,
    int Cancelled,
    decimal AverageValue,
    IReadOnlyList<NamedValueDto> ByHour,
    IReadOnlyList<TopDishDto> TopDishes);
