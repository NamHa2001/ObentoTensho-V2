using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Thay Cloudflare trong test: chỉ token "turnstile-ok" hợp lệ; Down giả lập Cloudflare không phản hồi
public sealed class FakeTurnstileVerifier : ITurnstileVerifier
{
    public const string ValidToken = "turnstile-ok";

    public bool Down { get; set; }

    public Task<TurnstileVerdict> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(Down ? TurnstileVerdict.Unavailable
            : string.Equals(token, ValidToken, StringComparison.Ordinal) ? TurnstileVerdict.Passed : TurnstileVerdict.Failed);
}
