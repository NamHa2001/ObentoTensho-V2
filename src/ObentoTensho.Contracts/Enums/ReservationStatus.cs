namespace ObentoTensho.Contracts.Enums;

// docs/srs/11 §6: máy trạng thái đặt bàn; giá trị số khớp cột Status (tinyint)
public enum ReservationStatus : byte
{
    Pending = 0,
    Confirmed = 1,
    CheckedIn = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}
