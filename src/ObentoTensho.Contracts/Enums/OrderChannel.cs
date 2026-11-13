namespace ObentoTensho.Contracts.Enums;

// Kênh bán: đặt trước rồi đến lấy (online) hay ăn tại quán (nhân viên gọi món). Giá trị số khớp cột Channel (tinyint)
public enum OrderChannel : byte
{
    Pickup = 0,
    DineIn = 1
}
