using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Auth.Abstractions;
using ObentoTensho.Core.Auth.Domain;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

internal sealed class AccountEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IAccountEmailOutbox
{
    public const string EmailType = "email.send";

    // SEC-46: token đặt ở phần #fragment — trình duyệt không gửi lên máy chủ, không lọt vào log/Referer
    private const string VerifyEmailPath = "/auth/verify-email#token=";
    private const string ResetPasswordPath = "/auth/reset-password#token=";
    private const string StaffInvitePath = "/auth/accept-invite#token=";

    public void EnqueueEmailVerification(User user, string verificationToken) =>
        Enqueue(new EmailRequest(
            EmailKind.VerifyEmail,
            user.Email,
            user.PreferredLang,
            Values(user.FullName, ("link", Link(links.Value.PublicBaseUrl, VerifyEmailPath, verificationToken))),
            $"verify_email:{user.Id:N}:{Guid.CreateVersion7():N}",
            user.Id.ToString()));

    public void EnqueueDuplicateRegistrationNotice(User existingUser, DateOnly localDate) =>
        Enqueue(new EmailRequest(
            EmailKind.DuplicateRegistration,
            existingUser.Email,
            existingUser.PreferredLang,
            Values(existingUser.FullName, ("loginLink", links.Value.PublicBaseUrl + "/auth/login")),
            $"duplicate_registration:{existingUser.Id:N}:{localDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}",
            existingUser.Id.ToString()));

    public void EnqueuePasswordReset(AccountRecipient recipient, string resetToken) =>
        Enqueue(new EmailRequest(
            EmailKind.PasswordReset,
            recipient.Email,
            recipient.Language,
            Values(recipient.FullName, ("link", Link(BaseUrl(recipient), ResetPasswordPath, resetToken))),
            $"password_reset:{recipient.Id:N}:{Guid.CreateVersion7():N}",
            recipient.Id.ToString()));

    public void EnqueueStaffInvite(AccountRecipient recipient, string inviteToken) =>
        Enqueue(new EmailRequest(
            EmailKind.StaffInvite,
            recipient.Email,
            recipient.Language,
            Values(recipient.FullName, ("link", Link(links.Value.AdminBaseUrl, StaffInvitePath, inviteToken))),
            $"staff_invite:{recipient.Id:N}:{Guid.CreateVersion7():N}",
            recipient.Id.ToString()));

    public void EnqueuePasswordChanged(AccountRecipient recipient, DateTime changedAtUtc) =>
        Enqueue(new EmailRequest(
            EmailKind.PasswordChanged,
            recipient.Email,
            recipient.Language,
            Values(
                recipient.FullName,
                ("changedAt", clock.ToLocal(changedAtUtc).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("forgotLink", BaseUrl(recipient) + "/auth/forgot-password")),
            $"password_changed:{recipient.Id:N}:{changedAtUtc.Ticks}",
            recipient.Id.ToString()));

    private string BaseUrl(AccountRecipient recipient) => recipient.IsStaff ? links.Value.AdminBaseUrl : links.Value.PublicBaseUrl;

    private static string Link(string baseUrl, string path, string token) => baseUrl.TrimEnd('/') + path + Uri.EscapeDataString(token);

    private static Dictionary<string, string> Values(string fullName, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["fullName"] = fullName };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        return values;
    }

    private void Enqueue(EmailRequest request)
    {
        var now = clock.UtcNow;
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = EmailType,
            PayloadJson = JsonSerializer.Serialize(request, OutboxJson.Options),
            IdempotencyKey = request.IdempotencyKey,
            CreatedAtUtc = now,
            NextAttemptAtUtc = now,
        });
    }
}
