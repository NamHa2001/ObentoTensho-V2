using System.Globalization;
using System.Security.Cryptography;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Contracts.Errors;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Validators;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Verification.Abstractions;
using ObentoTensho.Core.Verification.Domain;

namespace ObentoTensho.Core.Verification.Services;

// BR-AUTH-18, SEC-77: dịch vụ OTP dùng chung. Mã gắn mục đích và địa chỉ nhận; chỉ lưu băm; so sánh hằng thời gian.
public sealed class OtpService(
    IOtpRepository challenges,
    IOtpEmailOutbox emails,
    IOtpSettings settings,
    ISecureTokenService secureTokens,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    // Vượt giới hạn trả RATE_LIMITED; cùng thông điệp cho mọi địa chỉ nên không lộ địa chỉ nào có tài khoản
    public async Task<Result> SendAsync(OtpPurpose purpose, string? email, string? language, string? ipAddress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailValidator.IsValid(EmailAddress.Normalize(email)) || !Enum.IsDefined(purpose))
        {
            return Result.Failure(ErrorCodes.ValidationFailed);
        }

        var target = EmailAddress.Normalize(email);
        var targetHash = Hash(target);
        var ipHash = ipAddress is null ? null : Hash(ipAddress);
        if (await IsThrottledAsync(purpose, targetHash, ipHash, cancellationToken))
        {
            return Result.Failure(ErrorCodes.RateLimited);
        }

        var now = clock.UtcNow;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        challenges.Add(new OtpChallenge
        {
            Purpose = purpose,
            TargetHash = targetHash,
            CodeHash = CodeHash(targetHash, code),
            ExpiresAtUtc = now + OtpPolicy.Lifetime,
            IpHash = ipHash,
            CreatedAtUtc = now,
        });
        var lang = language is not null && AuthPolicy.SupportedLanguages.Contains(language, StringComparer.Ordinal) ? language : AuthPolicy.DefaultLanguage;
        emails.EnqueueCode(target, lang, code, Guid.CreateVersion7());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // Sai thì tăng số lần thử; sai đủ 5 lần thì mã hết hiệu lực, phải xin mã mới (có chờ 60 giây)
    public async Task<bool> VerifyAsync(OtpPurpose purpose, string email, string? code, Guid? relatedEntityId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var targetHash = Hash(EmailAddress.Normalize(email));
        var challenge = await challenges.FindUsableForUpdateAsync(purpose, targetHash, now, cancellationToken);
        if (challenge is null || string.IsNullOrEmpty(code) || code.Length != OtpPolicy.CodeLength)
        {
            return false;
        }

        if (!secureTokens.HashEquals(challenge.CodeHash, CodeHash(targetHash, code)))
        {
            challenge.Attempts++;
            if (challenge.Attempts >= OtpPolicy.MaxAttempts)
            {
                challenge.ExpiresAtUtc = now;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }

        challenge.VerifiedAtUtc = now;
        challenge.RelatedEntityId = relatedEntityId;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> IsThrottledAsync(OtpPurpose purpose, string targetHash, string? ipHash, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (await challenges.LastSentAtAsync(purpose, targetHash, cancellationToken) is { } last && now - last < OtpPolicy.ResendInterval)
        {
            return true;
        }

        var hourAgo = now.AddHours(-1);
        if (await challenges.CountForTargetSinceAsync(targetHash, hourAgo, cancellationToken) >= OtpPolicy.MaxPerHourPerTarget
            || (ipHash is not null && await challenges.CountForIpSinceAsync(ipHash, hourAgo, cancellationToken) >= OtpPolicy.MaxPerHourPerIp))
        {
            return true;
        }

        var startOfDay = clock.ToUtc(clock.TodayLocal, TimeOnly.MinValue);
        return await challenges.CountSinceAsync(startOfDay, cancellationToken) >= settings.DailyQuota;
    }

    private string Hash(string value) => secureTokens.Hash(value);

    // Băm kèm địa chỉ nhận: lộ bảng thì cũng không dùng chung một bảng tra cho mọi mã
    private string CodeHash(string targetHash, string code) => secureTokens.Hash($"{targetHash}:{code}");
}
