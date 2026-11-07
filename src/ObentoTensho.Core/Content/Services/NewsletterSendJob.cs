using ObentoTensho.Contracts.Enums;
using ObentoTensho.Core.Common;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Notifications.Services;
using ObentoTensho.Core.Time;

namespace ObentoTensho.Core.Content.Services;

// BR-CNT-23: mỗi phút gửi một lô, ghi trạng thái từng người nhận. Bản tin ưu tiên thấp nhất nên hết hạn mức ngày thì
// dừng lô, ngày sau gửi tiếp (BR-NTF-12). Khóa chống trùng của EmailLogs đảm bảo chạy lại không gửi hai lần
public sealed class NewsletterSendJob(
    INewsletterRepository newsletter,
    INewsletterEmails emails,
    INewsletterTokenProtector protector,
    EmailDispatchService dispatcher,
    IUnitOfWork unitOfWork,
    IClock clock) : IRecurringJob
{
    public const int BatchSize = 50;

    public string Name => "content.newsletter-send";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        if (await newsletter.FindDueCampaignAsync(clock.UtcNow, cancellationToken) is not { } campaign)
        {
            return 0;
        }

        if (campaign.Status == NewsletterCampaignStatus.Scheduled)
        {
            campaign.RecipientCount = await newsletter.SnapshotRecipientsAsync(campaign.Id, cancellationToken);
            campaign.Status = NewsletterCampaignStatus.Sending;
            campaign.StartedAtUtc = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var batch = await newsletter.TakePendingAsync(campaign.Id, BatchSize, cancellationToken);
        if (batch.Count == 0)
        {
            campaign.Status = NewsletterCampaignStatus.Finished;
            campaign.FinishedAtUtc = clock.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var processed = 0;
        foreach (var item in batch)
        {
            if (!await SendAsync(campaign, item, cancellationToken))
            {
                break;
            }

            processed++;
        }

        return processed;
    }

    // false: hết hạn mức, dừng lô
    private async Task<bool> SendAsync(NewsletterCampaign campaign, NewsletterSendItem item, CancellationToken cancellationToken)
    {
        var (recipient, subscriber) = item;
        if (!subscriber.IsSubscribed)
        {
            await FailAsync(recipient, "unsubscribed", cancellationToken);
            return true;
        }

        if (protector.Unprotect(subscriber.UnsubscribeTokenProtected) is not { } token)
        {
            await FailAsync(recipient, "unsubscribe_token_unavailable", cancellationToken);
            return true;
        }

        var request = emails.BuildCampaignEmail(campaign, subscriber, token);
        switch (await dispatcher.DispatchAsync(request, cancellationToken))
        {
            case DispatchOutcome.Sent:
                recipient.Status = NewsletterRecipientStatus.Sent;
                recipient.SentAtUtc = clock.UtcNow;
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            case DispatchOutcome.Suppressed:
                await FailAsync(recipient, "recipient_limit", cancellationToken);
                return true;
            case DispatchOutcome.Deferred:
                return false;
            default:
                await RetryOrFailAsync(recipient, request, cancellationToken);
                return true;
        }
    }

    private async Task RetryOrFailAsync(NewsletterRecipient recipient, EmailRequest request, CancellationToken cancellationToken)
    {
        recipient.Attempts++;
        if (recipient.Attempts >= NewsletterRecipient.MaxAttempts)
        {
            await dispatcher.MarkFailedAsync(request.IdempotencyKey, cancellationToken);
            await FailAsync(recipient, "send_failed", cancellationToken);
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Task FailAsync(NewsletterRecipient recipient, string error, CancellationToken cancellationToken)
    {
        recipient.Status = NewsletterRecipientStatus.Failed;
        recipient.Error = error;
        return unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
