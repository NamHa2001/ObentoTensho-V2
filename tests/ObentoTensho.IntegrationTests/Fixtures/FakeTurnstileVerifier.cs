using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.IntegrationTests.Fixtures;

// Thay Cloudflare trong test: chỉ token "turnstile-ok" hợp lệ
public sealed class FakeTurnstileVerifier : ITurnstileVerifier
{
    public const string ValidToken = "turnstile-ok";

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(token, ValidToken, StringComparison.Ordinal));
}
