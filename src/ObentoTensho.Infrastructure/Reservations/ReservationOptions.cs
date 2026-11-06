using ObentoTensho.Core.Reservations.Abstractions;

namespace ObentoTensho.Infrastructure.Reservations;

// Giá trị mặc định theo docs/srs/19 §3 (`reservation.*`)
public sealed class ReservationOptions : IReservationSettings
{
    public const string SectionName = "Reservation";

    public int MaxGuestsPerBooking { get; set; } = 20;

    public int MaxAdvanceDays { get; set; } = 60;

    public int MaxActivePerPhone { get; set; } = 2;

    public bool RequireContactVerify { get; set; }

    public int CallAttempts { get; set; } = 3;

    public int NoShowFlagCount { get; set; } = 2;

    public int LargePartySize { get; set; } = 8;

    public int TimeStepMinutes { get; set; } = 15;

    public bool IsValid() =>
        MaxGuestsPerBooking is >= 1 and <= 100 && MaxAdvanceDays is >= 1 and <= 365 && MaxActivePerPhone is >= 1 and <= 10
        && CallAttempts is >= 1 and <= 10 && NoShowFlagCount is >= 1 and <= 10 && LargePartySize is >= 2 and <= 50
        && TimeStepMinutes is 5 or 10 or 15 or 30 or 60;
}
