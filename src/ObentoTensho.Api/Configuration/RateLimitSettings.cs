namespace ObentoTensho.Api.Configuration;

// docs/srs/03 §2. Số liệu đo lại bằng k6 trước khi chạy thật (D-07)
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    // Đăng nhập, đăng ký, các bước 2FA, nhận lời mời, đổi mật khẩu
    public int LoginPerMinute { get; set; } = 10;

    // Mọi yêu cầu còn lại
    public int GeneralPerMinute { get; set; } = 100;
}
