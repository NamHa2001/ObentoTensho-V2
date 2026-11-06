using ObentoTensho.Core.Reservations.Domain;
using ObentoTensho.Infrastructure.Messaging;

namespace ObentoTensho.UnitTests.Reservations;

public sealed class ReservationCalendarTests
{
    [Fact]
    public void Tep_ics_dung_RFC_5545_gio_UTC_va_UID_on_dinh()
    {
        var reservation = new Reservation
        {
            ReservationCode = "OBT-8K4Q-7ZM2",
            DailySeq = 1,
            GuestName = "An",
            GuestPhone = "+84912345678",
            Lang = "vi",
            ReservedDate = new DateOnly(2026, 11, 8),
            ReservedTime = new TimeOnly(18, 30),
            ReservedAtUtc = new DateTime(2026, 11, 8, 11, 30, 0, DateTimeKind.Utc),
            SlotId = 1,
            AdultCount = 2,
            CreatedAtUtc = DateTime.UnixEpoch,
        };

        var ics = ReservationCalendar.Create(reservation, new DateTime(2026, 11, 7, 3, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20261108T113000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261108T133000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains($"UID:{reservation.Id:N}@obento-tensho\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
    }
}
