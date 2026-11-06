using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ObentoTensho.Api.Configuration;

// SEC-02: chỉ tin X-Forwarded-For từ proxy đã khai báo (Nginx/Docker, dải IP Cloudflare);
// không khai báo thì chỉ tin loopback — client không tự giả được IP để lách giới hạn
public static class NetworkConfiguration
{
    public const string SectionName = "Network";

    public static IServiceCollection AddForwardedHeadersFromConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection($"{SectionName}:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection($"{SectionName}:KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Chỉ lấy địa chỉ do proxy gần nhất ghi, bỏ phần client tự chèn phía trước
            options.ForwardLimit = 1;

            if (proxies.Length > 0 || networks.Length > 0)
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
            }

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }
}
