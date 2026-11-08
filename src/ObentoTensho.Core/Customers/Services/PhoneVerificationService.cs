using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Verification.Services;

namespace ObentoTensho.Core.Customers.Services;

// Chủ dự án 2026-11-08 (thay D-39): đặt bàn, đặt món và voucher sinh nhật chỉ dành cho tài khoản đã xác minh SĐT
// bằng OTP SMS. Chỉ nhận SĐT Việt Nam; một SĐT chỉ một tài khoản xác minh (chỉ mục UNIQUE có lọc giữ khi đua)
public sealed class PhoneVerificationService(
    IUserRepository users,
    ICustomerRepository customers,
    OtpService otp,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> SendCodeAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(userId, cancellationToken) is not { IsDeleted: false, IsActive: true } user)
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (user.IsPhoneVerified)
        {
            return Result.Success();
        }

        if (string.IsNullOrEmpty(user.PhoneNumber))
        {
            return Result.Failure(ErrorCodes.ValidationFailed);
        }

        // Không tốn SMS cho số đã thuộc tài khoản khác
        if (await users.PhoneVerifiedByOtherAsync(user.PhoneNumber, user.Id, cancellationToken))
        {
            return Result.Failure(ErrorCodes.PhoneTaken);
        }

        return await otp.SendSmsAsync(OtpPurpose.AccountPhone, user.PhoneNumber, ipAddress, cancellationToken);
    }

    public async Task<Result> VerifyAsync(Guid userId, string? code, CancellationToken cancellationToken)
    {
        if (await customers.FindForUpdateAsync(userId, cancellationToken) is not { IsActive: true } user || string.IsNullOrEmpty(user.PhoneNumber))
        {
            return Result.Failure(ErrorCodes.NotFound);
        }

        if (user.IsPhoneVerified)
        {
            return Result.Success();
        }

        if (!await otp.VerifySmsAsync(OtpPurpose.AccountPhone, user.PhoneNumber, code, cancellationToken))
        {
            return Result.Failure(ErrorCodes.OtpInvalid);
        }

        user.IsPhoneVerified = true;
        return await unitOfWork.TrySaveAsync(ErrorCodes.PhoneTaken, cancellationToken);
    }
}
