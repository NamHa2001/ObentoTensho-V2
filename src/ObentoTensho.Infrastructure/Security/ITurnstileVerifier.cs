namespace ObentoTensho.Infrastructure.Security;

public interface ITurnstileVerifier
{
    // false khi token sai, đã dùng, sai tên miền — hoặc khi dịch vụ không phản hồi (từ chối, không bỏ qua im lặng — SEC-57)
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}
