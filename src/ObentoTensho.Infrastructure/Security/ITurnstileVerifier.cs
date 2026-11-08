namespace ObentoTensho.Infrastructure.Security;

public interface ITurnstileVerifier
{
    // Failed khi token sai, đã dùng, sai tên miền
    Task<TurnstileVerdict> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
