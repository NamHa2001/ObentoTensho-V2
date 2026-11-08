using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Notifications.Services;

// BR-NTF-12: đã gửi ≥ 80% hạn mức email trong ngày (giờ VN) ⇒ báo Manager/SuperAdmin, mỗi ngày tối đa một lần. Mỗi 30 phút
public sealed class EmailQuotaAlertJob(
    IEmailLogRepository logs,
    INotificationSettings settings,
    IStaffRepository staff,
    IAdminEmailOutbox adminEmails,
    IUnitOfWork unitOfWork,
    IClock clock) : IRecurringJob
{
    public const int WarnPercent = 80;

    public string Name => "notifications.quota-alert";

    public TimeSpan Interval => TimeSpan.FromMinutes(30);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var quota = settings.EmailDailyQuota;
        var sent = await logs.CountSentSinceAsync(clock.ToUtc(clock.TodayLocal, TimeOnly.MinValue), cancellationToken);
        if (quota <= 0 || sent * 100L < quota * (long)WarnPercent)
        {
            return 0;
        }

        var message = $"Hôm nay đã gửi {sent}/{quota} email ({sent * 100L / quota}% hạn mức). Khi hết hạn mức, thư bản tin và thư nhắc sẽ bị hoãn sang ngày sau; thư xác nhận vẫn gửi.";
        var key = $"email-quota:{clock.TodayLocal:yyyyMMdd}";
        var recipients = await staff.ListActiveEmailsAsync([StaffRole.SuperAdmin, StaffRole.Manager], cancellationToken);
        foreach (var email in recipients)
        {
            adminEmails.EnqueueAlert(email, message, key);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // Hôm nay đã báo (cùng khóa thư) — không báo lại
            return 0;
        }

        return recipients.Count;
    }
}
