namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>docs/srs/23 FR-PLT-03: tổng theo 4 nhóm thanh toán và kênh con, dòng tổng cộng, chuỗi theo kỳ.</summary>
public sealed record PaymentGroupReportDto(
    IReadOnlyList<PaymentGroupRowDto> Groups,
    PaymentAmountsDto Total,
    IReadOnlyList<PaymentGroupPeriodDto> Series,
    string Definition);
