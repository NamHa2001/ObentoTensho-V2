using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.UnitTests.Orders;

// docs/srs/12 §11: bảng tính giá, máy trạng thái, mã đơn
public sealed class OrderRulesTests
{
    [Theory]
    [InlineData(200_000, 0, 0, 0, 0, 200_000)]

    // Hạng Bạc 3%: làm tròn xuống đồng
    [InlineData(123_457, 0.03, 0, 3_703, 0, 119_754)]

    // Có voucher thì không giảm theo hạng (BR-ORD-03)
    [InlineData(200_000, 0.08, 30_000, 0, 30_000, 170_000)]

    // Voucher lớn hơn tạm tính: chặn ở tạm tính, tổng 0 (BR-ORD-04)
    [InlineData(50_000, 0, 80_000, 0, 50_000, 0)]
    [InlineData(0, 0.05, 0, 0, 0, 0)]
    public void Tinh_tien_theo_cong_thuc(int subTotal, double rate, int voucher, int member, int voucherApplied, int total)
    {
        var totals = OrderPricing.Compute(subTotal, (decimal)rate, voucher);

        Assert.Equal((subTotal, member, voucherApplied, total), ((int)totals.SubTotal, (int)totals.MemberDiscount, (int)totals.VoucherDiscount, (int)totals.TotalAmount));
    }

    [Fact]
    public void Tam_tinh_gom_ca_gia_tuy_chon()
    {
        Assert.Equal(185_000m, OrderPricing.SubTotal([(65_000m, 15_000m, 2), (25_000m, 0m, 1)]));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Preparing, OrderActor.Staff, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Ready, OrderActor.Staff, false)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready, OrderActor.Staff, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed, OrderActor.Staff, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Completed, OrderActor.Staff, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, OrderActor.Customer, true)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Cancelled, OrderActor.Customer, false)]
    [InlineData(OrderStatus.Ready, OrderStatus.Cancelled, OrderActor.Staff, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled, OrderActor.Staff, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled, OrderActor.Staff, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, OrderActor.Staff, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Preparing, OrderActor.Customer, false)]
    public void May_trang_thai_chi_cho_cac_buoc_hop_le(OrderStatus from, OrderStatus to, OrderActor actor, bool allowed) =>
        Assert.Equal(allowed, OrderStateMachine.CanTransition(from, to, actor));

    [Theory]
    [InlineData(OrderStatus.Preparing, StaffCapability.OrdersKitchen)]
    [InlineData(OrderStatus.Ready, StaffCapability.OrdersKitchen)]
    [InlineData(OrderStatus.Completed, StaffCapability.OrdersCashier)]
    [InlineData(OrderStatus.Cancelled, StaffCapability.OrdersCancel)]
    public void Moi_buoc_can_quyen_rieng(OrderStatus to, StaffCapability capability) =>
        Assert.Equal(capability, OrderStateMachine.RequiredCapability(to));

    [Fact]
    public void Nhan_vien_huy_phai_co_ly_do_khach_thi_khong()
    {
        Assert.True(OrderStateMachine.RequiresReason(OrderStatus.Cancelled, OrderActor.Staff));
        Assert.False(OrderStateMachine.RequiresReason(OrderStatus.Cancelled, OrderActor.Customer));
        Assert.False(OrderStateMachine.RequiresReason(OrderStatus.Ready, OrderActor.Staff));
    }

    [Fact]
    public void Ma_don_ngau_nhien_dung_dinh_dang()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => OrderCodes.New()).ToArray();

        Assert.All(codes, c => Assert.Matches("^OBT-TK-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$", c));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("OBT-TK-8K4Q-7ZM2", "OBT-TK-8K4Q-7ZM2")]
    [InlineData("obt-tk-8k4q-7zm2", "OBT-TK-8K4Q-7ZM2")]
    [InlineData(" obttk8k4q7zm2 ", "OBT-TK-8K4Q-7ZM2")]
    [InlineData("8K4Q7ZM2", "OBT-TK-8K4Q-7ZM2")]
    [InlineData("OBT-TK-8K4Q-7ZMO", "OBT-TK-8K4Q-7ZM0")]
    [InlineData("OBT-TK-8K4Q-7ZM", "")]
    [InlineData("OBT-TK-8K4Q-7ZMU", "")]
    [InlineData(null, "")]
    public void Ma_khach_go_lech_van_chuan_hoa_dung(string? input, string expected) =>
        Assert.Equal(expected, OrderCodes.Normalize(input));

    [Theory]
    [InlineData("Nguyễn Văn An", "An")]
    [InlineData("  Bình  ", "Bình")]
    [InlineData("", "")]
    public void Bep_chi_thay_ten_goi_ngan(string fullName, string shortName) =>
        Assert.Equal(shortName, OrderMappings.ShortName(fullName));
}
