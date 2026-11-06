namespace ObentoTensho.Core.Reservations.Domain;

// DB-20: số lượt và số khách đã giữ của một ca trong một ngày VN; tăng/giảm bằng UPDATE có điều kiện
public sealed class SlotDayCapacity
{
    public required int SlotId { get; init; }

    public required DateOnly Date { get; init; }

    public int BookedCount { get; init; }

    public int BookedGuests { get; init; }

    public byte[] RowVersion { get; private set; } = [];
}
