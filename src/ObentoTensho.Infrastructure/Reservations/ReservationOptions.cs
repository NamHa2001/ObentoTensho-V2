namespace ObentoTensho.Infrastructure.Reservations;

// Thông số kỹ thuật ngoài danh mục cài đặt
public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    public int TimeStepMinutes { get; set; } = 15;

    public int ReminderLeadMinutes { get; set; } = 120;

    public bool IsValid() => TimeStepMinutes is 5 or 10 or 15 or 30 or 60 && ReminderLeadMinutes is >= 30 and <= 1440;
}
