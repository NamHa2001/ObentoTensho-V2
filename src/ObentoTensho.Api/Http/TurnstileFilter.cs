using Microsoft.AspNetCore.Mvc.Filters;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Http;

// SEC-22, SEC-57: form công khai dễ bị bot (đặt bàn, gửi OTP, đăng ký) phải kèm token Turnstile, kiểm ở server
public sealed class TurnstileFilter(ITurnstileVerifier verifier) : IAsyncResourceFilter
{
    public const string Header = "X-Turnstile-Token";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        var token = http.Request.Headers[Header].ToString();
        if (!await verifier.VerifyAsync(token, http.Connection.RemoteIpAddress?.ToString(), http.RequestAborted))
        {
            context.Result = ApiProblems.Create(http, ErrorCodes.CaptchaFailed).ToActionResult();
            return;
        }

        await next();
    }
}
