namespace ObentoTensho.Contracts.Enums;

// BR-TBL-07: 0–2 tự động theo đặt bàn; Cleaning do nhân viên đặt
public enum TableStatus : byte
{
    Available = 0,
    Reserved = 1,
    Occupied = 2,
    Cleaning = 3
}
