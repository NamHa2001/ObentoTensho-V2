namespace ObentoTensho.Infrastructure.Delivery;

// Google Maps Platform: khóa phía máy chủ (chỉ bật Routes API), đặt qua cấu hình bí mật; trống = chưa tính được phí giao
public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    public string? ServerApiKey { get; set; }
}
