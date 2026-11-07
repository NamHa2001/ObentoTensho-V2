using FluentValidation;
using ObentoTensho.Contracts.Dtos;
using ObentoTensho.Contracts.Dtos.Staff;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Audit.Abstractions;
using ObentoTensho.Core.Audit.Domain;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-STF-01..04. Không xóa cứng (BR-STF-16), không có API đặt hộ mật khẩu; khóa/đổi vai trò/đặt lại có hiệu lực ngay (BR-STF-05)
public sealed class StaffAdminService(
    IStaffRepository staff,
    IRefreshTokenRepository refreshTokens,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    IAuditTrail audit,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<CreateStaffRequest> createValidator,
    IValidator<UpdateStaffRequest> updateValidator)
{
    public const int MaxPageSize = 100;
    private const string Entity = "Staff";

    public async Task<PagedResponse<StaffDto>> ListAsync(StaffQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var (items, total) = await staff.ListAsync(query with { Page = page, PageSize = pageSize }, cancellationToken);
        return new PagedResponse<StaffDto>(items.Select(ToDto).ToArray(), new PageMeta(page, pageSize, total));
    }

    public async Task<Result<StaffDto>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await staff.FindByIdAsync(id, cancellationToken) is { } member
            ? Result.Success(ToDto(member))
            : Result.Failure<StaffDto>(ErrorCodes.NotFound);

    public async Task<Result<StaffMeDto>> MeAsync(Guid id, CancellationToken cancellationToken) =>
        await staff.FindByIdAsync(id, cancellationToken) is { } m
            ? Result.Success(new StaffMeDto(m.Id, m.FullName, m.Email, m.Role, StaffCapabilities.For(m.Role), m.TotpEnabled))
            : Result.Failure<StaffMeDto>(ErrorCodes.NotFound);

    public async Task<Result<StaffDto>> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<StaffDto>();
        }

        // BR-STF-16: tính cả nhân viên đã vô hiệu hóa
        var email = EmailAddress.Normalize(request.Email);
        if (await staff.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return Result.Failure<StaffDto>(ErrorCodes.EmailTaken);
        }

        var now = clock.UtcNow;
        var invite = secureTokens.Create();
        var member = new Staff
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = request.Role,
            MustChangePassword = false,
            InviteTokenHash = invite.Hash,
            InviteExpiryUtc = now + AuthPolicy.StaffInviteLifetime,
            SecurityStamp = secureTokens.NewSecurityStamp(),
            CreatedAtUtc = now,
        };
        return await unitOfWork.TryCreateAsync(async ct =>
        {
            staff.Add(member);
            emailOutbox.EnqueueStaffInvite(Recipient(member), invite.Value);
            audit.Record(new AuditEntry($"staff.{Entity}.created", Entity, member.Id.ToString(), After: Snapshot(member)));
            await unitOfWork.SaveChangesAsync(ct);
            return ToDto(member);
        }, ErrorCodes.EmailTaken, cancellationToken);
    }

    public async Task<Result<StaffDto>> UpdateAsync(Guid id, UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<StaffDto>();
        }

        return await unitOfWork.TryInTransactionAsync(async ct =>
        {
            var superAdmins = await staff.CountActiveSuperAdminsForUpdateAsync(ct);
            if (await staff.FindForUpdateAsync(id, ct) is not { } member)
            {
                return Result.Failure<StaffDto>(ErrorCodes.NotFound);
            }

            var roleChanged = member.Role != request.Role;
            if (roleChanged && IsOnlySuperAdmin(member, superAdmins))
            {
                return Result.Failure<StaffDto>(ErrorCodes.LastSuperAdmin);
            }

            var before = Snapshot(member);
            member.FullName = request.FullName.Trim();
            member.Role = request.Role;
            audit.Record(new AuditEntry($"staff.{Entity}.updated", Entity, member.Id.ToString(), before, Snapshot(member)));
            if (roleChanged)
            {
                await EndSessionsAsync(member, RefreshTokenRevokedReason.AccountLocked, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(ToDto(member));
        }, cancellationToken);
    }

    public Task<Result<StaffDto>> SetStatusAsync(Guid id, StaffStatusRequest request, Guid actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return unitOfWork.TryInTransactionAsync(async ct =>
        {
            var superAdmins = await staff.CountActiveSuperAdminsForUpdateAsync(ct);
            if (await staff.FindForUpdateAsync(id, ct) is not { } member)
            {
                return Result.Failure<StaffDto>(ErrorCodes.NotFound);
            }

            if (!request.IsActive && id == actorId)
            {
                return Result.Failure<StaffDto>(ErrorCodes.CannotChangeSelf);
            }

            if (!request.IsActive && IsOnlySuperAdmin(member, superAdmins))
            {
                return Result.Failure<StaffDto>(ErrorCodes.LastSuperAdmin);
            }

            var before = Snapshot(member);
            member.IsActive = request.IsActive;
            audit.Record(new AuditEntry($"staff.{Entity}.status_changed", Entity, member.Id.ToString(), before, Snapshot(member)));
            if (!request.IsActive)
            {
                await EndSessionsAsync(member, RefreshTokenRevokedReason.AccountLocked, ct);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(ToDto(member));
        }, cancellationToken);
    }

    public async Task<Result> ResendInviteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await staff.FindForUpdateAsync(id, cancellationToken) is not { } member)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (member.PasswordHash is not null)
        {
            return Result.Failure(ErrorCodes.InviteAlreadyAccepted);
        }

        var invite = secureTokens.Create();
        member.InviteTokenHash = invite.Hash;
        member.InviteExpiryUtc = clock.UtcNow + AuthPolicy.StaffInviteLifetime;
        emailOutbox.EnqueueStaffInvite(Recipient(member), invite.Value);
        audit.Record(new AuditEntry($"staff.{Entity}.invite_resent", Entity, member.Id.ToString()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> RevokeSessionsAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAccessAsync(id, $"staff.{Entity}.sessions_revoked", _ => Task.CompletedTask, cancellationToken);

    // Đăng nhập lần sau phải thiết lập lại nếu vai trò bắt buộc 2FA
    public Task<Result> ResetTwoFactorAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeAccessAsync(id, $"staff.{Entity}.two_factor_reset", async member =>
        {
            member.TotpEnabled = false;
            member.TotpSecretEncrypted = null;
            member.TotpLastUsedStep = null;
            await staff.ReplaceRecoveryCodesAsync(member.Id, [], cancellationToken);
        }, cancellationToken);

    private async Task<Result> ChangeAccessAsync(Guid id, string action, Func<Staff, Task> change, CancellationToken cancellationToken)
    {
        var result = await unitOfWork.TryInTransactionAsync(async ct =>
        {
            if (await staff.FindForUpdateAsync(id, ct) is not { } member)
            {
                return Result.Failure<bool>(ErrorCodes.NotFound);
            }

            await change(member);
            audit.Record(new AuditEntry(action, Entity, member.Id.ToString()));
            await EndSessionsAsync(member, RefreshTokenRevokedReason.AccountLocked, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(true);
        }, cancellationToken);
        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorCode ?? ErrorCodes.InternalError);
    }

    // SEC-06: đổi stamp làm access token và mã step-up mất hiệu lực, thu hồi refresh token
    private async Task EndSessionsAsync(Staff member, RefreshTokenRevokedReason reason, CancellationToken cancellationToken)
    {
        member.SecurityStamp = secureTokens.NewSecurityStamp();
        await refreshTokens.RevokeAllForStaffAsync(member.Id, reason, clock.UtcNow, cancellationToken);
    }

    private static bool IsOnlySuperAdmin(Staff member, int superAdmins) =>
        member is { Role: StaffRole.SuperAdmin, IsActive: true } && superAdmins <= 1;

    private static AccountRecipient Recipient(Staff member) =>
        new(member.Id, member.Email, member.FullName, AuthPolicy.DefaultLanguage, IsStaff: true);

    private static object Snapshot(Staff m) => new { m.FullName, m.Email, Role = m.Role.ToString(), m.IsActive };

    private static StaffDto ToDto(Staff m) =>
        new(m.Id, m.FullName, m.Email, m.Role, m.IsActive, m.TotpEnabled, m.PasswordHash is null, m.LastLoginAtUtc, m.CreatedAtUtc);
}
