using ObentoTensho.Core.Auth.Abstractions;

namespace ObentoTensho.Api.Http;

public static class HttpContextExtensions
{
    // IP lấy từ kết nối; sau khi bật ForwardedHeaders (SEC-02) sẽ là IP thật của khách
    public static ClientInfo GetClientInfo(this HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        var userAgent = http.Request.Headers.UserAgent.ToString();
        return new ClientInfo(
            http.Connection.RemoteIpAddress?.ToString(),
            string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
    }
}
