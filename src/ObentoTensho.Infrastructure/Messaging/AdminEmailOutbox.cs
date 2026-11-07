using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Customers.Abstractions;
using ObentoTensho.Core.Customers.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

internal sealed class AdminEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IAdminEmailOutbox
{
    public void EnqueueAlert(string staffEmail, string message, string key)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["message"] = message,
            ["adminLink"] = links.Value.AdminBaseUrl.TrimEnd('/') + "/audit-logs",
        };
        Add(new EmailRequest(EmailKind.AdminAlert, staffEmail, "vi", values, $"{EmailTypes.Of(EmailKind.AdminAlert)}:{key}:{staffEmail}", RelatedEntityId: null));
    }

    public void EnqueueExportReady(string staffEmail, DataExport export, string token)
    {
        ArgumentNullException.ThrowIfNull(export);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["link"] = $"{links.Value.AdminBaseUrl.TrimEnd('/')}/exports/download#token={Uri.EscapeDataString(token)}",
        };
        Add(new EmailRequest(EmailKind.DataExportReady, staffEmail, "vi", values, $"{EmailTypes.Of(EmailKind.DataExportReady)}:{export.Id:N}", RelatedEntityId: null));
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
