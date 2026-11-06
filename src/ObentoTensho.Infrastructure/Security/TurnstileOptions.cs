namespace ObentoTensho.Infrastructure.Security;

// SEC-22, SEC-57: Cloudflare Turnstile cho form công khai (đặt bàn, gửi OTP, đăng ký). SecretKey qua biến môi trường.
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public bool Enabled { get; set; }

    public string? SecretKey { get; set; }

    // SEC-57: token phải được cấp cho đúng tên miền của quán
    public IList<string> AllowedHostnames { get; } = [];

    public string? Validate(bool isProduction)
    {
        if (!Enabled)
        {
            return isProduction ? "Production bắt buộc bật Turnstile (Turnstile:Enabled = true)." : null;
        }

        return string.IsNullOrWhiteSpace(SecretKey) || AllowedHostnames.Count == 0
            ? "Thiếu Turnstile:SecretKey hoặc Turnstile:AllowedHostnames."
            : null;
    }
}
