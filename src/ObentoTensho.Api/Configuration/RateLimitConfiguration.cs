using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Api.Configuration;

// SEC-02, docs/srs/09 §6: phân vùng theo IP thật (sau ForwardedHeaders), cửa sổ trượt.
// Bộ đếm hiện ở bộ nhớ từng tiến trình; chạy nhiều instance thì chuyển sang Redis ở phần triển khai.
public static class RateLimitConfiguration
{
    public const string LoginPolicy = "login";
    public const string ReservationPolicy = "reservation";
    public const string LookupPolicy = "lookup";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const int SegmentsPerWindow = 6;

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .Validate(s => s.LoginPerMinute > 0 && s.GeneralPerMinute > 0 && s.ReservationPerHour > 0 && s.LookupPerMinute > 0, "RateLimiting: giới hạn phải lớn hơn 0.")
            .ValidateOnStart();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitSettings>>((options, settings) =>
        {
            var limits = settings.Value;
            options.OnRejected = OnRejectedAsync;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                http.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : SlidingWindow("general:" + ClientKey(http), limits.GeneralPerMinute));
            options.AddPolicy(LoginPolicy, http => SlidingWindow(ClientKey(http), limits.LoginPerMinute));
            options.AddPolicy(ReservationPolicy, http => SlidingWindow(ClientKey(http), limits.ReservationPerHour, TimeSpan.FromHours(1)));
            options.AddPolicy(LookupPolicy, http => SlidingWindow(ClientKey(http), limits.LookupPerMinute));
        });

        return services;
    }

    // IPv6: một khách thường giữ cả dải /64, gộp lại để không lách được bằng cách đổi địa chỉ
    public static string ClientKey(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var ip = http.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return "unknown";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4().ToString();
        }

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    private static RateLimitPartition<string> SlidingWindow(string key, int permits, TimeSpan? window = null) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window ?? Window,
            SegmentsPerWindow = SegmentsPerWindow,
            QueueLimit = 0,
        });

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : Window / SegmentsPerWindow;
        context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        await ApiProblems.WriteAsync(context.HttpContext, ErrorCodes.RateLimited);
    }
}
