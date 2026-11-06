using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-02 cho nhân viên; SEC-03: khóa 5 lần sai, phản hồi và thời gian như nhau
public sealed class StaffLoginService(
    IStaffRepository staff,
    IPasswordHasher passwordHasher,
    StaffSignInFlow flow,
    AuthSecurityEvents securityEvents,
    IClock clock,
    IValidator<LoginRequest> loginValidator)
{
    private static readonly Result<StaffSignInResult> InvalidCredentials = Result.Failure<StaffSignInResult>(ErrorCodes.InvalidCredentials);

    public async Task<Result<StaffSignInResult>> LoginAsync(LoginRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToFailure<StaffSignInResult>();
        }

        var now = clock.UtcNow;
        var email = EmailAddress.Normalize(request.Email);
        var member = await staff.FindByEmailAsync(email, cancellationToken);

        // Chưa nhận lời mời (chưa có mật khẩu), không tồn tại hay đang bị khóa: cùng phản hồi
        if (member?.PasswordHash is null || member.LockoutEndUtc > now)
        {
            passwordHasher.SimulateVerify(request.Password);
            await securityEvents.StaffLoginFailedAsync(member?.Id, email, member is null ? "unknown_account" : "locked_or_no_password", cancellationToken);
            return InvalidCredentials;
        }

        var check = passwordHasher.Verify(member.PasswordHash, request.Password);
        if (check == PasswordCheckResult.Failed)
        {
            await staff.RegisterFailedLoginAsync(member.Id, now + AuthPolicy.LockoutDuration, cancellationToken);
            await securityEvents.StaffLoginFailedAsync(member.Id, email, "wrong_password", cancellationToken);
            return InvalidCredentials;
        }

        if (!member.IsActive)
        {
            await securityEvents.StaffLoginFailedAsync(member.Id, email, "inactive", cancellationToken);
            return InvalidCredentials;
        }

        // Không xóa bộ đếm sai ở đây: nếu còn bước 2FA, xóa sớm sẽ cho phép đoán mã TOTP vô hạn
        // (sai 4 lần → đăng nhập lại → bộ đếm về 0). Bộ đếm chỉ xóa khi qua 2FA hoặc khi cấp phiên.
        if (check == PasswordCheckResult.SuccessRehashNeeded)
        {
            await staff.ReplacePasswordHashAsync(member.Id, member.PasswordHash, passwordHasher.Hash(request.Password), cancellationToken);
        }

        return Result.Success(await flow.ContinueAsync(member, secondFactorPassed: false, client, cancellationToken));
    }
}
