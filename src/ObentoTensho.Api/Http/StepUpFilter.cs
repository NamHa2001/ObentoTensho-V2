using Microsoft.AspNetCore.Mvc.Filters;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Http;

// SEC-61: thao tác rủi ro cao phải kèm mã xác thực lại còn hạn của chính người gọi
public sealed class StepUpFilter(StaffStepUpService stepUp) : IAsyncResourceFilter
{
    public const string Header = "X-Step-Up-Token";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        if (!await stepUp.IsValidAsync(http.User.StaffId(), http.Request.Headers[Header].ToString(), http.RequestAborted))
        {
            context.Result = ApiProblems.Create(http, ErrorCodes.StepUpRequired).ToActionResult();
            return;
        }

        await next();
    }
}
