using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/12 §8 (công khai). Không cần tài khoản; đang đăng nhập thì đơn gắn vào tài khoản.
[ApiController]
[Route("api/v1/orders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersController(PublicOrderService orders) : ControllerBase
{
    [HttpGet("pickup-times")]
    [AllowAnonymous]
    public async Task<IActionResult> PickupTimes([FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        this.Data(await orders.PickupTimesAsync(date, cancellationToken));

    // Mở giỏ ⇒ kiểm lại giá/tình trạng; không tạo gì nên chỉ theo giới hạn chung
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> Quote(QuoteOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.QuoteAsync(request, User.CustomerId(), cancellationToken));

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.OrderPolicy)]
    [ServiceFilter<TurnstileFilter>]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await orders.CreateAsync(request, User.CustomerId(), cancellationToken));

    [HttpPost("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Lookup(LookupOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.LookupAsync(request, cancellationToken));

    // SEC-46: token trong thân request, không trên URL
    [HttpPost("track")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Track(TrackOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.TrackAsync(request, cancellationToken));

    [HttpPost("{code}/cancel")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Cancel(string code, CancelOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.CancelAsync(code, request, User.CustomerId(), cancellationToken));
}
