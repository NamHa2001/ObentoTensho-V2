using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// docs/srs/20 #21. Liên kết trong thư đặt token sau #fragment (SEC-46); one-click thì nhà cung cấp hộp thư POST thẳng vào API nên token nằm ở query
internal sealed class NewsletterEmails(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : INewsletterEmails
{
    public void EnqueueConfirmation(NewsletterSubscriber subscriber, string token)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["link"] = $"{BaseUrl()}/newsletter/confirm#token={Uri.EscapeDataString(token)}" };
        var key = $"{EmailTypes.Of(EmailKind.NewsletterConfirm)}:{subscriber.ConfirmTokenHash}";
        Enqueue(new EmailRequest(EmailKind.NewsletterConfirm, subscriber.Email, subscriber.Lang, values, key, RelatedEntityId: null));
    }

    public void EnqueueAlreadySubscribed(NewsletterSubscriber subscriber, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        var key = $"{EmailTypes.Of(EmailKind.NewsletterAlreadySubscribed)}:{subscriber.Id}:{day:yyyyMMdd}";
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Enqueue(new EmailRequest(EmailKind.NewsletterAlreadySubscribed, subscriber.Email, subscriber.Lang, values, key, RelatedEntityId: null));
    }

    private void Enqueue(EmailRequest request)
    {
        var now = clock.UtcNow;
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = AccountEmailOutbox.EmailType,
            PayloadJson = JsonSerializer.Serialize(request, OutboxJson.Options),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
        });
    }

    public EmailRequest BuildCampaignEmail(NewsletterCampaign campaign, NewsletterSubscriber subscriber, string unsubscribeToken)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(subscriber);
        var escaped = Uri.EscapeDataString(unsubscribeToken);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["subject"] = campaign.Subject,
            [EmailRequest.TrustedHtmlValue] = campaign.ContentHtml,
            ["contentText"] = NewsletterText.FromHtml(campaign.ContentHtml),
            ["unsubscribeLink"] = $"{BaseUrl()}/newsletter/unsubscribe#token={escaped}",
        };
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["List-Unsubscribe"] = $"<{BaseUrl()}/api/v1/newsletter/one-click?token={escaped}>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };
        return new EmailRequest(EmailKind.Newsletter, subscriber.Email, subscriber.Lang, values, $"{EmailTypes.Of(EmailKind.Newsletter)}:{campaign.Id}:{subscriber.Id}", RelatedEntityId: null, Headers: headers);
    }

    private string BaseUrl() => links.Value.PublicBaseUrl.TrimEnd('/');
}
