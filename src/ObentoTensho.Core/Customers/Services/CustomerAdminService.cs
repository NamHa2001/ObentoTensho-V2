using System.Globalization;
using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Customers;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Customers.Services;

// FR-CUS-01..05. Xem chi tiết có PII nên ghi audit và giới hạn theo nhân viên (BR-CUS-07, BR-CUS-09)
public sealed class CustomerAdminService(
    ICustomerRepository customers,
    IUserRepository users,
    IStaffRepository staff,
    IRefreshTokenRepository refreshTokens,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    IAdminEmailOutbox adminEmails,
    IActionThrottle throttle,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CustomerStatusRequest> statusValidator)
{
    public const int MaxPageSize = 100;
    private const int RecentLimit = 10;
    private const int VoucherLimit = 20;
    private const int ViewLimitPerHour = 100;
    private const string Entity = "User";

    public async Task<PagedResponse<CustomerListItemDto>> ListAsync(
        string? search,
        MemberTier? tier,
        bool? isActive,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var fromUtc = from is { } start ? clock.ToUtc(start, TimeOnly.MinValue) : (DateTime?)null;
        var toUtc = to is { } end ? clock.ToUtc(end.AddDays(1), TimeOnly.MinValue) : (DateTime?)null;
        var (items, total) = await customers.ListAsync(new CustomerQuery(search, tier, isActive, fromUtc, toUtc, page, pageSize), cancellationToken);
        return new PagedResponse<CustomerListItemDto>(
            items.Select(u => new CustomerListItemDto(
                u.Id,
                u.FullName,
                AuditRedactor.MaskEmail(u.Email),
                u.PhoneNumber is { } phone ? AuditRedactor.MaskPhone(phone) : null,
                u.MemberTier,
                u.CurrentPoints,
                u.IsActive,
                u.IsEmailVerified,
                u.CreatedAtUtc)).ToArray(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<CustomerDetailDto>> GetAsync(Guid id, Guid staffId, CancellationToken cancellationToken)
    {
        var key = $"customer-view:{staffId:N}";
        if (throttle.Count(key) >= ViewLimitPerHour)
        {
            await AlertOverLimitAsync(staffId, cancellationToken);
            return Result.Failure<CustomerDetailDto>(ErrorCodes.RateLimited);
        }

        if (await users.FindByIdAsync(id, cancellationToken) is not { IsDeleted: false } user)
        {
            return Result.Failure<CustomerDetailDto>(ErrorCodes.NotFound);
        }

        throttle.Increment(key, TimeSpan.FromHours(1));
        var reservations = await customers.RecentReservationsAsync(id, RecentLimit, cancellationToken);
        var orders = await customers.RecentOrdersAsync(id, RecentLimit, cancellationToken);
        var vouchers = await customers.VouchersAsync(id, VoucherLimit, cancellationToken);
        audit.Record(new AuditEntry($"customers.{Entity}.viewed", Entity, id.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new CustomerDetailDto(
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            user.DateOfBirth,
            user.MemberTier,
            user.CurrentPoints,
            user.PointsDebt,
            user.AccumulatedPoints,
            user.IsActive,
            user.IsEmailVerified,
            user.CreatedAtUtc,
            reservations,
            orders,
            vouchers));
    }

    // BR-CUS-01, BR-CUS-08: khóa thu hồi mọi phiên, có lý do, audit và thư báo khách
    public async Task<Result> SetStatusAsync(Guid id, CustomerStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await statusValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var changed = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await customers.FindForUpdateAsync(id, ct) is not { } user)
            {
                return Result.Failure<bool>(ErrorCodes.NotFound);
            }

            if (user.IsActive == request.IsActive)
            {
                return Result.Success(true);
            }

            var before = new { user.IsActive };
            user.IsActive = request.IsActive;
            user.UpdatedAtUtc = clock.UtcNow;
            var recipient = new AccountRecipient(user.Id, user.Email, user.FullName, user.PreferredLang, IsStaff: false);
            if (request.IsActive)
            {
                emailOutbox.EnqueueAccountUnlocked(recipient, user.UpdatedAtUtc.Value);
            }
            else
            {
                user.SecurityStamp = secureTokens.NewSecurityStamp();
                await refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokedReason.AccountLocked, clock.UtcNow, ct);
                emailOutbox.EnqueueAccountLocked(recipient, user.UpdatedAtUtc.Value);
            }

            audit.Record(new AuditEntry($"customers.{Entity}.status_changed", Entity, id.ToString(), before, new { user.IsActive, request.Reason }));
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }, cancellationToken);
        return changed.IsSuccess ? Result.Success() : Result.Failure(changed.ErrorCode ?? ErrorCodes.InternalError);
    }

    private async Task AlertOverLimitAsync(Guid staffId, CancellationToken cancellationToken)
    {
        var hour = clock.UtcNow.ToString("yyyyMMddHH", CultureInfo.InvariantCulture);
        var name = (await staff.FindByIdAsync(staffId, cancellationToken))?.FullName ?? staffId.ToString();
        foreach (var email in await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken))
        {
            adminEmails.EnqueueAlert(email, $"{name} vượt {ViewLimitPerHour} lượt xem thông tin khách trong một giờ.", $"customer-view:{staffId:N}:{hour}");
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Đã báo trong giờ này
        }
    }
}
