using FluentValidation;
using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// FR-AUTH-02 cho nhân viên; SEC-03, SEC-51: chặn theo (tài khoản, IP), khóa cả tài khoản khi sai từ nhiều nơi; phản hồi và thời gian như nhau
public sealed class StaffLoginService(
    IStaffRepository staff,
    IPasswordHasher passwordHasher,
    StaffSignInFlow flow,
    AuthSecurityEvents securityEvents,
    LoginGuard guard,
    IAccountEmailOutbox emails,
    IUnitOfWork unitOfWork,
    IClock clock,
    IValidator<LoginRequest> loginValidator)
{
    private static readonly Result<StaffSignInResult> InvalidCredentials = Result.Failure<StaffSignInResult>(ErrorCodes.InvalidCredentials);
    private const string Scope = "staff";

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
        if (member?.PasswordHash is null || member.LockoutEndUtc > now || guard.IsBlocked(Scope, email, client.IpAddress))
        {
            passwordHasher.SimulateVerify(request.Password);
            guard.RegisterFailure(Scope, email, client.IpAddress);
            await securityEvents.StaffLoginFailedAsync(member?.Id, email, member is null ? "unknown_account" : "locked_or_no_password", cancellationToken);
            return InvalidCredentials;
        }

        var check = passwordHasher.Verify(member.PasswordHash, request.Password);
        if (check == PasswordCheckResult.Failed)
        {
            if (guard.RegisterFailure(Scope, email, client.IpAddress))
            {
                await staff.LockUntilAsync(member.Id, now + LoginGuard.Window, cancellationToken);
                emails.EnqueueLoginLockout(new AccountRecipient(member.Id, member.Email, member.FullName, AuthPolicy.DefaultLanguage, IsStaff: true), now);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await securityEvents.StaffLoginFailedAsync(member.Id, email, "wrong_password", cancellationToken);
            return InvalidCredentials;
        }

        guard.Reset(Scope, email, client.IpAddress);

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
