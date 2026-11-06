using System.Globalization;
using System.Text;
using ObentoTensho.Core.Reservations.Domain;

namespace ObentoTensho.Infrastructure.Messaging;

// Tệp lịch iCalendar (RFC 5545) để khách bấm "Thêm vào lịch" (docs/srs/11 §4.1). Giờ ghi theo UTC (hậu tố Z)
// nên ứng dụng lịch tự đổi sang múi giờ của máy; UID cố định theo đặt bàn nên thư sau cập nhật đúng sự kiện cũ.
public static class ReservationCalendar
{
    // Không có giờ kết thúc riêng ở Phase 1 (BR-TBL-17) — lấy khoảng 2 giờ cho lịch của khách
    private static readonly TimeSpan Duration = TimeSpan.FromHours(2);

    public static string Create(Reservation reservation, DateTime nowUtc)
    {
        var builder = new StringBuilder()
            .Append("BEGIN:VCALENDAR\r\n")
            .Append("VERSION:2.0\r\n")
            .Append("PRODID:-//Obento Tensho//Reservations//VI\r\n")
            .Append("METHOD:PUBLISH\r\n")
            .Append("BEGIN:VEVENT\r\n")
            .Append(CultureInfo.InvariantCulture, $"UID:{reservation.Id:N}@obento-tensho\r\n")
            .Append(CultureInfo.InvariantCulture, $"DTSTAMP:{Stamp(nowUtc)}\r\n")
            .Append(CultureInfo.InvariantCulture, $"DTSTART:{Stamp(reservation.ReservedAtUtc)}\r\n")
            .Append(CultureInfo.InvariantCulture, $"DTEND:{Stamp(reservation.ReservedAtUtc + Duration)}\r\n")
            .Append(CultureInfo.InvariantCulture, $"SUMMARY:{Escape($"Obento Tensho – {reservation.ReservationCode}")}\r\n")
            .Append(CultureInfo.InvariantCulture, $"DESCRIPTION:{Escape($"{reservation.ReservationCode} – {reservation.GuestCount}")}\r\n")
            .Append("END:VEVENT\r\n")
            .Append("END:VCALENDAR\r\n");
        return builder.ToString();
    }

    private static string Stamp(DateTime utc) => utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    // RFC 5545 §3.3.11: thoát \ ; , và xuống dòng trong giá trị văn bản
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .ReplaceLineEndings("\\n");
}
