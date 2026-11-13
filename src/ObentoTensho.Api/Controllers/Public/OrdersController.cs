using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Delivery;
using ObentoTensho.Contracts.Dtos.Orders;
using ObentoTensho.Core.Delivery.Services;
using ObentoTensho.Core.Orders.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/12 §8 (công khai). Đặt món cần tài khoản khách đã xác minh SĐT; báo giá, theo dõi, hủy bằng token không cần.
[ApiController]
[Route("api/v1/orders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersController(PublicOrderService orders, DeliveryQuoteService delivery) : ControllerBase
{
    // Phí ship theo quãng đường thật; mỗi lần gọi có thể tốn phí Google nên giới hạn như tra cứu
    [HttpPost("delivery-quote")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> DeliveryQuote(DeliveryQuoteRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await delivery.QuoteAsync(request.Address, cancellationToken));

    [HttpGet("pickup-times")]
    [AllowAnonymous]
    public async Task<IActionResult> PickupTimes([FromQuery, BindRequired] DateOnly date, CancellationToken cancellationToken) =>
        this.Data(await orders.PickupTimesAsync(date, cancellationToken));

    // Mở giỏ ⇒ kiểm lại giá/tình trạng; không tạo gì nên chỉ theo giới hạn chung
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> Quote(QuoteOrderRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await orders.QuoteAsync(request, User.CustomerId(), cancellationToken));

    [HttpPost]
    [RequireCustomer]
    [EnableRateLimiting(RateLimitConfiguration.OrderPolicy)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await orders.CreateAsync(request, User.CustomerId() ?? Guid.Empty, cancellationToken));

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
