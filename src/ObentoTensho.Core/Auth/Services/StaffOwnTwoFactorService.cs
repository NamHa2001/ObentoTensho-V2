using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// Nhân viên đã đăng nhập tự quản lý 2FA (docs/srs/10 §4, 2fa/recovery): bật tự nguyện với vai trò không bắt buộc,
// tạo lại mã khôi phục, tắt 2FA (chỉ vai trò không bắt buộc). Tạo lại mã và tắt 2FA cần step-up (SEC-61) ở API.
// Mọi thay đổi đều có audit và thư báo cho chính nhân viên
public sealed class StaffOwnTwoFactorService(
    IStaffRepository staff,
    ITotpSecretProtector secretProtector,
    ISecureTokenService secureTokens,
    StaffTwoFactorService twoFactor,
    AuthSecurityEvents securityEvents,
    IAdminEmailOutbox adminEmails,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    // Sinh khóa mới mỗi lần gọi; chỉ có hiệu lực sau khi xác nhận bằng một mã đúng
    public async Task<Result<TotpSetupResponse>> BeginSetupAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (await staff.FindForUpdateAsync(staffId, cancellationToken) is not { } member)
        {
            return Result.Failure<TotpSetupResponse>(ErrorCodes.NotFound);
        }

        if (member.TotpEnabled)
        {
            return Result.Failure<TotpSetupResponse>(ErrorCodes.InvalidTransition);
        }

        var secret = Totp.NewSecret();
        member.TotpSecretEncrypted = secretProtector.Protect(secret);
        member.TotpLastUsedStep = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new TotpSetupResponse(Base32.Encode(secret), Totp.BuildUri(AuthPolicy.TotpIssuer, member.Email, secret)));
    }

    public async Task<Result<RecoveryCodesDto>> ConfirmSetupAsync(Guid staffId, TotpCodeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await staff.FindByIdAsync(staffId, cancellationToken) is not { } member || member.TotpEnabled || member.TotpSecretEncrypted is null)
        {
            return Result.Failure<RecoveryCodesDto>(ErrorCodes.InvalidTransition);
        }

        if (await twoFactor.TryConsumeTotpAsync(member, request.Code, cancellationToken) is null
            || await staff.FindForUpdateAsync(staffId, cancellationToken) is not { } tracked)
        {
            return Result.Failure<RecoveryCodesDto>(ErrorCodes.InvalidCredentials);
        }

        tracked.TotpEnabled = true;
        var codes = await ReplaceCodesAsync(tracked, cancellationToken);
        securityEvents.RecordStaffTwoFactorEnabled(tracked);
        Notify(tracked, "Xác thực hai bước (2FA) vừa được bật cho tài khoản quản trị của bạn.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new RecoveryCodesDto(codes));
    }

    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (await staff.FindForUpdateAsync(staffId, cancellationToken) is not { TotpEnabled: true } member)
        {
            return Result.Failure<RecoveryCodesDto>(ErrorCodes.InvalidTransition);
        }

        var codes = await ReplaceCodesAsync(member, cancellationToken);
        securityEvents.RecordStaffRecoveryCodesRegenerated(member);
        Notify(member, "Bộ mã khôi phục 2FA vừa được tạo lại; các mã cũ không còn dùng được.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new RecoveryCodesDto(codes));
    }

    // D-06: SuperAdmin/Manager bắt buộc 2FA nên không tắt được
    public async Task<Result> DisableAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (await staff.FindForUpdateAsync(staffId, cancellationToken) is not { TotpEnabled: true } member)
        {
            return Result.Failure(ErrorCodes.InvalidTransition);
        }

        if (AuthPolicy.RequiresTwoFactor(member.Role))
        {
            return Result.Failure(ErrorCodes.Forbidden);
        }

        member.TotpEnabled = false;
        member.TotpSecretEncrypted = null;
        member.TotpLastUsedStep = null;
        await staff.ReplaceRecoveryCodesAsync(member.Id, [], cancellationToken);
        securityEvents.RecordStaffTwoFactorDisabled(member);
        Notify(member, "Xác thực hai bước (2FA) vừa bị tắt cho tài khoản quản trị của bạn. Nếu không phải bạn, hãy đổi mật khẩu ngay.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<IReadOnlyList<string>> ReplaceCodesAsync(Staff member, CancellationToken cancellationToken)
    {
        var codes = RecoveryCodes.Generate();
        await staff.ReplaceRecoveryCodesAsync(member.Id, codes.Select(c => secureTokens.Hash(RecoveryCodes.Normalize(c))), cancellationToken);
        return codes;
    }

    private void Notify(Staff member, string message) =>
        adminEmails.EnqueueAlert(member.Email, message, $"two-factor:{member.Id:N}:{clock.UtcNow.Ticks}");
}
