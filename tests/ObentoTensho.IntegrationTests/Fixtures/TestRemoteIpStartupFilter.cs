using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ObentoTensho.IntegrationTests.Fixtures;

// TestServer không có IP kết nối; header này giả lập địa chỉ TCP của bên gọi (proxy hoặc client)
// để test ForwardedHeaders và giới hạn theo IP. Chạy trước mọi middleware của ứng dụng.
public sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.TryParse(context.Request.Headers[Header], out var ip)
                ? ip
                : IPAddress.Loopback;
            return nextMiddleware(context);
        });
        next(app);
    };
}
