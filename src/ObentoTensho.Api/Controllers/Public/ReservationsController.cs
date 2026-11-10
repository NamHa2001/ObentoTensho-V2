using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Core.Reservations.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/11 §8 (công khai). Đặt bàn cần tài khoản khách đã xác minh SĐT; xem chỗ trống, tra cứu, hủy bằng token không cần.
[ApiController]
[Route("api/v1/reservations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationsController(ReservationSchedule schedule, PublicReservationService reservations) : ControllerBase
{
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<IActionResult> Availability([FromQuery, BindRequired] DateOnly date, CancellationToken cancellationToken) =>
        this.Data(await schedule.GetAvailabilityAsync(date, cancellationToken));

    [HttpPost]
    [RequireCustomer]
    [EnableRateLimiting(RateLimitConfiguration.ReservationPolicy)]
    public async Task<IActionResult> Create(CreateReservationRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await reservations.CreateAsync(request, User.CustomerId() ?? Guid.Empty, cancellationToken));

    [HttpPost("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Lookup(LookupReservationRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await reservations.LookupAsync(request, cancellationToken));

    [HttpPost("{code}/cancel")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> Cancel(string code, CancelReservationRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await reservations.CancelAsync(code, request, User.CustomerId(), cancellationToken));
}
