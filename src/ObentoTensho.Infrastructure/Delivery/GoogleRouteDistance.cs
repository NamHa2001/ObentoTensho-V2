using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Delivery.Abstractions;

namespace ObentoTensho.Infrastructure.Delivery;

// Routes API computeRoutes cho xe máy, chỉ lấy distanceMeters để tốn ít phí nhất.
// Cùng một cặp địa chỉ được nhớ 6 giờ: khách xem phí rồi đặt đơn chỉ tốn một lần gọi.
internal sealed partial class GoogleRouteDistance(
    HttpClient http,
    IOptions<MapsOptions> options,
    IMemoryCache cache,
    ILogger<GoogleRouteDistance> logger) : IRouteDistance
{
    private static readonly Uri Endpoint = new("https://routes.googleapis.com/directions/v2:computeRoutes");
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);

    public async Task<RouteDistanceResult> DrivingAsync(string origin, string destination, CancellationToken cancellationToken)
    {
        var key = options.Value.ServerApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return RouteDistanceResult.Unavailable;
        }

        var cacheKey = $"route:{origin.ToUpperInvariant()}|{destination.ToUpperInvariant()}";
        if (cache.TryGetValue(cacheKey, out RouteDistanceResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new
                {
                    origin = Waypoint(origin),
                    destination = Waypoint(destination),
                    travelMode = "TWO_WHEELER",
                    languageCode = "vi",
                    regionCode = "VN",
                }),
            };
            request.Headers.Add("X-Goog-Api-Key", key);
            request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogFailed(logger, (int)response.StatusCode, null);
                return (int)response.StatusCode is 400 or 404 ? RouteDistanceResult.NotFound : RouteDistanceResult.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<RoutesResponse>(cancellationToken);
            var result = body?.Routes is [{ DistanceMeters: > 0 } first, ..] ? RouteDistanceResult.Of(first.DistanceMeters) : RouteDistanceResult.NotFound;
            cache.Set(cacheKey, result, CacheFor);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            LogFailed(logger, 0, ex);
            return RouteDistanceResult.Unavailable;
        }
    }

    // "10.7769,106.7009" ⇒ toạ độ; còn lại là địa chỉ để Google tự tìm
    private static object Waypoint(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng)
            ? new { location = new { latLng = new { latitude = lat, longitude = lng } } }
            : new { address = value };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Không tính được quãng đường giao hàng (HTTP {Status}).")]
    private static partial void LogFailed(ILogger logger, int status, Exception? exception);

    private sealed record RoutesResponse([property: JsonPropertyName("routes")] IReadOnlyList<RouteItem>? Routes);

    private sealed record RouteItem([property: JsonPropertyName("distanceMeters")] int DistanceMeters);
}
