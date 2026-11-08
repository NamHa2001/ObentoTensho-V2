namespace ObentoTensho.Api.Http;

// BR-ORD-20: chỉ khách vãng lai phải qua Turnstile khi đặt món; khách đã đăng nhập đã có giới hạn theo tài khoản
public sealed class GuestTurnstileFilter(TurnstileGate gate) : TurnstileFilter(gate)
{
    protected override bool IsExempt(HttpContext http) => http.User.CustomerId() is not null;
}
