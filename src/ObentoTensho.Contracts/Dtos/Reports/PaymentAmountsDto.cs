namespace ObentoTensho.Contracts.Dtos.Reports;

/// <summary>docs/srs/23 §4.3: số đơn, giá gốc, giảm giá, doanh thu ròng, các khoản trừ và thực nhận của một nhóm hoặc kênh con.</summary>
public sealed record PaymentAmountsDto(
    int Orders,
    decimal SubTotal,
    decimal Discount,
    decimal NetRevenue,
    decimal DeductionAmount,
    decimal VatAmount,
    decimal PitAmount,
    decimal ReceivedAmount);
