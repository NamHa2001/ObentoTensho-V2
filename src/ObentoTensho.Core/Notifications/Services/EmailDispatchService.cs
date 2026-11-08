using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Notifications.Abstractions;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Notifications.Services;

// Gửi một email từ hộp thư đi: chống gửi trùng (BR-NTF-03), giới hạn theo địa chỉ nhận (BR-NTF-08),
// hạn mức ngày theo ưu tiên (BR-NTF-12), ghi nhật ký không chứa nội dung (BR-NTF-06)
public sealed class EmailDispatchService(
    IEmailLogRepository logs,
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    INotificationSettings settings,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int ErrorMaxLength = 500;

    public async Task<DispatchOutcome> DispatchAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var log = await logs.FindForUpdateAsync(request.IdempotencyKey, cancellationToken);
        switch (log?.Status)
        {
            case EmailStatus.Sent:
                return DispatchOutcome.Sent;
            case EmailStatus.Suppressed:
                return DispatchOutcome.Suppressed;
        }

        if (log is null)
        {
            var limit = await RecipientLimitAsync(request, cancellationToken);
            if (limit == RecipientLimit.Suppress)
            {
                logs.Add(NewLog(request, EmailStatus.Suppressed, "recipient_limit"));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return DispatchOutcome.Suppressed;
            }

            var sentToday = await logs.CountSentSinceAsync(StartOfTodayUtc(), cancellationToken);
            if (limit == RecipientLimit.Defer || EmailPolicy.ShouldDefer(request.Kind, sentToday, settings.EmailDailyQuota))
            {
                return DispatchOutcome.Deferred;
            }

            log = NewLog(request, EmailStatus.Pending, error: null);
            logs.Add(log);
        }

        return await SendAsync(request, log, cancellationToken);
    }

    // Hộp thư đi đã thử đủ số lần: đánh dấu thất bại để hiện trong trang quản trị (BR-NTF-01)
    public async Task MarkFailedAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        if (await logs.FindForUpdateAsync(idempotencyKey, cancellationToken) is { Status: EmailStatus.Pending } log)
        {
            log.Status = EmailStatus.Failed;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    // Thời điểm 00:00 ngày mai giờ VN — thư bị hoãn vì hạn mức chờ tới lúc này
    public DateTime NextQuotaResetUtc() => clock.ToUtc(clock.TodayLocal.AddDays(1), TimeOnly.MinValue);

    private async Task<DispatchOutcome> SendAsync(EmailRequest request, EmailLog log, CancellationToken cancellationToken)
    {
        log.Attempts++;
        try
        {
            await sender.SendAsync(renderer.Render(request), cancellationToken);
            log.Status = EmailStatus.Sent;
            log.SentAtUtc = clock.UtcNow;
            log.LastError = null;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return DispatchOutcome.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LastError = Truncate($"{ex.GetType().Name}: {ex.Message}");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return DispatchOutcome.RetryLater;
        }
    }

    private async Task<RecipientLimit> RecipientLimitAsync(EmailRequest request, CancellationToken cancellationToken)
    {
        if (EmailPolicy.IsInternal(request.Kind))
        {
            return RecipientLimit.Within;
        }

        var to = request.To;
        var sameKindLastHour = await logs.CountSentToAsync(to, [EmailTypes.Of(request.Kind)], clock.UtcNow.AddHours(-1), cancellationToken);
        var allToday = await logs.CountSentToAsync(to, types: null, StartOfTodayUtc(), cancellationToken);
        return EmailPolicy.CheckRecipient(request.Kind, sameKindLastHour, allToday);
    }

    private EmailLog NewLog(EmailRequest request, EmailStatus status, string? error) => new()
    {
        Type = EmailTypes.Of(request.Kind),
        ToEmail = request.To,
        Status = status,
        LastError = error,
        RelatedEntityId = request.RelatedEntityId,
        IdempotencyKey = request.IdempotencyKey,
        CreatedAtUtc = clock.UtcNow,
    };

    private DateTime StartOfTodayUtc() => clock.ToUtc(clock.TodayLocal, TimeOnly.MinValue);

    private static string Truncate(string value) => value.Length <= ErrorMaxLength ? value : value[..ErrorMaxLength];
}
