namespace ObentoTensho.Contracts.Enums;

// BR-AUTH-18: mã gắn mục đích — mã cho đặt bàn không dùng được để xác minh việc khác
public enum OtpPurpose : byte
{
    ReservationContact = 0,
    AccountPhone = 1
}
