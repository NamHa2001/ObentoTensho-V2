using System.Globalization;
using System.Text.Json;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

internal sealed class AccountEmailOutbox(AppDbContext db, IClock clock) : IAccountEmailOutbox
{
    public const string EmailVerificationType = "email.verify-email";
    public const string DuplicateRegistrationType = "email.duplicate-registration";

    // Token thô chỉ nằm trong hộp thư đi đến khi gửi xong; bản đã xử lý bị dọn theo DB-13
    public void EnqueueEmailVerification(User user, string verificationToken) =>
        Enqueue(
            EmailVerificationType,
            new EmailVerificationPayload(user.Id, user.Email, user.FullName, user.PreferredLang, verificationToken),
            $"{EmailVerificationType}:{user.Id:N}:{Guid.CreateVersion7():N}");

    public void EnqueueDuplicateRegistrationNotice(User existingUser, DateOnly localDate) =>
        Enqueue(
            DuplicateRegistrationType,
            new DuplicateRegistrationPayload(existingUser.Id, existingUser.Email, existingUser.FullName, existingUser.PreferredLang),
            $"{DuplicateRegistrationType}:{existingUser.Id:N}:{localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}");

    private void Enqueue<TPayload>(string type, TPayload payload, string idempotencyKey) =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonSerializerOptions.Web),
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = clock.UtcNow,
        });
}
