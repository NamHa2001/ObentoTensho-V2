using System.Globalization;

namespace ObentoTensho.Infrastructure.Delivery;

// Chuỗi "vĩ độ,kinh độ" (vd "10.7769,106.7009") dùng chung cho điểm xuất phát trong cài đặt và điểm khách ghim
internal static class LatLng
{
    public static bool TryParse(string value, out double latitude, out double longitude)
    {
        latitude = 0;
        longitude = 0;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latitude)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out longitude)
            && latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180;
    }
}
