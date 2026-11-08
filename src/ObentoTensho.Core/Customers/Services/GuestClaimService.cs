using System.Globalization;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Loyalty.Abstractions;
using ObentoTensho.Core.Loyalty.Domain;
using ObentoTensho.Core.Loyalty.Services;
using ObentoTensho.Core.Orders.Abstractions;
using ObentoTensho.Core.Orders.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Core.Customers.Services;

// BR-AUTH-17: khách đã xác thực email gắn đơn/đặt bàn vãng lai vào tài khoản. Chỉ bản ghi cùng email và chứng minh được
// bằng token trong thư gửi tới email đó; không gắn theo SĐT. Điểm đơn cũ chỉ cộng khi bật loyalty.claimGuestOrders.
// Sai mã/token/email trả cùng NOT_FOUND và tính vào giới hạn để không dò được
public sealed class GuestClaimService(
    IUserRepository users,
    IOrderRepository orders,
    IReservationRepository reservations,
    ISecureTokenService secureTokens,
    IActionThrottle throttle,
    LoyaltyLedger ledger,
    ILoyaltySettings loyaltySettings,
    IAuditTrail audit,
    IUnitOfWork unitOfWork)
{
    private const int MaxFailures = 10;
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    public async Task<Result> ClaimOrderAsync(Guid userId, ClaimGuestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await CheckUserAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.EmailNotVerified);
        }

        if (throttle.Count(Key(userId)) >= MaxFailures)
        {
            return Result.Failure(ErrorCodes.RateLimited);
        }

        var claimed = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            var order = await orders.FindByCodeAsync(OrderCodes.Normalize(request.Code), withItems: false, ct);
            if (order is null || !Matches(order.CustomerEmail, order.AccessTokenHash, user, request.Token) || (order.UserId is { } owner && owner != userId))
            {
                return Result.Failure<bool>(ErrorCodes.NotFound);
            }

            if (order.UserId is null)
            {
                order.UserId = userId;
                await AwardPointsAsync(order, userId, ct);
                audit.Record(new AuditEntry("customers.guest_order.claimed", "order", order.Id.ToString("D", CultureInfo.InvariantCulture), After: new { userId, order.PointsEarned }));
                await unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success(true);
        }, cancellationToken);
        return claimed.IsSuccess ? Result.Success() : Fail(userId, claimed.ErrorCode);
    }

    public async Task<Result> ClaimReservationAsync(Guid userId, ClaimGuestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await CheckUserAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.EmailNotVerified);
        }

        if (throttle.Count(Key(userId)) >= MaxFailures)
        {
            return Result.Failure(ErrorCodes.RateLimited);
        }

        var reservation = await reservations.FindByCodeForUpdateAsync(ReservationCodes.Normalize(request.Code), cancellationToken);
        if (reservation is null || reservation.GuestEmail is null
            || !Matches(reservation.GuestEmail, reservation.AccessTokenHash, user, request.Token)
            || (reservation.UserId is { } owner && owner != userId))
        {
            return Fail(userId, ErrorCodes.NotFound);
        }

        if (reservation.UserId is null)
        {
            reservation.UserId = userId;
            audit.Record(new AuditEntry("customers.guest_reservation.claimed", "reservation", reservation.Id.ToString("D", CultureInfo.InvariantCulture), After: new { userId }));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    // Điểm hồi tố chỉ cho đơn đã hoàn thành (đã thanh toán), một lần — sổ cái tham chiếu theo mã đơn
    private async Task AwardPointsAsync(Order order, Guid userId, CancellationToken cancellationToken)
    {
        if (!loyaltySettings.ClaimGuestOrders || order.Status != OrderStatus.Completed || order.PointsEarned > 0)
        {
            return;
        }

        var points = LoyaltyRules.OrderPoints(order.TotalAmount, loyaltySettings.VndPerPoint);
        order.PointsEarned = await ledger.EarnAsync(userId, LoyaltyTransactionType.OrderEarn, order.OrderCode, points, note: "Gắn đơn vãng lai", cancellationToken);
    }

    private async Task<User?> CheckUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.FindByIdAsync(userId, cancellationToken) is { IsEmailVerified: true, IsDeleted: false } user ? user : null;

    private bool Matches(string? recordEmail, string? tokenHash, User user, string? token) =>
        recordEmail is not null
        && string.Equals(EmailAddress.Normalize(recordEmail), EmailAddress.Normalize(user.Email), StringComparison.Ordinal)
        && tokenHash is not null
        && !string.IsNullOrEmpty(token)
        && secureTokens.HashEquals(tokenHash, secureTokens.Hash(token));

    private Result Fail(Guid userId, string? errorCode)
    {
        if (string.Equals(errorCode, ErrorCodes.NotFound, StringComparison.Ordinal))
        {
            throttle.Increment(Key(userId), FailureWindow);
        }

        return Result.Failure(errorCode ?? ErrorCodes.NotFound);
    }

    private static string Key(Guid userId) => $"guest-claim:{userId:N}";
}
