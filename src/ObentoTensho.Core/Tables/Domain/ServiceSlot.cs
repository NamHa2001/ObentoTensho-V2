namespace ObentoTensho.Core.Tables.Domain;

// Ca phục vụ: sức chứa đặt bàn theo ca (D-09); DayOfWeek null = mọi ngày
public sealed class ServiceSlot
{
    public int Id { get; private set; }

    public byte? DayOfWeek { get; set; }

    public required TimeOnly StartTime { get; set; }

    public required TimeOnly EndTime { get; set; }

    public required int MaxReservations { get; set; }

    public int? MaxGuests { get; set; }

    public bool IsActive { get; set; } = true;

    public byte[] RowVersion { get; private set; } = [];

    public bool AppliesTo(byte dayOfWeek) => DayOfWeek is null || DayOfWeek == dayOfWeek;
}
