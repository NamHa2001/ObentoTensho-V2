namespace ObentoTensho.Infrastructure.Delivery;

// Google Maps Platform: khóa phía máy chủ (chỉ bật Routes API), đặt qua cấu hình bí mật.
// Trống ⇒ dùng OpenStreetMap: Nominatim tìm toạ độ theo địa chỉ, OSRM tính quãng đường (dịch vụ công cộng miễn phí).
public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    public string? ServerApiKey { get; set; }

    public Uri NominatimBaseUrl { get; set; } = new("https://nominatim.openstreetmap.org/");

    public Uri OsrmBaseUrl { get; set; } = new("https://router.project-osrm.org/");
}
