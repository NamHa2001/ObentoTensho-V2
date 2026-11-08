using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Api.Http;
using ObentoTensho.Api.Realtime;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Content.Services;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Services;

namespace ObentoTensho.Api.Controllers.Public;

// docs/srs/10 §4.6–§4.9, BR-CUS-05: khách tự quản lý tài khoản
[ApiController]
[Route("api/v1/account")]
[RequireCustomer]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(
    AccountService account,
    AccountDeletionService deletion,
    NewsletterService newsletter,
    IUserRepository users,
    ICustomerRepository customers,
    GuestClaimService guestClaims,
    HubTicketIssuer tickets) : ControllerBase
{
    private const int ReservationLimit = 50;

    // RT-09: vé một lần để nối /hubs/v1/public với danh tính khách (theo dõi đơn của mình)
    [HttpPost("realtime-ticket")]
    public IActionResult RealtimeTicket() =>
        this.DataOrProblem(tickets.Issue(User, AuthSchemes.Customer, HubIdentity.CustomerKind));

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken) =>
        this.DataOrProblem(await account.ProfileAsync(CustomerId(), cancellationToken));

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await account.UpdateProfileAsync(CustomerId(), request, cancellationToken));

    [HttpPut("change-password")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await account.ChangePasswordAsync(CustomerId(), request, HttpContext.GetClientInfo(), cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result);
        }

        RefreshTokenCookie.Write(Response, RefreshTokenCookie.CustomerName, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAtUtc);
        return Ok(new ApiResponse<AccessTokenResponse>(new AccessTokenResponse(result.Value.AccessToken.Value, result.Value.AccessToken.ExpiresAtUtc)));
    }

    [HttpPost("change-email")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await account.RequestEmailChangeAsync(CustomerId(), request, cancellationToken);
        return result.IsSuccess ? Accepted() : this.ToProblem(result);
    }

    // Liên kết mở từ email mới, có thể chưa đăng nhập
    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> ConfirmEmail(TokenRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await account.ConfirmEmailChangeAsync(request.Token, cancellationToken));

    // BR-AUTH-17: nhận đơn/đặt bàn đã làm khi chưa đăng nhập (mã + token trong thư gửi tới email của tài khoản)
    [HttpPost("claim-order")]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> ClaimOrder(ClaimGuestRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await guestClaims.ClaimOrderAsync(CustomerId(), request, cancellationToken));

    [HttpPost("claim-reservation")]
    [EnableRateLimiting(RateLimitConfiguration.LookupPolicy)]
    public async Task<IActionResult> ClaimReservation(ClaimGuestRequest request, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await guestClaims.ClaimReservationAsync(CustomerId(), request, cancellationToken));

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken) =>
        this.Data(await account.SessionsAsync(CustomerId(), cancellationToken));

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await account.RevokeSessionAsync(CustomerId(), id, cancellationToken));

    [HttpPost("sessions/revoke-all")]
    public async Task<IActionResult> RevokeAll(CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await account.RevokeAllSessionsAsync(CustomerId(), cancellationToken));

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations(CancellationToken cancellationToken) =>
        this.Data(await customers.RecentReservationsAsync(CustomerId(), ReservationLimit, cancellationToken));

    [HttpGet("newsletter")]
    public async Task<IActionResult> Newsletter(CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(CustomerId(), cancellationToken) ?? throw new InvalidOperationException("Thiếu tài khoản sau khi xác thực.");
        return this.Data(new NewsletterPreferenceDto(await newsletter.IsSubscribedAsync(user.Email, cancellationToken)));
    }

    [HttpPut("newsletter")]
    public async Task<IActionResult> SetNewsletter(NewsletterPreferenceDto request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(CustomerId(), cancellationToken) ?? throw new InvalidOperationException("Thiếu tài khoản sau khi xác thực.");
        return this.NoContentOrProblem(await newsletter.SetForAccountAsync(user, request.Subscribed, HttpContext.GetClientInfo().IpAddress, cancellationToken));
    }

    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitConfiguration.LoginPolicy)]
    public async Task<IActionResult> Delete(AccountPasswordRequest request, CancellationToken cancellationToken) =>
        this.DataOrProblem(await deletion.RequestAsync(CustomerId(), request, cancellationToken));

    [HttpPost("delete/cancel")]
    public async Task<IActionResult> CancelDelete(CancellationToken cancellationToken) =>
        this.NoContentOrProblem(await deletion.CancelAsync(CustomerId(), cancellationToken));

    private Guid CustomerId() => User.CustomerId() ?? throw new InvalidOperationException("Thiếu định danh khách sau khi xác thực.");
}
