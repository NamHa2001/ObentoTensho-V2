namespace ObentoTensho.Contracts.Enums;

// docs/srs/12 §6: máy trạng thái đơn mang về; giá trị số khớp cột Status (tinyint)
public enum OrderStatus : byte
{
    Pending = 0,
    Preparing = 1,
    Ready = 2,
    Completed = 3,
    Cancelled = 4
}
