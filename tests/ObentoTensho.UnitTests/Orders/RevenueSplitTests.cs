using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;

namespace ObentoTensho.UnitTests.Orders;

// docs/srs/23 BR-PLT-02/03/05: doanh thu ròng, khấu trừ 25%, GTGT 3%, TNCN 1,5%, thực nhận 70,5%
public sealed class RevenueSplitTests
{
    private static readonly RevenueSplit.Rates Rates = new(25m, 3m, 1.5m);

    [Fact]
    public void Don_grab_giam_15_phan_tram()
    {
        var order = NewOrder(subTotal: 100_000m);
        order.PlatformDiscount = 15_000m;

        var split = RevenueSplit.Compute(RevenueSplit.NetOf(order), Rates);

        Assert.Equal(85_000m, split.NetRevenue);
        Assert.Equal(21_250m, split.DeductionAmount);
        Assert.Equal(2_550m, split.VatAmount);
        Assert.Equal(1_275m, split.PitAmount);
        Assert.Equal(59_925m, split.ReceivedAmount);
    }

    [Fact]
    public void Don_tai_quan_khong_giam_tru_29_5_phan_tram()
    {
        var split = RevenueSplit.Compute(RevenueSplit.NetOf(NewOrder(subTotal: 200_000m)), Rates);

        Assert.Equal(59_000m, split.DeductionAmount + split.VatAmount + split.PitAmount);
        Assert.Equal(141_000m, split.ReceivedAmount);
    }

    [Fact]
    public void Don_tai_quan_tru_moi_giam_gia_khong_tinh_phi_ship()
    {
        var order = NewOrder(subTotal: 120_000m);
        order.VoucherDiscount = 20_000m;

        Assert.Equal(100_000m, RevenueSplit.NetOf(order));
    }

    // BR-PLT-05: từng khoản làm tròn về đồng, thực nhận lấy phần còn lại nên các cột luôn cộng khớp
    [Theory]
    [InlineData(33_333)]
    [InlineData(1)]
    [InlineData(99_999)]
    [InlineData(0)]
    public void Lam_tron_tung_khoan_cong_khop(int net)
    {
        var split = RevenueSplit.Compute(net, Rates);

        Assert.Equal(split.NetRevenue, split.DeductionAmount + split.VatAmount + split.PitAmount + split.ReceivedAmount);
        Assert.Equal(decimal.Round(split.DeductionAmount), split.DeductionAmount);
    }

    [Fact]
    public void Ban_chup_giu_ti_le_luc_hoan_thanh()
    {
        var split = RevenueSplit.Compute(100_000m, Rates);

        Assert.Equal((25m, 3m, 1.5m), (split.DeductionPercent, split.VatPercent, split.PitPercent));
    }

    private static Order NewOrder(decimal subTotal) => new()
    {
        OrderCode = "OBT-TEST",
        DailySeq = 1,
        AccessTokenHash = new string('0', 64),
        CustomerName = "Khách",
        CustomerPhone = "0900000000",
        CustomerEmail = "khach@example.com",
        Lang = "vi",
        PickupAtUtc = new DateTime(2026, 11, 24, 5, 0, 0, DateTimeKind.Utc),
        Channel = OrderChannel.Counter,
        SubTotal = subTotal,
        TotalAmount = subTotal,
        DeliveryFee = 15_000m,
        CreatedAtUtc = new DateTime(2026, 11, 24, 4, 0, 0, DateTimeKind.Utc),
    };
}
