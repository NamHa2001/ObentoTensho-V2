namespace ObentoTensho.Infrastructure.Persistence;

// DB-16: số thứ tự trong ngày VN cho đặt bàn/đơn — chỉ tăng bằng UPDATE … OUTPUT, không đọc-sửa-ghi
public sealed class DailyCounter
{
    public const string ReservationKind = "reservation";
    public const string OrderKind = "order";

    public required DateOnly Date { get; init; }

    public required string Kind { get; init; }

    public int LastValue { get; init; }
}
