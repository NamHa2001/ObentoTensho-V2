using ObentoTensho.Api.Configuration;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Auth.Services;

namespace ObentoTensho.Api.Http;

// SEC-51: sau 3 lần đăng nhập sai của cặp (tài khoản, IP), lần thử tiếp phải kèm token Turnstile.
// Đếm theo email đã chuẩn hóa kể cả email không tồn tại, nên yêu cầu Turnstile không lộ tài khoản nào có thật
public sealed class LoginCaptcha(LoginGuard guard, TurnstileGate gate)
{
    public async Task<bool> PassesAsync(HttpContext http, string scope, string email)
    {
        ArgumentNullException.ThrowIfNull(http);
        return !guard.NeedsCaptcha(scope, EmailAddress.Normalize(email), http.GetClientInfo().IpAddress)
            || await gate.PassesAsync(http, RateLimitConfiguration.LoginPolicy);
    }
}
