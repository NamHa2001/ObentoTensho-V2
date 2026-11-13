using ObentoTensho.Core.Delivery.Domain;

namespace ObentoTensho.UnitTests.Delivery;

// Chủ quán 2026-11-13: miễn phí 2 km quanh quán, từ km thứ 3 tính 15.000đ/km, phần lẻ làm tròn lên
public sealed class DeliveryFeeTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1_999, 0)]
    [InlineData(2_000, 0)]
    [InlineData(2_001, 15_000)]
    [InlineData(2_300, 15_000)]
    [InlineData(3_000, 15_000)]
    [InlineData(3_001, 30_000)]
    [InlineData(4_100, 45_000)]
    [InlineData(7_000, 75_000)]
    public void Mien_phi_2km_tu_km_thu_3_tinh_15_nghin_moi_km(int meters, int fee)
    {
        Assert.Equal(fee, DeliveryFee.Compute(meters, freeKm: 2, feePerKm: 15_000));
    }

    [Fact]
    public void Ban_kinh_mien_phi_bang_0_thi_tinh_tu_met_dau_tien()
    {
        Assert.Equal(10_000, DeliveryFee.Compute(1, freeKm: 0, feePerKm: 10_000));
    }

    [Theory]
    [InlineData(2_349, 2.3)]
    [InlineData(2_350, 2.4)]
    [InlineData(10_000, 10.0)]
    public void So_km_hien_thi_lam_tron_mot_chu_so(int meters, double km)
    {
        Assert.Equal((decimal)km, DeliveryFee.Kilometers(meters));
    }
}
