using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ObentoTensho.Core.Notifications.Domain;
using ObentoTensho.Core.Reservations.Abstractions;
using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Core.Time;
using ObentoTensho.Infrastructure.Email;
using ObentoTensho.Infrastructure.Persistence;

namespace ObentoTensho.Infrastructure.Messaging;

// BR-NTF-04: thư có mã đặt bàn, giờ VN; liên kết quản lý trỏ về trang tra cứu/hủy (token sau #fragment — SEC-46)
internal sealed class ReservationEmailOutbox(AppDbContext db, IClock clock, IOptions<PublicLinkOptions> links) : IReservationEmailOutbox
{
    public void EnqueueReceived(Reservation reservation, string accessToken) =>
        Enqueue(EmailKind.ReservationReceived, reservation, withCalendar: true, ("manageLink", ManageLink(reservation, accessToken)));

    public void EnqueueConfirmed(Reservation reservation, string? accessToken) =>
        Enqueue(EmailKind.ReservationConfirmed, reservation, withCalendar: true, ("manageLink", ManageLink(reservation, accessToken)));

    public void EnqueueReminder(Reservation reservation) =>
        Enqueue(EmailKind.ReservationReminder, reservation, withCalendar: true, ("manageLink", ManageLink(reservation, accessToken: null)));

    public void EnqueueAutoCancelled(Reservation reservation) =>
        Enqueue(EmailKind.ReservationAutoCancelled, reservation, withCalendar: false, ("bookingLink", BookingLink()));

    public void EnqueueRejected(Reservation reservation, string reason) =>
        Enqueue(EmailKind.ReservationRejected, reservation, withCalendar: false, ("reason", reason), ("bookingLink", BookingLink()));

    public void EnqueueCancelled(Reservation reservation) =>
        Enqueue(EmailKind.ReservationCancelled, reservation, withCalendar: false, ("bookingLink", BookingLink()));

    private string BookingLink() => links.Value.PublicBaseUrl.TrimEnd('/') + "/reservations";

    // Có token: mở thẳng trang hủy; không có (thư thứ hai): trang tra cứu bằng mã + 4 số cuối SĐT
    private string ManageLink(Reservation reservation, string? accessToken)
    {
        var baseUrl = links.Value.PublicBaseUrl.TrimEnd('/');
        var code = Uri.EscapeDataString(reservation.ReservationCode);
        return accessToken is null
            ? $"{baseUrl}/reservations/lookup?code={code}"
            : $"{baseUrl}/reservations/manage?code={code}#token={Uri.EscapeDataString(accessToken)}";
    }

    private void Enqueue(EmailKind kind, Reservation reservation, bool withCalendar, params (string Key, string Value)[] extra)
    {
        if (reservation.GuestEmail is null)
        {
            return;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fullName"] = reservation.GuestName,
            ["code"] = reservation.ReservationCode,
            ["date"] = reservation.ReservedDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["time"] = reservation.ReservedTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["guests"] = reservation.GuestCount.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        var now = clock.UtcNow;
        IReadOnlyList<EmailAttachment>? attachments = withCalendar
            ? [new EmailAttachment("obento-tensho.ics", "text/calendar; charset=utf-8; method=PUBLISH", ReservationCalendar.Create(reservation, now))]
            : null;
        var request = new EmailRequest(kind, reservation.GuestEmail, reservation.Lang, values, $"{EmailTypes.Of(kind)}:{reservation.Id:N}", reservation.Id.ToString(), attachments);
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
