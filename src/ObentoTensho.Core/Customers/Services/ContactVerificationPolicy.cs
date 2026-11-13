using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Settings.Domain;

namespace ObentoTensho.Core.Customers.Services;

// Chủ dự án 2026-11-13: chưa có kinh phí SMS ⇒ công tắc otp.requirePhoneVerify. Bật: đặt món, đặt bàn, voucher sinh nhật
// cần SĐT đã xác minh bằng OTP SMS (như 2026-11-08); tắt: chỉ cần email đã xác minh
public sealed class ContactVerificationPolicy(IUserRepository users, ISettingsReader settings)
{
    public bool RequirePhone => settings.GetBool(SettingKeys.OtpRequirePhoneVerify);

    // Mã lỗi khi tài khoản chưa đủ điều kiện; null khi được phép
    public async Task<string?> CheckAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (RequirePhone)
        {
            return await users.IsPhoneVerifiedAsync(userId, cancellationToken) ? null : ErrorCodes.PhoneNotVerified;
        }

        return await users.IsEmailVerifiedAsync(userId, cancellationToken) ? null : ErrorCodes.EmailNotVerified;
    }
}
