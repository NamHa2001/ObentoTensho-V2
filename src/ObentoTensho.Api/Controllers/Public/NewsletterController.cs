using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Content;
using ObentoTensho.Core.Content.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/17 §3.6: mọi thay đổi trạng thái đều là POST để máy quét email không tự xác nhận hay tự hủy (BR-CNT-22)
[ApiController]
[Route("api/v1/newsletter")]
[AllowAnonymous]
public sealed class NewsletterController(NewsletterService newsletter) : ControllerBase
{
    [HttpPost("subscribe")]
    [EnableRateLimiting(RateLimitConfiguration.ContactPolicy)]
    [ServiceFilter<TurnstileFilter>]
    public async Task<IActionResult> Subscribe(NewsletterSubscribeRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await newsletter.SubscribeAsync(request, cancellationToken));

    [HttpPost("confirm")]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Confirm(NewsletterTokenRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await newsletter.ConfirmAsync(request.Token, HttpContext.GetClientInfo().IpAddress, cancellationToken));

    [HttpPost("unsubscribe")]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Unsubscribe(NewsletterTokenRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await newsletter.UnsubscribeAsync(request.Token, cancellationToken));

    // BR-CNT-26, RFC 8058: nhà cung cấp hộp thư POST "List-Unsubscribe=One-Click" tới địa chỉ trong header
    [HttpPost("one-click")]
    public async Task<IActionResult> OneClick([FromQuery] string token, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await newsletter.UnsubscribeAsync(token, cancellationToken));
}
