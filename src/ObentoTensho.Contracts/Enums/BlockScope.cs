namespace ObentoTensho.Contracts.Enums;

// BR-TBL-18: khóa riêng đặt bàn, riêng đặt món mang về, hoặc cả hai
public enum BlockScope : byte
{
    Both = 0,
    Reservation = 1,
    Order = 2
}
