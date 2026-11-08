using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Errors;

namespace ObentoTensho.Api.Http;

// SEC-22, SEC-57: form công khai dễ bị bot (đặt bàn, gửi OTP, đăng ký) phải kèm token Turnstile, kiểm ở server
public class TurnstileFilter(TurnstileGate gate) : IAsyncResourceFilter
{
    public const string Header = "X-Turnstile-Token";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (IsExempt(http))
        {
            await next();
            return;
        }

        // Giới hạn dự phòng khi Cloudflare lỗi lấy theo chính sách rate limit của endpoint
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? RateLimitConfiguration.ContactPolicy;
        if (!await gate.PassesAsync(http, policy))
        {
            context.Result = ApiProblems.Create(http, ErrorCodes.CaptchaFailed).ToActionResult();
            return;
        }

        await next();
    }

    protected virtual bool IsExempt(HttpContext http) => false;
}
