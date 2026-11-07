using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Settings.Abstractions;
using static ObentoTensho.Core.Settings.Domain.SettingKeys;

namespace ObentoTensho.Infrastructure.Settings;

internal sealed class StoreNotificationSettings(ISettingsReader s) : INotificationSettings
{
    public int EmailDailyQuota => s.GetInt(Core.Settings.Domain.SettingKeys.EmailDailyQuota);
}
