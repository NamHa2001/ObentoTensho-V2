using Microsoft.AspNetCore.Mvc.Filters;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Api.Http;

// SEC-42: endpoint dùng cookie (refresh, logout) chỉ nhận yêu cầu từ origin frontend, có header tùy biến
// mà form cross-site không tự thêm được. SameSite=Strict không đủ vì www./admin./api. cùng một site.
public abstract class TrustedOriginFilter : IResourceFilter
{
    public const string ClientHeader = "X-OT-Client";

    protected abstract IEnumerable<string> AllowedOrigins { get; }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsTrusted(context.HttpContext.Request))
        {
            context.Result = ApiProblems.Create(context.HttpContext, ErrorCodes.Forbidden).ToActionResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    private bool IsTrusted(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Headers[ClientHeader]))
        {
            return false;
        }

        var origin = request.Headers.Origin.ToString();
        if (!AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // Trình duyệt cũ không gửi Sec-Fetch-Site; khi có thì phải cùng site
        var fetchSite = request.Headers["Sec-Fetch-Site"].ToString();
        return fetchSite.Length == 0 || fetchSite is "same-origin" or "same-site";
    }
}
