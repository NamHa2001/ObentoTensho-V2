namespace ObentoTensho.Contracts.Dtos.PlatformOrders;

// docs/srs/23 BR-PLT-03: tỉ lệ hiện tại để màn hình nhập đơn app tính thử thực nhận
public sealed record RevenueRatesDto(decimal DeductionPercent, decimal VatPercent, decimal PitPercent);
