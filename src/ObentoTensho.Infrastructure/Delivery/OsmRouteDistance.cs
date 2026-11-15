using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Delivery.Abstractions;

namespace ObentoTensho.Infrastructure.Delivery;

// Chưa có khóa Google: địa chỉ chữ ⇒ toạ độ qua Nominatim, quãng đường qua OSRM (đều của OpenStreetMap, miễn phí, dùng vừa phải).
// Toạ độ địa chỉ nhớ 1 ngày, quãng đường nhớ 6 giờ để khách xem phí rồi đặt đơn không gọi lại.
internal sealed partial class OsmRouteDistance(
    HttpClient http,
    IOptions<MapsOptions> options,
    IMemoryCache cache,
    ILogger<OsmRouteDistance> logger) : IRouteDistance
{
    private static readonly TimeSpan RouteCacheFor = TimeSpan.FromHours(6);
    private static readonly TimeSpan PlaceCacheFor = TimeSpan.FromDays(1);

    public async Task<RouteDistanceResult> DrivingAsync(string origin, string destination, CancellationToken cancellationToken)
    {
        var cacheKey = $"osm-route:{origin.ToUpperInvariant()}|{destination.ToUpperInvariant()}";
        if (cache.TryGetValue(cacheKey, out RouteDistanceResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var from = await LocateAsync(origin, cancellationToken);
            var to = await LocateAsync(destination, cancellationToken);
            if (from is null || to is null)
            {
                return RouteDistanceResult.NotFound;
            }

            var path = string.Create(CultureInfo.InvariantCulture, $"route/v1/driving/{from.Value.Lng:F6},{from.Value.Lat:F6};{to.Value.Lng:F6},{to.Value.Lat:F6}?overview=false");
            using var response = await http.GetAsync(new Uri(options.Value.OsrmBaseUrl, path), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogFailed(logger, "OSRM", (int)response.StatusCode, null);
                return RouteDistanceResult.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<OsrmResponse>(cancellationToken);
            var result = body?.Routes is [{ Distance: > 0 } first, ..] ? RouteDistanceResult.Of((int)Math.Round(first.Distance)) : RouteDistanceResult.NotFound;
            cache.Set(cacheKey, result, RouteCacheFor);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            LogFailed(logger, "OpenStreetMap", 0, ex);
            return RouteDistanceResult.Unavailable;
        }
    }

    // Toạ độ có sẵn thì dùng luôn; còn lại tìm địa chỉ trong Việt Nam
    private async Task<(double Lat, double Lng)?> LocateAsync(string value, CancellationToken cancellationToken)
    {
        if (LatLng.TryParse(value, out var lat, out var lng))
        {
            return (lat, lng);
        }

        var cacheKey = $"osm-place:{value.ToUpperInvariant()}";
        if (cache.TryGetValue(cacheKey, out (double, double)? known))
        {
            return known;
        }

        var path = $"search?format=jsonv2&limit=1&countrycodes=vn&accept-language=vi&q={Uri.EscapeDataString(value)}";
        using var response = await http.GetAsync(new Uri(options.Value.NominatimBaseUrl, path), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogFailed(logger, "Nominatim", (int)response.StatusCode, null);
            throw new HttpRequestException($"Nominatim HTTP {(int)response.StatusCode}");
        }

        var places = await response.Content.ReadFromJsonAsync<IReadOnlyList<Place>>(cancellationToken);
        (double, double)? found = places is [var place, ..]
            && double.TryParse(place.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var foundLat)
            && double.TryParse(place.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var foundLng)
            ? (foundLat, foundLng)
            : null;
        cache.Set(cacheKey, found, PlaceCacheFor);
        return found;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tính được quãng đường giao hàng qua {Service} (HTTP {Status}).")]
    private static partial void LogFailed(ILogger logger, string service, int status, Exception? exception);

    private sealed record OsrmResponse([property: JsonPropertyName("routes")] IReadOnlyList<OsrmRoute>? Routes);

    private sealed record OsrmRoute([property: JsonPropertyName("distance")] double Distance);

    private sealed record Place([property: JsonPropertyName("lat")] string? Lat, [property: JsonPropertyName("lon")] string? Lon);
}
