using ObentoTensho.Core.Common;

namespace ObentoTensho.Core.Auth.Services;

// BR-AUTH-03, SEC-51: sai 5 lần trong 15 phút ⇒ chặn cặp (tài khoản, IP) đó 15 phút — kẻ xấu ở IP khác không khóa được
// chủ tài khoản. Sai từ nhiều IP cộng dồn 20 lần ⇒ khóa cả tài khoản 15 phút và báo chủ tài khoản. Khóa tự mở.
// Đếm theo email đã chuẩn hóa kể cả khi tài khoản không tồn tại, để phản hồi không lộ email nào có thật
public sealed class LoginGuard(IActionThrottle throttle)
{
    public const int MaxPerAccountAndIp = 5;
    public const int MaxPerAccount = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool IsBlocked(string scope, string account, string? ip) => throttle.Count(PairKey(scope, account, ip)) >= MaxPerAccountAndIp;

    // true khi lần sai này vừa chạm ngưỡng khóa cả tài khoản
    public bool RegisterFailure(string scope, string account, string? ip)
    {
        throttle.Increment(PairKey(scope, account, ip), Window);
        return throttle.Increment(AccountKey(scope, account), Window) == MaxPerAccount;
    }

    public void Reset(string scope, string account, string? ip)
    {
        throttle.Reset(PairKey(scope, account, ip));
        throttle.Reset(AccountKey(scope, account));
    }

    private static string PairKey(string scope, string account, string? ip) => $"login:{scope}:{account}:{ip ?? "unknown"}";

    private static string AccountKey(string scope, string account) => $"login:{scope}:{account}";
}
