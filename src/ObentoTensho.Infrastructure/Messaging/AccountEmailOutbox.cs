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
    private const string ConfirmEmailPath = "/account/confirm-email#token=";
    private const string NotMePath = "/auth/not-me#token=";

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

    public void EnqueueEmailChangeVerify(AccountRecipient recipient, string newEmail, string token) =>
        Enqueue(new EmailRequest(
            EmailKind.EmailChangeVerify,
            newEmail,
            recipient.Language,
            Values(recipient.FullName, ("link", Link(links.Value.PublicBaseUrl, ConfirmEmailPath, token))),
            $"email_change_verify:{recipient.Id:N}:{Guid.CreateVersion7():N}",
            recipient.Id.ToString()));

    public void EnqueueEmailChangeNotice(AccountRecipient recipient, DateTime atUtc) =>
        Enqueue(new EmailRequest(
            EmailKind.EmailChangeNotice,
            recipient.Email,
            recipient.Language,
            Values(recipient.FullName, ("changedAt", clock.ToLocal(atUtc).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture))),
            $"email_change_notice:{recipient.Id:N}:{atUtc.Ticks}",
            recipient.Id.ToString()));

    public void EnqueueAccountDeletionScheduled(AccountRecipient recipient, DateTime scheduledAtUtc) =>
        Enqueue(new EmailRequest(
            EmailKind.AccountDeletionScheduled,
            recipient.Email,
            recipient.Language,
            Values(recipient.FullName, ("date", clock.ToLocal(scheduledAtUtc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))),
            $"account_deletion_scheduled:{recipient.Id:N}:{scheduledAtUtc.Ticks}",
            recipient.Id.ToString()));

    public void EnqueueAccountLocked(AccountRecipient recipient, DateTime atUtc) => EnqueueStatus(EmailKind.AccountLocked, recipient, atUtc);

    public void EnqueueAccountUnlocked(AccountRecipient recipient, DateTime atUtc) => EnqueueStatus(EmailKind.AccountUnlocked, recipient, atUtc);

    public void EnqueueLoginLockout(AccountRecipient recipient, DateTime atUtc) => EnqueueStatus(EmailKind.LoginLockout, recipient, atUtc);

    public void EnqueueStaffNewDevice(AccountRecipient recipient, ClientInfo client, DateTime atUtc, string notMeToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        Enqueue(new EmailRequest(
            EmailKind.StaffNewDevice,
            recipient.Email,
            recipient.Language,
            Values(
                recipient.FullName,
                ("at", clock.ToLocal(atUtc).ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)),
                ("ip", client.IpAddress ?? "?"),
                ("userAgent", client.UserAgent ?? "?"),
                ("notMeLink", Link(links.Value.AdminBaseUrl, NotMePath, notMeToken))),
            $"staff_new_device:{recipient.Id:N}:{atUtc.Ticks}",
            recipient.Id.ToString()));
    }

    private void EnqueueStatus(EmailKind kind, AccountRecipient recipient, DateTime atUtc) =>
        Enqueue(new EmailRequest(
            kind,
            recipient.Email,
            recipient.Language,
            Values(recipient.FullName),
            $"{EmailTypes.Of(kind)}:{recipient.Id:N}:{atUtc.Ticks}",
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
