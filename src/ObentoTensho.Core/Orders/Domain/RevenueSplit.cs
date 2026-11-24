namespace ObentoTensho.Core.Orders.Domain;

// docs/srs/23 BR-PLT-02/03/05: doanh thu ròng = giá gốc − giảm giá; khấu trừ, GTGT, TNCN cùng tính trên doanh thu ròng,
// mỗi khoản làm tròn về đồng (≥ 0,5 lên); thực nhận = doanh thu ròng − ba khoản đã làm tròn để cộng cột luôn khớp
public sealed record RevenueSplit(
    decimal DeductionPercent,
    decimal VatPercent,
    decimal PitPercent,
    decimal NetRevenue,
    decimal DeductionAmount,
    decimal VatAmount,
    decimal PitAmount,
    decimal ReceivedAmount)
{
    public sealed record Rates(decimal DeductionPercent, decimal VatPercent, decimal PitPercent);

    public static RevenueSplit Compute(decimal netRevenue, Rates rates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(netRevenue);
        ArgumentNullException.ThrowIfNull(rates);
        var deduction = Part(netRevenue, rates.DeductionPercent);
        var vat = Part(netRevenue, rates.VatPercent);
        var pit = Part(netRevenue, rates.PitPercent);
        return new RevenueSplit(rates.DeductionPercent, rates.VatPercent, rates.PitPercent, netRevenue, deduction, vat, pit, netRevenue - deduction - vat - pit);
    }

    // Doanh thu ròng của một đơn: tạm tính trừ mọi giảm giá trên đơn; phí ship của quán không tính
    public static decimal NetOf(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        return Math.Max(0, order.SubTotal - order.MemberDiscount - order.VoucherDiscount - order.PlatformDiscount);
    }

    private static decimal Part(decimal amount, decimal percent) => decimal.Round(amount * percent / 100m, 0, MidpointRounding.AwayFromZero);
}
