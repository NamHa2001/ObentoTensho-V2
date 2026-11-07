namespace ObentoTensho.Core.Orders.Domain;

// BR-ORD-03/04: giảm giá loại trừ nhau — có voucher thì không giảm theo hạng; giảm theo hạng làm tròn xuống đồng;
// tổng giảm không vượt tạm tính; tổng tiền không âm. Phần voucher/hạng nối vào ở các phần điểm và voucher.
public static class OrderPricing
{
    public sealed record Totals(decimal SubTotal, decimal MemberDiscount, decimal VoucherDiscount, decimal TotalAmount);

    public static Totals Compute(decimal subTotal, decimal memberRate, decimal voucherDiscount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(subTotal);
        ArgumentOutOfRangeException.ThrowIfNegative(memberRate);
        ArgumentOutOfRangeException.ThrowIfNegative(voucherDiscount);

        var voucher = Math.Min(voucherDiscount, subTotal);
        var member = voucher > 0 ? 0 : Math.Min(decimal.Floor(subTotal * memberRate), subTotal);
        return new Totals(subTotal, member, voucher, Math.Max(0, subTotal - member - voucher));
    }

    public static decimal SubTotal(IEnumerable<(decimal UnitPrice, decimal OptionsPrice, int Quantity)> lines) =>
        lines.Sum(l => (l.UnitPrice + l.OptionsPrice) * l.Quantity);
}
