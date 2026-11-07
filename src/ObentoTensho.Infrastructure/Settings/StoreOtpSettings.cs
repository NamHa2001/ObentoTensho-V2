using ObentoTensho.Core.Settings.Abstractions;
using ObentoTensho.Core.Verification.Abstractions;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

internal sealed class StoreOtpSettings(ISettingsReader s) : IOtpSettings
{
    public int DailyQuota => s.GetInt(OtpDailyQuota);

    public int TtlSeconds => s.GetInt(OtpTtlSeconds);

    public int ResendSeconds => s.GetInt(OtpResendSeconds);

    public int MaxAttempts => s.GetInt(OtpMaxAttempts);
}
