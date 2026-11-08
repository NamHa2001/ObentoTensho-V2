namespace ObentoTensho.Infrastructure.Messaging;

// Kênh SMS cho OTP. "log": chỉ ghi log (phát triển/test, không tốn tiền). Nhà cung cấp thật (eSMS, SpeedSMS…)
// thêm khi chủ dự án ký hợp đồng brandname; Production không chạy với "log"
public sealed class SmsOptions
{
    public const string SectionName = "Sms";
    public const string LogProvider = "log";

    public string Provider { get; set; } = LogProvider;

    public string? Validate(bool isProduction) =>
        isProduction && string.Equals(Provider, LogProvider, StringComparison.OrdinalIgnoreCase)
            ? "Production cần nhà cung cấp SMS thật (Sms:Provider) để gửi OTP xác minh số điện thoại."
            : null;
}
