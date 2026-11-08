using FluentValidation;
using ObentoTensho.Contracts.Dtos.Account;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Domain;
using ObentoTensho.Core.Media.Abstractions;
using ObentoTensho.Core.Media.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Customers.Services;

// FR-AUTH-06, FR-AUTH-08, FR-AUTH-09, BR-AUTH-15: khách tự quản lý tài khoản; định danh luôn lấy từ access token
public sealed class AccountService(
    IUserRepository users,
    ICustomerRepository customers,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    SessionFactory sessions,
    IAccountEmailOutbox emailOutbox,
    IImageRepository images,
    ImageMapper imageMapper,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<UpdateProfileRequest> profileValidator,
    IValidator<ChangeOwnPasswordRequest> passwordValidator,
    IValidator<ChangeEmailRequest> emailValidator)
{
    public async Task<Result<ProfileDto>> ProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        await users.FindByIdAsync(userId, cancellationToken) is { IsDeleted: false } user
            ? Result.Success(await ToDtoAsync(user, cancellationToken))
            : Result.Failure<ProfileDto>(ErrorCodes.NotFound);

    public async Task<Result<ProfileDto>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await profileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<ProfileDto>();
        }

        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure<ProfileDto>(ErrorCodes.NotFound);
        }

        if (!TryChangeDateOfBirth(user, request.DateOfBirth))
        {
            return Result.Failure<ProfileDto>(ErrorCodes.DateOfBirthLocked);
        }

        VietnamesePhoneNumber.TryNormalize(request.PhoneNumber, out var phone);
        if (!string.Equals(phone, user.PhoneNumber, StringComparison.Ordinal))
        {
            user.PhoneNumber = phone;
            user.IsPhoneVerified = false;
        }

        user.FullName = request.FullName.Trim();
        user.PreferredLang = request.PreferredLang ?? user.PreferredLang;
        user.UpdatedAtUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(await ToDtoAsync(user, cancellationToken));
    }

    // Thu hồi mọi phiên rồi cấp phiên mới cho thiết bị đang dùng (FR-AUTH-06: chỉ các phiên khác bị đăng xuất)
    public async Task<Result<AuthSession>> ChangePasswordAsync(Guid userId, ChangeOwnPasswordRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var validation = await passwordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<AuthSession>();
        }

        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { PasswordHash: { } currentHash } user)
        {
            return Result.Failure<AuthSession>(ErrorCodes.Unauthenticated);
        }

        var now = clock.UtcNow;
        if (passwordHasher.Verify(currentHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            await users.RegisterFailedLoginAsync(user.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            return Result.Failure<AuthSession>(ErrorCodes.InvalidCredentials);
        }

        if (PasswordRules.ContainsEmail(request.NewPassword, user.Email))
        {
            return Result.Failure<AuthSession>(ErrorCodes.ValidationFailed, new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["newPassword"] = [FieldErrorCodes.PasswordContainsEmail],
            });
        }

        return await unitOfWork.InTransactionAsync(async ct =>
        {
            var tracked = await customers.FindForUpdateAsync(userId, ct) ?? throw new InvalidOperationException("Tài khoản vừa bị xóa.");
            tracked.PasswordHash = passwordHasher.Hash(request.NewPassword);
            tracked.PasswordChangedAtUtc = now;
            tracked.SecurityStamp = secureTokens.NewSecurityStamp();
            await refreshTokens.RevokeAllForUserAsync(tracked.Id, RefreshTokenRevokedReason.PasswordChanged, now, ct);
            emailOutbox.EnqueuePasswordChanged(Recipient(tracked), now);
            var session = sessions.CreateForUser(tracked, Guid.CreateVersion7(), sessions.NewRefreshToken(), client);
            await unitOfWork.SaveChangesAsync(ct);
            return Result.Success(session);
        }, cancellationToken);
    }

    // BR-AUTH-14: email mới đã có người dùng thì vẫn trả thành công, không gửi thư, để không dò được tài khoản
    public async Task<Result> RequestEmailChangeAsync(Guid userId, ChangeEmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await emailValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { PasswordHash: { } currentHash } user)
        {
            return Result.Failure(ErrorCodes.Unauthenticated);
        }

        var now = clock.UtcNow;
        if (passwordHasher.Verify(currentHash, request.CurrentPassword) == PasswordCheckResult.Failed)
        {
            await users.RegisterFailedLoginAsync(user.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            return Result.Failure(ErrorCodes.InvalidCredentials);
        }

        var email = EmailAddress.Normalize(request.NewEmail);
        if (string.Equals(email, user.Email, StringComparison.Ordinal) || await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return Result.Success();
        }

        var token = secureTokens.Create();
        user.PendingEmail = email;
        user.PendingEmailTokenHash = token.Hash;
        user.PendingEmailExpiryUtc = now + AuthPolicy.EmailVerificationLifetime;
        emailOutbox.EnqueueEmailChangeVerify(Recipient(user), email, token.Value);
        emailOutbox.EnqueueEmailChangeNotice(Recipient(user), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(string? token, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var user = string.IsNullOrEmpty(token) ? null : await users.FindByPendingEmailHashForUpdateAsync(secureTokens.Hash(token), cancellationToken);
        if (user is not { IsDeleted: false, PendingEmail: { } email } || user.PendingEmailExpiryUtc <= now)
        {
            return Result.Failure(ErrorCodes.TokenInvalid);
        }

        user.Email = email;
        user.EmailCanonical = EmailAddress.Canonicalize(email);
        user.IsEmailVerified = true;
        user.PendingEmail = null;
        user.PendingEmailTokenHash = null;
        user.PendingEmailExpiryUtc = null;
        user.SecurityStamp = secureTokens.NewSecurityStamp();
        user.UpdatedAtUtc = now;
        var saved = await unitOfWork.TrySaveAsync(ErrorCodes.TokenInvalid, cancellationToken);
        if (saved.IsSuccess)
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokedReason.PasswordChanged, now, cancellationToken);
        }

        return saved;
    }

    public async Task<IReadOnlyList<AccountSessionDto>> SessionsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await refreshTokens.ListActiveForUserAsync(userId, clock.UtcNow, cancellationToken))
            .Select(t => new AccountSessionDto(t.FamilyId, t.UserAgent, t.CreatedByIp, t.CreatedAtUtc))
            .ToArray();

    public async Task<Result> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var active = await refreshTokens.ListActiveForUserAsync(userId, now, cancellationToken);
        if (!active.Any(t => t.FamilyId == sessionId))
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        await refreshTokens.RevokeFamilyAsync(sessionId, RefreshTokenRevokedReason.Logout, now, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        user.SecurityStamp = secureTokens.NewSecurityStamp();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(userId, RefreshTokenRevokedReason.Logout, clock.UtcNow, cancellationToken);
        return Result.Success();
    }

    // BR-LOY-20: đặt lần đầu luôn được; đổi chỉ một lần và không đổi khi đang gần ngày sinh cũ hoặc mới
    private bool TryChangeDateOfBirth(User user, DateOnly? next)
    {
        if (next is not { } date || date == user.DateOfBirth)
        {
            return next is not null || user.DateOfBirth is null;
        }

        var now = clock.UtcNow;
        if (user.DateOfBirth is { } current)
        {
            var today = clock.TodayLocal;
            if (user.DateOfBirthEditCount >= 1
                || BirthdayRules.IsNear(current, today, AuthPolicy.DateOfBirthLockDays)
                || BirthdayRules.IsNear(date, today, AuthPolicy.DateOfBirthLockDays))
            {
                return false;
            }

            user.DateOfBirthEditCount++;
        }

        user.DateOfBirth = date;
        user.DateOfBirthSetAtUtc = now;
        return true;
    }

    private static AccountRecipient Recipient(User u) => new(u.Id, u.Email, u.FullName, u.PreferredLang, IsStaff: false);

    private async Task<ProfileDto> ToDtoAsync(User u, CancellationToken cancellationToken)
    {
        var avatar = u.AvatarImageId is { } id ? await images.FindAsync(id, cancellationToken) : null;
        return new(
            u.FullName, u.Email, u.PendingEmail, u.PhoneNumber, u.DateOfBirth, u.PreferredLang, u.IsEmailVerified, u.MemberTier, u.CurrentPoints,
            u.DeletionScheduledAtUtc, avatar is null ? null : imageMapper.ToDto(avatar));
    }
}
