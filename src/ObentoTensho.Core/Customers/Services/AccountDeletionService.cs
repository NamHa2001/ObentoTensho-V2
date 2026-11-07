using FluentValidation;
using ObentoTensho.Contracts.Dtos.Account;
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

// BR-CUS-05, DB-18: xóa = ẩn danh hóa sau 7 ngày chờ; đơn, sổ cái điểm và số liệu tiền giữ nguyên, không còn định danh
public sealed class AccountDeletionService(
    IUserRepository users,
    ICustomerRepository customers,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CustomerDeletionRequest> validator)
{
    public const int BatchSize = 20;
    private const string Entity = "User";

    public async Task<Result<DateTime>> RequestAsync(Guid userId, AccountPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { PasswordHash: { } hash } user)
        {
            return Result.Failure<DateTime>(ErrorCodes.Unauthenticated);
        }

        var now = clock.UtcNow;
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > AuthPolicy.PasswordMaxLength
            || passwordHasher.Verify(hash, request.Password) == PasswordCheckResult.Failed)
        {
            await users.RegisterFailedLoginAsync(user.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            return Result.Failure<DateTime>(ErrorCodes.InvalidCredentials);
        }

        return Result.Success(await ScheduleAsync(user, "customer_request", cancellationToken));
    }

    public async Task<Result> CancelAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (user.DeletionScheduledAtUtc is not null)
        {
            user.DeletionScheduledAtUtc = null;
            audit.Record(new AuditEntry($"customers.{Entity}.deletion_cancelled", Entity, userId.ToString()));
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> RequestByStaffAsync(Guid userId, CustomerDeletionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        await ScheduleAsync(user, request.Reason.Trim(), cancellationToken);
        return Result.Success();
    }

    public async Task<int> AnonymizeDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await users.ListDueForDeletionAsync(now, BatchSize, cancellationToken);
        foreach (var id in due.Select(u => u.Id).ToArray())
        {
            await unitOfWork.InTransactionAsync(ct => AnonymizeAsync(id, now, ct), cancellationToken);
        }

        return due.Count;
    }

    private async Task<DateTime> ScheduleAsync(User user, string reason, CancellationToken cancellationToken)
    {
        var scheduled = user.DeletionScheduledAtUtc ?? clock.UtcNow + AuthPolicy.AccountDeletionGrace;
        user.DeletionScheduledAtUtc = scheduled;
        audit.Record(new AuditEntry($"customers.{Entity}.deletion_scheduled", Entity, user.Id.ToString(), After: new { scheduled, reason }));
        emailOutbox.EnqueueAccountDeletionScheduled(new AccountRecipient(user.Id, user.Email, user.FullName, user.PreferredLang, IsStaff: false), scheduled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return scheduled;
    }

    private async Task<bool> AnonymizeAsync(Guid id, DateTime now, CancellationToken cancellationToken)
    {
        // Khách có thể hủy ngay trước khi job chạy: đọc lại trong transaction
        if (await customers.FindForUpdateAsync(id, cancellationToken) is not { DeletionScheduledAtUtc: { } scheduled } user || scheduled > now)
        {
            return false;
        }

        var email = user.Email;
        var anonymous = $"deleted-{id:N}@deleted.invalid";
        await customers.AnonymizeRelatedAsync(id, email, anonymous, AuthPolicy.DeletedAccountName, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(id, RefreshTokenRevokedReason.AccountLocked, now, cancellationToken);
        user.FullName = AuthPolicy.DeletedAccountName;
        user.Email = anonymous;
        user.EmailCanonical = anonymous;
        user.PhoneNumber = null;
        user.IsPhoneVerified = false;
        user.DateOfBirth = null;
        user.DateOfBirthSetAtUtc = null;
        user.PasswordHash = null;
        user.PendingEmail = null;
        user.PendingEmailTokenHash = null;
        user.PendingEmailExpiryUtc = null;
        user.EmailVerifyTokenHash = null;
        user.ResetPasswordTokenHash = null;
        user.SecurityStamp = secureTokens.NewSecurityStamp();
        user.IsActive = false;
        user.IsDeleted = true;
        user.DeletedAtUtc = now;
        user.DeletionScheduledAtUtc = null;
        user.UpdatedAtUtc = now;
        audit.Record(new AuditEntry($"customers.{Entity}.anonymized", Entity, id.ToString(), Actor: AuditActor.System));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
