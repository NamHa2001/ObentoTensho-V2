using Microsoft.Extensions.Options;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Api.Realtime;

// docs/srs/09 §7: WebSocket không qua CORS nên kiểm Origin ở đây — hub nhân viên chỉ từ trang quản trị, hub công khai chỉ từ trang khách
public sealed class HubOriginMiddleware(RequestDelegate next, IOptions<WebOriginsOptions> origins)
{
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Request.Path;
        var allowed = path.StartsWithSegments(HubRoutes.Staff, StringComparison.OrdinalIgnoreCase) ? origins.Value.AdminOrigins
            : path.StartsWithSegments(HubRoutes.Public, StringComparison.OrdinalIgnoreCase) ? origins.Value.CustomerOrigins
            : null;
        if (allowed is not null && !allowed.Contains(context.Request.Headers.Origin.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            return ApiProblems.WriteAsync(context, ErrorCodes.Forbidden);
        }

        return next(context);
    }
}
