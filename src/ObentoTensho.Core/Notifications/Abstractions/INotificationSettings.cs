namespace ObentoTensho.Core.Notifications.Abstractions;

// `email.dailyQuota` (docs/srs/19 §3)
public interface INotificationSettings
{
    int EmailDailyQuota { get; }
}
