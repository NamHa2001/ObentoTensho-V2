namespace ObentoTensho.Core.Delivery.Domain;

// Chủ quán 2026-11-13: miễn phí trong bán kính freeKm; từ km kế tiếp tính feePerKm mỗi km, phần lẻ làm tròn lên
// (vd miễn phí 2 km, 15.000đ/km: 2,3 km ⇒ 15.000đ; 4,1 km ⇒ 45.000đ)
public static class DeliveryFee
{
    public static decimal Compute(int meters, int freeKm, int feePerKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(meters);
        var extra = meters - (freeKm * 1000);
        return extra <= 0 ? 0 : (decimal)Math.Ceiling(extra / 1000m) * feePerKm;
    }

    public static decimal Kilometers(int meters) => Math.Round(meters / 1000m, 1, MidpointRounding.AwayFromZero);
}
