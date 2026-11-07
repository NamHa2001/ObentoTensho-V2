namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>Retention: phần trăm khách có từ 2 đơn trong 90 ngày, trên số khách có đơn trong 90 ngày tính đến ngày <c>To</c> (BR-RPT-14).</summary>
public sealed record CustomerReportDto(
    IReadOnlyList<DayValueDto> NewByDay,
    int NewTotal,
    IReadOnlyList<TierCountDto> Tiers,
    decimal RetentionPercent,
    IReadOnlyList<TopCustomerDto> TopCustomers,
    string RetentionDefinition);
