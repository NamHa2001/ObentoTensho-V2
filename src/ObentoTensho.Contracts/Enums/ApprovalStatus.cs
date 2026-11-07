namespace ObentoTensho.Contracts.Enums;

// Nguyên tắc bốn mắt: điều chỉnh điểm lớn (BR-LOY-21), voucher giá trị cao (BR-VCH-21) chờ người thứ hai duyệt
public enum ApprovalStatus : byte
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
