using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-01: phản hồi luôn trung tính, không cấp phiên, dù email mới hay đã có (BR-AUTH-14, D-13)
public sealed class CustomerRegistrationService(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    IAccountEmailOutbox emailOutbox,
    IClock clock,
    IValidator<RegisterRequest> validator)
{
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure();
        }

        var email = EmailAddress.Normalize(request.Email);
        var existing = await users.FindByEmailAsync(email, cancellationToken);

        // Băm mật khẩu ở cả hai nhánh để thời gian phản hồi như nhau
        var passwordHash = passwordHasher.Hash(request.Password);

        if (existing is not null)
        {
            if (!existing.IsDeleted)
            {
                emailOutbox.EnqueueDuplicateRegistrationNotice(existing, clock.TodayLocal);
            }
        }
        else
        {
            var verification = secureTokens.Create();
            var user = CreateUser(request, email, passwordHash, verification.Hash);
            users.Add(user);
            emailOutbox.EnqueueEmailVerification(user, verification.Value);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Hai yêu cầu cùng email chạy song song, hoặc đã gửi thông báo trùng hôm nay: vẫn trả trung tính
        }

        return Result.Success();
    }

    private User CreateUser(RegisterRequest request, string email, string passwordHash, string verificationTokenHash)
    {
        var now = clock.UtcNow;
        VietnamesePhoneNumber.TryNormalize(request.PhoneNumber, out var phone);

        return new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            EmailCanonical = EmailAddress.Canonicalize(email),
            PhoneNumber = phone,
            PasswordHash = passwordHash,
            PasswordChangedAtUtc = now,
            DateOfBirth = request.DateOfBirth,
            DateOfBirthSetAtUtc = request.DateOfBirth is null ? null : now,
            EmailVerifyTokenHash = verificationTokenHash,
            EmailVerifyExpiryUtc = now + AuthPolicy.EmailVerificationLifetime,
            SecurityStamp = secureTokens.NewSecurityStamp(),
            PreferredLang = request.PreferredLang ?? AuthPolicy.DefaultLanguage,
            CreatedAtUtc = now,
        };
    }
}
