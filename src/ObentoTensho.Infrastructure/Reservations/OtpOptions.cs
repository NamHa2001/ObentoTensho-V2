using ObentoTensho.Core.Verification.Abstractions;

namespace ObentoTensho.Infrastructure.Reservations;

// `otp.*` (docs/srs/19 §3); kênh mặc định email (miễn phí)
public sealed class OtpOptions : IOtpSettings
{
    public const string SectionName = "Otp";

    public int DailyQuota { get; set; } = 200;
}
