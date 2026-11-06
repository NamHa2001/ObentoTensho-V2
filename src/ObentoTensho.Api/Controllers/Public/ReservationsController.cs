using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Contracts.Dtos.Reservations;
using ObentoTensho.Core.Reservations.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/11 §8 (công khai). Không cần tài khoản; đang đăng nhập thì đặt bàn gắn vào tài khoản.
[ApiController]
[Route("api/v1/reservations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReservationsController(ReservationSchedule schedule, PublicReservationService reservations) : ControllerBase
{
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<IActionResult> Availability([FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        this.Data(await schedule.GetAvailabilityAsync(date, cancellationToken));

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.ReservationPolicy)]
    [ServiceFilter<TurnstileFilter>]
    public async Task<IActionResult> Create(CreateReservationRequest request, CancellationToken cancellationToken) =>
        this.CreatedOrProblem(await reservations.CreateAsync(request, User.CustomerId(), cancellationToken));

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
