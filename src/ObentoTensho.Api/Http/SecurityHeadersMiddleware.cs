using System.Diagnostics;
using Microsoft.Net.Http.Headers;

namespace ObentoTensho.Api.Http;

// SEC-09 (header bảo mật cho API JSON), SEC-68 (không cache phản hồi có xác thực), docs/srs/09 §7
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(() =>
        {
            ApplyHeaders(context, environment.IsDevelopment());
            return Task.CompletedTask;
        });
        return next(context);
    }

    private static void ApplyHeaders(HttpContext context, bool development)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";

        // Trang thử API (chỉ Development) cần chạy script của chính nó; mọi nơi khác giữ chính sách chặt
        headers.ContentSecurityPolicy = development && context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
            ? "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline' https:; font-src 'self' https: data:; img-src 'self' https: data:; connect-src 'self'; frame-ancestors 'none'"
            : "default-src 'none'; frame-ancestors 'none'";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (CorrelationIds.FromActivity(Activity.Current) is { } correlationId)
        {
            headers[CorrelationIds.Header] = correlationId.ToString();
        }

        var personalized = context.Request.Headers.ContainsKey(HeaderNames.Authorization)
            || context.Request.Headers.ContainsKey(HeaderNames.Cookie)
            || headers.ContainsKey(HeaderNames.SetCookie);
        if (personalized && !headers.ContainsKey(HeaderNames.CacheControl))
        {
            headers.CacheControl = "private, no-store";
        }
    }
}
