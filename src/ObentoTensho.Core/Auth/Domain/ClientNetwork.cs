using System.Net;

namespace ObentoTensho.Core.Auth.Domain;

// SEC-58: "thiết bị quen" = cùng trình duyệt và cùng dải mạng (IPv4 /24, IPv6 /64) — IP nhà mạng hay đổi trong dải
public static class ClientNetwork
{
    public static string? Prefix(string? ip)
    {
        if (ip is null || !IPAddress.TryParse(ip, out var address))
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4
            ? $"{bytes[0]}.{bytes[1]}.{bytes[2]}."
            : string.Join(':', Enumerable.Range(0, 4).Select(i => ((bytes[i * 2] << 8) | bytes[(i * 2) + 1]).ToString("x", System.Globalization.CultureInfo.InvariantCulture))) + ":";
    }
}
