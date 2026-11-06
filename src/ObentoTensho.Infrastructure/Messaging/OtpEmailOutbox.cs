using System.Globalization;
using System.Text.Json;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Core.Verification.Abstractions;
using ObentoTensho.Core.Verification.Domain;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// Mã rõ chỉ nằm trong hộp thư đi tới khi gửi xong (payload bị xóa sau đó)
internal sealed class OtpEmailOutbox(AppDbContext db, IClock clock) : IOtpEmailOutbox
{
    public void EnqueueCode(string email, string language, string code, Guid challengeKey)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code"] = code,
            ["minutes"] = ((int)OtpPolicy.Lifetime.TotalMinutes).ToString(CultureInfo.InvariantCulture),
        };
        var request = new EmailRequest(EmailKind.VerificationCode, email, language, values, $"verification_code:{challengeKey:N}", RelatedEntityId: null);
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
