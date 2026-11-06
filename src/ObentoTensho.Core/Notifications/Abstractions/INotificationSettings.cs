namespace ObentoTensho.Core.Notifications.Abstractions;

// Cài đặt thông báo; khi có bảng cài đặt (docs/srs/19) sẽ đọc `email.dailyQuota` từ đó
public interface INotificationSettings
{
    int EmailDailyQuota { get; }
}
