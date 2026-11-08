using System.Globalization;
using Microsoft.Extensions.Options;
using ObentoTensho.Api.Configuration;
using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Http;

// SEC-57, Q5: Cloudflare lỗi thì không chặn khách (PERF-43) mà cho qua với giới hạn theo IP chỉ còn 1/3 giới hạn
// thường của endpoint; lỗi kéo dài quá 10 phút thì báo SuperAdmin
public sealed partial class TurnstileGate(
    ITurnstileVerifier verifier,
    TurnstileOutage outage,
    IActionThrottle throttle,
    IOptions<RateLimitSettings> limits,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<TurnstileGate> logger)
{
    public const int FallbackDivisor = 3;

    public async Task<bool> PassesAsync(HttpContext http, string policy)
    {
        ArgumentNullException.ThrowIfNull(http);
        var token = http.Request.Headers[TurnstileFilter.Header].ToString();
        var verdict = await verifier.VerifyAsync(token, http.GetClientInfo().IpAddress, http.RequestAborted);
        if (verdict != TurnstileVerdict.Unavailable)
        {
            if (verdict == TurnstileVerdict.Passed)
            {
                outage.Up();
            }

            return verdict == TurnstileVerdict.Passed;
        }

        if (outage.Down(clock.UtcNow) is { } since)
        {
            await AlertAsync(since, http.RequestAborted);
        }

        var (permits, window) = RateLimitConfiguration.Limit(policy, limits.Value);
        var allowed = Math.Max(1, permits / FallbackDivisor);
        var passes = throttle.Increment($"turnstile-fallback:{policy}:{RateLimitConfiguration.ClientKey(http)}", window) <= allowed;
        LogFallback(logger, policy, passes);
        return passes;
    }

    private async Task AlertAsync(DateTime since, CancellationToken cancellationToken)
    {
        var from = clock.ToLocal(since).ToString("HH:mm dd/MM", CultureInfo.InvariantCulture);
        var message = $"Không kiểm được Cloudflare Turnstile từ {from} (giờ VN). Form công khai đang cho qua với giới hạn theo IP chỉ còn 1/{FallbackDivisor}.";
        foreach (var email in await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin], cancellationToken))
        {
            adminEmails.EnqueueAlert(email, message, $"turnstile-outage:{since.Ticks}");
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Đợt này đã báo
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile không phản hồi; giới hạn dự phòng {Policy}, cho qua: {Passes}")]
    private static partial void LogFallback(ILogger logger, string policy, bool passes);
}
