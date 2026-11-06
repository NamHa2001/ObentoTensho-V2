using ObentoTensho.Contracts.Dtos.Auth;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Auth.Services;

// Bước 2FA khi đăng nhập và thiết lập TOTP lần đầu (SEC-15, D-06)
public sealed class StaffTwoFactorService(
    IStaffRepository staff,
    ITotpSecretProtector secretProtector,
    ISecureTokenService secureTokens,
    StaffSignInFlow flow,
    AuthSecurityEvents securityEvents,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<StaffSignInResult>> VerifyAsync(TwoFactorVerifyRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        if (await flow.ResolveAsync(request.ChallengeToken, StaffChallengeKind.TotpVerify, cancellationToken) is not { } resolved)
        {
            return Result.Failure<StaffSignInResult>(ErrorCodes.Unauthenticated);
        }

        var member = resolved.Member;
        var usesRecoveryCode = string.IsNullOrEmpty(request.Code);
        var passed = usesRecoveryCode
            ? await TryUseRecoveryCodeAsync(member, request.RecoveryCode, cancellationToken)
            : await TryConsumeTotpAsync(member, request.Code, cancellationToken) is not null;

        if (!passed)
        {
            // Sai 2FA cũng tính vào khóa tài khoản (docs/srs/10 §4.2)
            await staff.RegisterFailedLoginAsync(member.Id, clock.UtcNow + AuthPolicy.LockoutDuration, cancellationToken);
            await securityEvents.StaffLoginFailedAsync(member.Id, member.Email, usesRecoveryCode ? "wrong_recovery_code" : "wrong_totp", cancellationToken);
            return Result.Failure<StaffSignInResult>(ErrorCodes.InvalidCredentials);
        }

        if (usesRecoveryCode)
        {
            // Mã khôi phục giảm dần: ghi lại để chủ tài khoản/quản trị biết thiết bị 2FA có thể đã mất
            await securityEvents.StaffRecoveryCodeUsedAsync(member, cancellationToken);
        }

        await staff.ResetFailedLoginsAsync(member.Id, cancellationToken);
        return Result.Success(await flow.ContinueAsync(member, secondFactorPassed: true, client, cancellationToken));
    }

    // Sinh khóa mới mỗi lần gọi; chỉ có hiệu lực sau khi xác nhận bằng một mã đúng
    public async Task<Result<TotpSetupResponse>> BeginSetupAsync(string? challengeToken, CancellationToken cancellationToken)
    {
        if (await flow.ResolveAsync(challengeToken, StaffChallengeKind.TotpSetup, cancellationToken) is not { } resolved
            || resolved.Member.TotpEnabled
            || await staff.FindForUpdateAsync(resolved.Member.Id, cancellationToken) is not { } tracked)
        {
            return Result.Failure<TotpSetupResponse>(ErrorCodes.Unauthenticated);
        }

        var secret = Totp.NewSecret();
        tracked.TotpSecretEncrypted = secretProtector.Protect(secret);
        tracked.TotpLastUsedStep = null;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new TotpSetupResponse(
            Base32.Encode(secret),
            Totp.BuildUri(AuthPolicy.TotpIssuer, tracked.Email, secret)));
    }

    public async Task<Result<StaffTotpEnabled>> ConfirmSetupAsync(TotpSetupConfirmRequest request, ClientInfo client, CancellationToken cancellationToken)
    {
        if (await flow.ResolveAsync(request.ChallengeToken, StaffChallengeKind.TotpSetup, cancellationToken) is not { } resolved
            || resolved.Member.TotpEnabled)
        {
            return Result.Failure<StaffTotpEnabled>(ErrorCodes.Unauthenticated);
        }

        var member = resolved.Member;
        if (await TryConsumeTotpAsync(member, request.Code, cancellationToken) is null
            || await staff.FindForUpdateAsync(member.Id, cancellationToken) is not { } tracked)
        {
            await staff.RegisterFailedLoginAsync(member.Id, clock.UtcNow + AuthPolicy.LockoutDuration, cancellationToken);
            await securityEvents.StaffLoginFailedAsync(member.Id, member.Email, "wrong_totp_setup", cancellationToken);
            return Result.Failure<StaffTotpEnabled>(ErrorCodes.InvalidCredentials);
        }

        var codes = RecoveryCodes.Generate();
        await staff.ReplaceRecoveryCodesAsync(member.Id, codes.Select(c => secureTokens.Hash(RecoveryCodes.Normalize(c))), cancellationToken);
        tracked.TotpEnabled = true;
        securityEvents.RecordStaffTwoFactorEnabled(tracked);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var session = await flow.StartSessionAsync(tracked, client, cancellationToken);
        return Result.Success(new StaffTotpEnabled(session, codes));
    }

    // Trả bước đã dùng; null khi mã sai hoặc bước đó đã được dùng (chống phát lại)
    private async Task<long?> TryConsumeTotpAsync(Staff member, string? code, CancellationToken cancellationToken)
    {
        if (member.TotpSecretEncrypted is null || secretProtector.Unprotect(member.TotpSecretEncrypted) is not { } secret)
        {
            return null;
        }

        var step = Totp.FindMatchingStep(secret, code, clock.UtcNow);
        return step is not null && await staff.TryConsumeTotpStepAsync(member.Id, step.Value, cancellationToken) ? step : null;
    }

    private async Task<bool> TryUseRecoveryCodeAsync(Staff member, string? recoveryCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recoveryCode) || recoveryCode.Length > 32)
        {
            return false;
        }

        var hash = secureTokens.Hash(RecoveryCodes.Normalize(recoveryCode));
        return await staff.TryUseRecoveryCodeAsync(member.Id, hash, clock.UtcNow, cancellationToken);
    }
}
