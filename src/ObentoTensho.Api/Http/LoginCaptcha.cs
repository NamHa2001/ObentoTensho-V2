using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Services;
using ObentoTensho.Infrastructure.Security;

namespace ObentoTensho.Api.Http;

// SEC-51: sau 3 lần đăng nhập sai của cặp (tài khoản, IP), lần thử tiếp phải kèm token Turnstile.
// Đếm theo email đã chuẩn hóa kể cả email không tồn tại, nên yêu cầu Turnstile không lộ tài khoản nào có thật
public sealed class LoginCaptcha(LoginGuard guard, ITurnstileVerifier verifier)
{
    public async Task<bool> PassesAsync(HttpContext http, string scope, string email)
    {
        ArgumentNullException.ThrowIfNull(http);
        var ip = http.GetClientInfo().IpAddress;
        return !guard.NeedsCaptcha(scope, EmailAddress.Normalize(email), ip)
            || await verifier.VerifyAsync(http.Request.Headers[TurnstileFilter.Header].ToString(), ip, http.RequestAborted);
    }
}
