using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Abstractions;

namespace ObentoTensho.Infrastructure.Email;

internal sealed class OptionsNotificationSettings(IOptions<EmailOptions> options) : INotificationSettings
{
    public int EmailDailyQuota => options.Value.DailyQuota;
}
