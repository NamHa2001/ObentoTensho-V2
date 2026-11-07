using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Public;

// BR-CNT-33: form liên hệ công khai
[ApiController]
[Route("api/v1/contact")]
[AllowAnonymous]
public sealed class ContactController(ContactService contacts) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitConfiguration.ContactPolicy)]
    [ServiceFilter<TurnstileFilter>]
    public async Task<IActionResult> Submit(ContactRequest request, CancellationToken cancellationToken)
    {
        var result = await contacts.SubmitAsync(request, HttpContext.GetClientInfo().IpAddress, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }
}
