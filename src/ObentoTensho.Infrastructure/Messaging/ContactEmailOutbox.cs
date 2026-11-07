using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Content.Abstractions;
using ObentoTensho.Core.Content.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// docs/srs/20 #18, #19. Thư cho khách không chứa gì do người gửi nhập, tránh bị dùng để gửi nội dung tới địa chỉ người khác (DEF-41)
internal sealed class ContactEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IContactEmailOutbox
{
    public void EnqueueReceived(ContactMessage message, string language)
    {
        ArgumentNullException.ThrowIfNull(message);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["menuLink"] = links.Value.PublicBaseUrl.TrimEnd('/') + "/menu",
        };
        Add(new EmailRequest(EmailKind.ContactReceived, message.Email, language, values, $"{EmailTypes.Of(EmailKind.ContactReceived)}:{message.Id}", RelatedEntityId: null));
    }

    public void EnqueueInternal(ContactMessage message, string staffEmail)
    {
        ArgumentNullException.ThrowIfNull(message);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullName"] = message.FullName,
            ["contactEmail"] = message.Email,
            ["contactPhone"] = message.PhoneNumber ?? "—",
            ["subject"] = message.Subject ?? "—",
            ["message"] = message.Message,
            ["adminLink"] = links.Value.AdminBaseUrl.TrimEnd('/') + "/contact-messages",
        };
        Add(new EmailRequest(EmailKind.ContactInternal, staffEmail, "vi", values, $"{EmailTypes.Of(EmailKind.ContactInternal)}:{message.Id}:{staffEmail}", RelatedEntityId: null));
    }

    private void Add(EmailRequest request)
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
}
