namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary><c>Net</c> = tiền thu theo ngày thanh toán trừ hoàn tiền theo ngày hoàn; <c>PreviousNet</c>: kỳ liền trước cùng độ dài (BR-RPT-31).</summary>
public sealed record RevenueReportDto(
    decimal Paid,
    decimal Refunds,
    decimal Net,
    decimal PreviousNet,
    IReadOnlyList<RevenuePointDto> Series,
    IReadOnlyList<NamedValueDto> ByCategory,
    string Definition,
    IReadOnlyList<ChannelRevenueDto>? ByChannel = null,
    decimal ShippingCollected = 0);
