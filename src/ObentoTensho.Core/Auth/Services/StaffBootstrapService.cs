using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// SEC-15: SuperAdmin đầu tiên tạo từ biến môi trường khi hệ thống chưa có SuperAdmin nào;
// bắt đổi mật khẩu và thiết lập 2FA ở lần đăng nhập đầu
public sealed class StaffBootstrapService(
    IStaffRepository staff,
    IPasswordHasher passwordHasher,
    ISecureTokenService secureTokens,
    ICommonPasswordList commonPasswords,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<bool> EnsureSuperAdminAsync(string email, string fullName, string password, CancellationToken cancellationToken)
    {
        if (await staff.AnyWithRoleAsync(StaffRole.SuperAdmin, cancellationToken))
        {
            return false;
        }

        var normalized = EmailAddress.Normalize(email);
        if (!EmailValidator.IsValid(normalized) || password.Length < AuthPolicy.PasswordMinLength
            || commonPasswords.Contains(password) || PasswordRules.ContainsEmail(password, normalized))
        {
            throw new InvalidOperationException("Cấu hình Seed:SuperAdmin không hợp lệ: email sai hoặc mật khẩu yếu.");
        }

        var now = clock.UtcNow;
        staff.Add(new Staff
        {
            FullName = fullName.Trim(),
            Email = normalized,
            PasswordHash = passwordHasher.Hash(password),
            PasswordChangedAtUtc = now,
            Role = StaffRole.SuperAdmin,
            MustChangePassword = true,
            SecurityStamp = secureTokens.NewSecurityStamp(),
            CreatedAtUtc = now,
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
