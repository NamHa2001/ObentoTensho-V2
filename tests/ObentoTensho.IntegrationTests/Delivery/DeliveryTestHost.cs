using Microsoft.Extensions.DependencyInjection;
using ObentoTensho.Core.Delivery.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;
using ObentoTensho.IntegrationTests.Fixtures;

namespace ObentoTensho.IntegrationTests.Delivery;

// API test có bật giao hàng và quãng đường giả: địa chỉ chứa "{n}m" thì cách quán n mét; "khong-tim-thay" ⇒ không có đường;
// "google-loi" ⇒ dịch vụ bản đồ lỗi; điểm ghim "vĩ độ,kinh độ" ⇒ cách quán 3500 m. Đếm số lần gọi để kiểm tra không gọi thừa.
public static class DeliveryTestHost
{
    public static ApiFactory Create(string connectionString, FakeRoutes routes, bool enabled = true) =>
        new(connectionString, services: s =>
        {
            s.AddSingleton<IRouteDistance>(routes);
            s.AddSingleton<ISettingsReader>(sp => new Overrides((ISettingsReader)sp.GetRequiredService<ISettingsSnapshot>(), enabled));
        });

    public sealed class FakeRoutes : IRouteDistance
    {
        private int _calls;

        public int Calls => _calls;

        public Task<RouteDistanceResult> DrivingAsync(string origin, string destination, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (destination.Contains("google-loi", StringComparison.Ordinal))
            {
                return Task.FromResult(RouteDistanceResult.Unavailable);
            }

            if (destination.StartsWith("10.", StringComparison.Ordinal) && destination.Contains(",106.", StringComparison.Ordinal))
            {
                return Task.FromResult(RouteDistanceResult.Of(3500));
            }

            var marker = destination.Split(' ').FirstOrDefault(w => w.EndsWith('m') && int.TryParse(w[..^1], System.Globalization.CultureInfo.InvariantCulture, out _));
            return Task.FromResult(marker is null ? RouteDistanceResult.NotFound : RouteDistanceResult.Of(int.Parse(marker[..^1], System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private sealed class Overrides(ISettingsReader inner, bool enabled) : ISettingsReader
    {
        public string Get(string key) => key switch
        {
            SettingKeys.DeliveryEnabled => enabled ? "true" : "false",
            SettingKeys.DeliveryOrigin => "10.7769,106.7009",
            _ => inner.Get(key),
        };

        public int GetInt(string key) => inner.GetInt(key);

        public bool GetBool(string key) => string.Equals(key, SettingKeys.DeliveryEnabled, StringComparison.Ordinal) ? enabled : inner.GetBool(key);

        public IReadOnlyDictionary<string, string> Public() => inner.Public();
    }
}
