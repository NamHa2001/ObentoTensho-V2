namespace ObentoTensho.Api.Configuration;

// docs/srs/03 §2. Số liệu đo lại bằng k6 trước khi chạy thật (D-07)
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    // Đăng nhập, đăng ký, các bước 2FA, nhận lời mời, đổi mật khẩu
    public int LoginPerMinute { get; set; } = 10;

    // docs/srs/03 §2 `reservation`: tạo đặt bàn (giới hạn chính là theo SĐT — BR-RSV-20; IP chỉ chặn lạm dụng thô)
    public int ReservationPerHour { get; set; } = 10;

    // BR-ORD-20 `order`: mỗi IP tối đa 10 đơn/giờ (giới hạn chính là theo SĐT)
    public int OrderPerHour { get; set; } = 10;

    // `lookup`: tra cứu/hủy đặt bàn và đơn theo mã
    public int LookupPerMinute { get; set; } = 10;

    // Mọi yêu cầu còn lại
    public int GeneralPerMinute { get; set; } = 100;
}
